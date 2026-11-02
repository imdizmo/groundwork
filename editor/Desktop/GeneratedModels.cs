using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// The models the generator makes for a map (its floating islands, its cliff pieces): .glb files of the map's own ground
/// and rock written into its models folder, and kinds of them in its catalog.
/// </summary>
internal static class GeneratedModels
{
    /// <summary>
    /// The folder they go in. A map from elsewhere names its own models folder: they go into the map's own one (made), or
    /// into a shared one only when it is real (there, not a link, models already in it).
    /// </summary>
    public static string Folder(MapProject m, string what)
    {
        string folder = m.Models ?? Path.Combine(m.Folder, "models");
        if (m.Models == null)
        {
            Directory.CreateDirectory(folder);
        }
        if (!Directory.Exists(folder) || new DirectoryInfo(folder).LinkTarget != null || (m.Models != null && ModelStore.ModelFiles(folder).Count == 0))
        {
            throw new InvalidDataException($"{folder}: not a models folder {what} can be written into");
        }
        return folder;
    }

    /// <summary>Refuses to write through a link planted where one of the models goes: each is written only as a file of its own.</summary>
    public static void NoLinks(string folder, IEnumerable<string> names)
    {
        if (names.Select(n => Path.Combine(folder, n + ".glb")).FirstOrDefault(f => new FileInfo(f).LinkTarget != null) is { } link)
        {
            throw new InvalidDataException($"{link} is a link: a generated model is written only as a file of its own");
        }
    }

    /// <summary>The map's first layer named with one of the words, or null.</summary>
    public static MapProject.Layer? Layer(MapProject m, params string[] words) =>
        m.Layers.FirstOrDefault(l => words.Any(w => l.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));

    /// <summary>How a layer looks on a model: its picture (when the map has it), its colour, the metres it repeats over.</summary>
    public static (string? Png, Vector3 Colour, float Size) Look(MapProject m, MapProject.Layer l) =>
        (l.Texture is { } t && File.Exists(Path.Combine(m.Folder, t)) ? Path.Combine(m.Folder, t) : null, l.Colour, l.Size);

    /// <summary>Puts the kinds in the map's catalog (those not there yet), each under its own category.</summary>
    public static void AddToCatalog(MapProject m, IEnumerable<MapTools.Kind> kinds)
    {
        if (m.Catalog is not { } cat || !File.Exists(cat))
        {
            return;
        }
        var j = JsonNode.Parse(File.ReadAllText(cat))!.AsObject();
        var list = j["kinds"] as JsonArray ?? [];
        foreach (var k in kinds.Where(k => !list.Any(e => e?["model"]?.GetValue<string>() == k.Model)))
        {
            list.Add(new JsonObject { ["model"] = k.Model, ["label"] = k.Label, ["category"] = k.Category, ["scale"] = new JsonArray(1, 1) });
        }
        j["kinds"] = list;
        File.WriteAllText(cat, j.ToJsonString(new() { WriteIndented = true }));
    }

    /// <summary>
    /// A model as a .glb: its parts (corners: position, normal, uv; triangles), each named and with its look's picture
    /// (embedded) or its colour.
    /// </summary>
    public static void WriteGlb(string file, IReadOnlyList<(List<float> V, List<int> I)> parts, IReadOnlyList<(string? Png, Vector3 Colour)> looks, IReadOnlyList<string> names)
    {
        var bin = new MemoryStream();
        var views = new JsonArray();
        var accessors = new JsonArray();
        int View(byte[] bytes, int? target)
        {
            while (bin.Length % 4 != 0)
            {
                bin.WriteByte(0);
            }
            var view = new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = bytes.Length };
            if (target is { } t)
            {
                view["target"] = t;
            }
            bin.Write(bytes);
            views.Add(view);
            return views.Count - 1;
        }
        int Accessor(float[] data, int offset, int width, string type, bool bounds)
        {
            int n = data.Length / 8;
            float[] flat = new float[n * width];
            for (int i = 0; i < n; i++)
            {
                Array.Copy(data, (i * 8) + offset, flat, i * width, width);
            }
            byte[] bytes = new byte[flat.Length * 4];
            Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
            var acc = new JsonObject { ["bufferView"] = View(bytes, 34962), ["componentType"] = 5126, ["count"] = n, ["type"] = type };
            if (bounds)
            {
                acc["min"] = new JsonArray([.. Enumerable.Range(0, width).Select(c => (JsonNode)Enumerable.Range(0, n).Min(i => flat[(i * width) + c]))]);
                acc["max"] = new JsonArray([.. Enumerable.Range(0, width).Select(c => (JsonNode)Enumerable.Range(0, n).Max(i => flat[(i * width) + c]))]);
            }
            accessors.Add(acc);
            return accessors.Count - 1;
        }
        var materials = new JsonArray();
        var textures = new JsonArray();
        var images = new JsonArray();
        var prims = new JsonArray();
        foreach (var (part, look, name) in parts.Zip(looks, names))
        {
            float[] v = [.. part.V];
            byte[] idx = new byte[part.I.Count * 4];
            Buffer.BlockCopy(part.I.ToArray(), 0, idx, 0, idx.Length);
            int indices = accessors.Count;
            accessors.Add(new JsonObject { ["bufferView"] = View(idx, 34963), ["componentType"] = 5125, ["count"] = part.I.Count, ["type"] = "SCALAR" });
            var pbr = new JsonObject { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
            if (look.Png != null)
            {
                images.Add(new JsonObject { ["bufferView"] = View(System.IO.File.ReadAllBytes(look.Png), null), ["mimeType"] = "image/png" });
                textures.Add(new JsonObject { ["source"] = images.Count - 1 });
                pbr["baseColorTexture"] = new JsonObject { ["index"] = textures.Count - 1 };
            }
            else
            {
                pbr["baseColorFactor"] = new JsonArray(look.Colour.X, look.Colour.Y, look.Colour.Z, 1);
            }
            materials.Add(new JsonObject { ["name"] = name, ["pbrMetallicRoughness"] = pbr });
            prims.Add(new JsonObject
            {
                ["attributes"] = new JsonObject { ["POSITION"] = Accessor(v, 0, 3, "VEC3", true), ["NORMAL"] = Accessor(v, 3, 3, "VEC3", false), ["TEXCOORD_0"] = Accessor(v, 6, 2, "VEC2", false) },
                ["indices"] = indices,
                ["material"] = materials.Count - 1,
            });
        }
        while (bin.Length % 4 != 0)
        {
            bin.WriteByte(0);
        }
        var gltf = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Groundwork" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0, ["name"] = Path.GetFileNameWithoutExtension(file) }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = prims }),
            ["materials"] = materials,
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length }),
            ["bufferViews"] = views,
            ["accessors"] = accessors,
        };
        if (images.Count > 0)
        {
            (gltf["images"], gltf["textures"]) = (images, textures);
        }
        byte[] json = Encoding.UTF8.GetBytes(gltf.ToJsonString());
        int jsonLength = (json.Length + 3) & ~3;
        using var w = new BinaryWriter(System.IO.File.Create(file));
        w.Write(0x46546C67u);
        w.Write(2u);
        w.Write((uint)(12 + 8 + jsonLength + 8 + bin.Length));
        w.Write((uint)jsonLength);
        w.Write(0x4E4F534Au);
        w.Write(json);
        w.Write(Enumerable.Repeat((byte)' ', jsonLength - json.Length).ToArray());
        w.Write((uint)bin.Length);
        w.Write(0x004E4942u);
        w.Write(bin.ToArray());
    }
}
