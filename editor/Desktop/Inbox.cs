using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The inbox other programs send models to (docs/inbox.md): a folder in the data folder where each delivery is a folder
/// of its own, its send.json and its .glb files. The open map takes a delivery in: the files go into its models folder
/// (a folder per source), the kinds into its catalog, and inbox/.results/&lt;id&gt;.json says how it went. A delivery is
/// read only once it is whole (renamed from .&lt;id&gt;.partial); names starting with '.' are left alone.
/// </summary>
internal static class Inbox
{
    /// <summary>The largest model file taken.</summary>
    internal const long MaxFile = 512L << 20;

    /// <summary>The most files in one delivery.</summary>
    internal const int MaxKinds = 200;

    /// <summary>The groups a kind can be sent in; any other is Other.</summary>
    internal static readonly string[] Categories = ["Trees", "Rocks", "Bushes", "Plants", "Props", "Buildings", "Markers", "Other"];

    /// <summary>The inbox folder (made when read).</summary>
    internal static string Folder
    {
        get
        {
            string f = Path.Combine(AppSettings.DataDir, "inbox");
            _ = Directory.CreateDirectory(f);
            return f;
        }
    }

    /// <summary>A delivery taken in or refused: its id, who sent it, the models it put on the map, or why not.</summary>
    internal sealed record Taken(string Id, string Source, IReadOnlyList<string> Models, string? Error);

    /// <summary>The deliveries waiting (whole ones, oldest first).</summary>
    internal static List<string> Waiting()
    {
        try
        {
            return [.. new DirectoryInfo(Folder).GetDirectories().Where(d => !d.Name.StartsWith('.') && d.LinkTarget == null)
                .OrderBy(d => d.CreationTimeUtc).Select(d => d.FullName)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(e, "listing the inbox");
            return [];
        }
    }

    /// <summary>Takes every waiting delivery into the map; each one's result is written and its folder deleted.</summary>
    internal static List<Taken> TakeAll(MapProject map)
    {
        var all = new List<Taken>();
        foreach (string dir in Waiting())
        {
            string id = Path.GetFileName(dir);
            Taken t;
            try
            {
                t = Take(map, dir);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or FormatException or InvalidOperationException or System.Text.Json.JsonException)
            {
                t = new Taken(id, "", [], e.Message);
            }
            all.Add(t);
            Answer(map, t);
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(e, "deleting a delivery taken from the inbox");
            }
        }
        return all;
    }

    /// <summary>Writes inbox/.results/&lt;id&gt;.json, and lets go of results older than a day.</summary>
    private static void Answer(MapProject map, Taken t)
    {
        string results = Path.Combine(Folder, ".results");
        var o = new JsonObject { ["ok"] = t.Error == null, ["map"] = map.Name, ["models"] = new JsonArray([.. t.Models.Select(m => JsonValue.Create(m))]) };
        if (t.Error != null)
        {
            o["error"] = t.Error;
        }
        try
        {
            SafeFile.WriteAllText(Path.Combine(results, t.Id + ".json"), o.ToJsonString());
            foreach (var old in new DirectoryInfo(results).GetFiles("*.json").Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1)))
            {
                old.Delete();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.CaughtBrief(e, "answering a delivery");
        }
    }

    /// <summary>A plain name: no folder in it, nothing hidden, no device name.</summary>
    private static bool Plain(string name) =>
        name.Length is > 0 and <= 120 && name.Trim() == name && !name.StartsWith('.') && name.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\', ':']) < 0
        && !SafeFile.IsDeviceName(name);

    /// <summary>One delivery into the map: checked whole before anything is written.</summary>
    private static Taken Take(MapProject map, string dir)
    {
        string id = Path.GetFileName(dir);
        var send = new FileInfo(Path.Combine(dir, "send.json"));
        if (!send.Exists || send.LinkTarget != null || send.Length > 1 << 20)
        {
            return new(id, "", [], "send.json is missing (or is a link, or over 1 MB).");
        }
        if (JsonNode.Parse(File.ReadAllText(send.FullName)) is not JsonObject o || o["version"]?.GetValue<int>() != 1)
        {
            return new(id, "", [], "send.json is not version 1 of the inbox.");
        }
        string source = o["source"]?.GetValue<string>()?.Trim() ?? "";
        if (!Plain(source) || source.Length > 60)
        {
            return new(id, source, [], "source must be a plain name of at most 60 characters.");
        }
        var list = o["kinds"] as JsonArray ?? [];
        if (list.Count is 0 or > MaxKinds)
        {
            return new(id, source, [], $"kinds must list 1 to {MaxKinds} models.");
        }
        var kinds = new List<(FileInfo File, string Model, string Label, string Category, float Min, float Max)>();
        foreach (var k in list)
        {
            string file = k?["file"]?.GetValue<string>() ?? "";
            var fi = new FileInfo(Path.Combine(dir, file));
            if (!Plain(file) || !file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || !fi.Exists || fi.LinkTarget != null)
            {
                return new(id, source, [], $"{file}: not a .glb file of the delivery.");
            }
            if (fi.Length > MaxFile || !IsGlb(fi.FullName))
            {
                return new(id, source, [], $"{file}: not a glTF binary (.glb) of at most {MaxFile >> 20} MB with everything in it (its textures embedded, no outside files).");
            }
            // Groundwork names a model by its file.
            string model = Path.GetFileNameWithoutExtension(file);
            if (k!["model"]?.GetValue<string>() is { } named && named != model)
            {
                return new(id, source, [], $"{file}: its model must be named as the file ({model}).");
            }
            if (kinds.Any(x => x.Model.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                return new(id, source, [], $"{model} is sent twice.");
            }
            string label = k["label"]?.GetValue<string>()?.Trim() is { Length: > 0 and <= 80 } l ? l : model;
            string cat = k["category"]?.GetValue<string>() is { } c && Categories.Contains(c) ? c : "Other";
            float min = k["scale"]?[0]?.GetValue<float>() ?? 1, max = k["scale"]?[1]?.GetValue<float>() ?? min;
            if (!float.IsFinite(min) || !float.IsFinite(max) || min <= 0 || max < min || max > 100)
            {
                return new(id, source, [], $"{model}: scale must be two sizes, 0 < min <= max <= 100.");
            }
            kinds.Add((fi, model, label, cat, min, max));
        }

        string models = GeneratedModels.Folder(map, "sent models");
        string into = Path.Combine(models, source);
        if (new DirectoryInfo(into) is { Exists: true, LinkTarget: not null })
        {
            return new(id, source, [], $"{into} is a link: sent models are written only into a folder of their own.");
        }
        // A name taken by another model (not this source's own, which is replaced) is refused.
        var elsewhere = ModelStore.ModelFiles(models).Where(f => !string.Equals(Path.GetDirectoryName(f), into, StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (kinds.FirstOrDefault(k => elsewhere.Contains(k.Model)) is { Model: { } taken })
        {
            return new(id, source, [], $"The map has another model named {taken}: rename it before sending.");
        }
        _ = Directory.CreateDirectory(into);
        GeneratedModels.NoLinks(into, kinds.Select(k => k.Model));
        foreach (var k in kinds)
        {
            string to = Path.Combine(into, k.Model + ".glb"), temp = $"{to}.{Guid.NewGuid():N}.tmp";
            try
            {
                k.File.CopyTo(temp);
                File.Move(temp, to, overwrite: true);
            }
            finally
            {
                File.Delete(temp);
            }
        }
        Catalog(map, kinds.Select(k => (k.Model, k.Label, k.Category, k.Min, k.Max)));
        return new(id, source, [.. kinds.Select(k => k.Model)], null);
    }

    /// <summary>
    /// Whether a file is a glTF binary that needs nothing beside it: its JSON names no outside file (an image or buffer
    /// uri that is not data:), which would be missing (or read from elsewhere on this computer) once it is copied.
    /// </summary>
    internal static bool IsGlb(string file)
    {
        using var f = File.OpenRead(file);
        Span<byte> head = stackalloc byte[20];
        if (f.Read(head) != 20 || !head[..4].SequenceEqual("glTF"u8) || BitConverter.ToUInt32(head[16..]) != 0x4E4F534A)
        {
            return false;
        }
        uint length = BitConverter.ToUInt32(head[12..]);
        if (length > 64 << 20)
        {
            return false;
        }
        byte[] json = new byte[length];
        f.ReadExactly(json);
        if (JsonNode.Parse(json) is not JsonObject o)
        {
            return false;
        }
        return ((string[])["images", "buffers"]).All(k => (o[k] as JsonArray ?? []).All(e => e?["uri"] is not { } uri || uri.GetValue<string>().StartsWith("data:", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The kinds into the map's catalog, each replacing the entry of its model (its bed and icon kept). A map with no
    /// catalog of its own gets one (catalog.json in its folder, named in map.json).
    /// </summary>
    private static void Catalog(MapProject map, IEnumerable<(string Model, string Label, string Category, float Min, float Max)> kinds)
    {
        if (map.Catalog is not { } file)
        {
            map.UseOwnCatalog();
            file = map.Catalog!;
        }
        var j = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o ? o : [];
        var list = j["kinds"] as JsonArray ?? [];
        j["kinds"] = list;
        foreach (var (model, label, category, min, max) in kinds)
        {
            var e = list.OfType<JsonObject>().FirstOrDefault(e => e["model"]?.GetValue<string>() == model);
            if (e == null)
            {
                e = new JsonObject { ["model"] = model };
                list.Add(e);
            }
            e["label"] = label;
            e["category"] = category;
            e["scale"] = new JsonArray(min, max);
        }
        SafeFile.WriteAllText(file, j.ToJsonString(new() { WriteIndented = true }));
    }
}
