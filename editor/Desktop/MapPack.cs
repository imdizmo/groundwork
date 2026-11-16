using System.IO.Compression;
using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// A map as one .zip to give to someone: its folder (as saved), and the models its objects use with the files they
/// read (a .gltf's .bin and pictures), in a models folder beside it. Nothing of this computer goes in: map.json loses
/// its models path and its Unreal project.
/// </summary>
internal static class MapPack
{
    /// <summary>A .gltf's lists of things with a uri.</summary>
    private static readonly string[] Lists = ["buffers", "images"];

    /// <summary>What was packed: how many models and their bytes, and what was left out (and why).</summary>
    internal sealed record Result(int Models, long ModelBytes, List<string> Left);

    /// <summary>The models the map's objects use: each one's files (path, path in the package's models folder), and the names not found.</summary>
    internal static (List<(string File, string Entry)> Files, int Models, List<string> Left) Used(MapProject m)
    {
        string models = Path.GetFullPath(m.Models ?? Path.Combine(m.Folder, "models"));
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string f in ModelStore.ModelFiles(models).Order(StringComparer.Ordinal))
        {
            byName.TryAdd(Path.GetFileNameWithoutExtension(f), f);
        }
        var names = ModelStore.ReadPlacements(m.ObjectsFile).Select(p => p.Name.Split('@')[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        List<(string, string)> files = [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<string> left = [];
        int found = 0;
        string? Entry(string file) =>
            Path.GetFullPath(file) is var full && full.StartsWith(models + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full) && new FileInfo(full).LinkTarget == null
                ? "models/" + Path.GetRelativePath(models, full).Replace('\\', '/')
                : null;
        void Add(string file, string entry)
        {
            if (seen.Add(entry))
            {
                files.Add((Path.GetFullPath(file), entry));
            }
        }
        foreach (string name in names)
        {
            if (!byName.TryGetValue(name, out string? file) || Entry(file) is not { } entry)
            {
                left.Add($"{name}: no glTF model of that name in the models folder");
                continue;
            }
            found++;
            Add(file, entry);
            if (!file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // a .gltf reads its buffers and pictures from beside it
            JsonObject? j = null;
            try
            {
                j = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
            }
            var uris = Lists.SelectMany(k => (j?[k] as JsonArray ?? []).Select(n => (n as JsonObject)?["uri"]?.GetValue<string>()));
            foreach (string uri in uris.OfType<string>().Where(u => !u.StartsWith("data:", StringComparison.Ordinal)))
            {
                string path = Path.Combine(Path.GetDirectoryName(file)!, Uri.UnescapeDataString(uri));
                if (Entry(path) is { } e)
                {
                    Add(path, e);
                }
                else
                {
                    left.Add($"{name}: {uri} (missing, or outside the models folder)");
                }
            }
        }
        // the catalog the map uses when it has none of its own
        if (m.Catalog == null && Entry(Path.Combine(models, "catalog.json")) is { } catalog)
        {
            Add(Path.Combine(models, "catalog.json"), catalog);
        }
        return (files, found, left);
    }

    /// <summary>Writes the package (a temporary file first, so a failed pack leaves nothing half written).</summary>
    /// <param name="m">The map, as saved.</param>
    /// <param name="zip">The .zip written.</param>
    /// <param name="withModels">The models go in; without them, only the catalog does.</param>
    internal static Result Write(MapProject m, string zip, bool withModels)
    {
        string folder = Path.GetFullPath(m.Folder), top = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        string ownModels = Path.GetFullPath(m.Models ?? Path.Combine(folder, "models"));
        var (used, count, left) = Used(m);
        if (!withModels)
        {
            used = [.. used.Where(u => u.Entry == "models/catalog.json")];
            (count, left) = (0, []);
        }
        string temp = $"{zip}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var z = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (string f in MapFiles(folder, ownModels))
                {
                    string rel = Path.GetRelativePath(folder, f).Replace('\\', '/');
                    if (Path.GetFullPath(f) != Path.GetFullPath(zip))
                    {
                        z.CreateEntryFromFile(f, $"{top}/{rel}", CompressionLevel.Optimal);
                    }
                }
                var j = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, MapProject.FileName)))!.AsObject();
                j.Remove("models");
                if (j["unreal"] is JsonObject u)
                {
                    u.Remove("project");
                    u.Remove("apply");
                }
                using (var w = new StreamWriter(z.CreateEntry($"{top}/{MapProject.FileName}").Open()))
                {
                    w.Write(j.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                // a top view, for the team's library to show (its pixels BGRA, as the start page draws them)
                if (Picture(folder) is { } png)
                {
                    using var pic = z.CreateEntry($"{top}/picture.png", CompressionLevel.NoCompression).Open();
                    pic.Write(png);
                }
                foreach (var (file, entry) in used)
                {
                    z.CreateEntryFromFile(file, $"{top}/{entry}", CompressionLevel.Optimal);
                }
            }
            File.Move(temp, zip, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
        return new(count, used.Where(u => u.Entry != "models/catalog.json").Sum(u => new FileInfo(u.File).Length), left);
    }

    /// <summary>The map's ground as the start page's picture, a PNG (null when its heights cannot be read).</summary>
    internal static byte[]? Picture(string folder)
    {
        try
        {
            if (StartPage.MapFacts.Of(folder).Thumb is not { } t)
            {
                return null;
            }
            using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(t.Width, t.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque));
            System.Runtime.InteropServices.Marshal.Copy(t.Pixels, 0, bitmap.GetPixels(), t.Pixels.Length);
            using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Log.CaughtBrief(e, "drawing the map's picture for its pack");
            return null;
        }
    }

    /// <summary>The map folder's files: no links, no models folder inside it, no map.json (written changed) or its old copies.</summary>
    private static IEnumerable<string> MapFiles(string folder, string models)
    {
        foreach (var e in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (e.LinkTarget != null || e.FullName == models || e.Name.StartsWith(MapProject.FileName, StringComparison.Ordinal) || e.Name.EndsWith(".tmp", StringComparison.Ordinal) || e.Name.EndsWith(".bak", StringComparison.Ordinal))
            {
                continue;
            }
            if (e is DirectoryInfo d)
            {
                foreach (string f in MapFiles(d.FullName, models))
                {
                    yield return f;
                }
            }
            else
            {
                yield return e.FullName;
            }
        }
    }
}
