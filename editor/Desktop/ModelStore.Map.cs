using System.Numerics;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

// The map's models: its models folder in the exporter's layout (Files), glTF files anywhere under it (Gltf), or both;
// and the objects placed on the map.
public sealed partial class ModelStore
{
    /// <summary>The open map's model folder (map.json's models; none: its own folder's models/).</summary>
    internal static string MapModels => MapProject.Current is { } m ? m.Models ?? Path.Combine(m.Folder, "models") : "";

    /// <summary>The objects of a map's objects file (name, x, y, z, Euler x, y, z, scale, and optionally an object of data).</summary>
    internal static List<Placement> ReadPlacements(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }
        List<Placement> placed = [];
        foreach (JsonNode? p in JsonNode.Parse(File.ReadAllText(file))!.AsArray())
        {
            float F(int i) => p![i]!.GetValue<float>();
            var data = p!.AsArray().Count > 8 && p[8] is JsonObject d ? d.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "") : null;
            placed.Add(new(p[0]!.GetValue<string>(), new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7), data));
        }
        return placed;
    }

    /// <summary>An object of a map's objects file: asset, place, turn (Euler degrees), scale, and the data extensions keep on it.</summary>
    internal sealed record Placement(string Name, Vector3 Position, Vector3 Rotation, float Scale, IReadOnlyDictionary<string, string>? Data);

    /// <summary>Every model's name by its hash (non-uniformly scaled copies, name@x_y_z, too).</summary>
    private static (string Folder, Dictionary<int, string> Names) _modelNames = ("", []);

    // ponytail: read again when the model folder changes (another map), kept otherwise
    private static Dictionary<int, string> ModelNames()
    {
        string folder = MapModels;
        if (_modelNames.Folder != folder)
        {
            string pieces = Path.Combine(folder, "pieces");
            var names = Directory.Exists(pieces) ? Directory.GetFiles(pieces, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList() : [];
            names.AddRange(Gltf.Files(folder).Select(Path.GetFileNameWithoutExtension).OfType<string>());
            _modelNames = (folder, names.Distinct().ToDictionary(Groundwork.App.AssetIds.Of));
        }
        return _modelNames.Names;
    }

    /// <summary>A map model's name, or null for any other prefab.</summary>
    internal static string? ModelName(int prefab) => ModelNames().GetValueOrDefault(prefab);

    /// <summary>The kinds Place offers on the map: every model but the scaled copies.</summary>
    internal static IEnumerable<string> ModelKinds() => ModelNames().Values.Where(n => !n.Contains('@'));

    /// <summary>The exporter's files first, then the glTF files; a glTF id has a '#'.</summary>
    private sealed class MapSource(Files? files, Gltf gltf) : ISource
    {
        private static bool IsGltf(string id) => id.Contains('#');

        public string? ObjectName(int prefab) => ModelName(prefab);

        public Model? Model(string name) => files?.Model(name) ?? gltf.Model(name);

        public MeshData? Mesh(string id) => IsGltf(id) ? gltf.Mesh(id) : files?.Mesh(id);

        public MaterialData Material(string id) => IsGltf(id) || files == null ? gltf.Material(id) : files.Material(id);

        public ImageData? Texture(string id) => IsGltf(id) ? gltf.Texture(id) : files?.Texture(id);
    }
}
