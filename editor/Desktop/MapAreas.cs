using System.Numerics;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// The map's areas: boxes on the ground that become volumes in Unreal (triggers, blocking walls, sound, post-process,
/// kill and navigation zones), kept in the map's areas file. Positions in Unreal metres, as the water's.
/// </summary>
public static class MapAreas
{
    /// <summary>A kind of area: its name, its Unreal volume class, a tag its actors get, its colour (0..1).</summary>
    public sealed record Kind(string Id, string Label, string Unreal, string? Tag, Vector3 Colour);

    /// <summary>
    /// An area: a box of a kind, its middle (Unreal X, Y), its width (along its turn) and depth, its bottom and top
    /// heights, its turn (degrees, Unreal yaw).
    /// </summary>
    public sealed class Area
    {
        /// <summary>Its name (unique: its Unreal actor is tagged with it).</summary>
        public string Name { get; set; } = "Area";
        /// <summary>Its kind's id.</summary>
        public string Kind { get; set; } = "trigger";
        /// <summary>Its middle, Unreal X in metres.</summary>
        public float X { get; set; }
        /// <summary>Its middle, Unreal Y in metres.</summary>
        public float Y { get; set; }
        /// <summary>Its width, metres (Unreal X before the turn).</summary>
        public float Width { get; set; } = 10;
        /// <summary>Its depth, metres (Unreal Y before the turn).</summary>
        public float Depth { get; set; } = 10;
        /// <summary>Its bottom, metres.</summary>
        public float Bottom { get; set; }
        /// <summary>Its top, metres.</summary>
        public float Top { get; set; } = 5;
        /// <summary>Its turn, degrees (Unreal yaw).</summary>
        public float Yaw { get; set; }
    }

    /// <summary>The kinds when the catalog has no "areas".</summary>
    public static IReadOnlyList<Kind> Defaults { get; } =
    [
        new("trigger", "Trigger", "TriggerVolume", null, new(1f, 0.75f, 0.1f)),
        new("blocking", "Blocking", "BlockingVolume", null, new(0.9f, 0.2f, 0.2f)),
        new("audio", "Sound", "AudioVolume", null, new(0.3f, 0.6f, 1f)),
        new("postprocess", "Post process", "PostProcessVolume", null, new(0.7f, 0.4f, 1f)),
        new("kill", "Kill zone", "KillZVolume", null, new(0.2f, 0.2f, 0.2f)),
        new("nav", "Navigation", "NavModifierVolume", null, new(0.2f, 0.85f, 0.4f)),
    ];

    /// <summary>The areas.</summary>
    public static List<Area> All { get; } = [];
    /// <summary>Changed since loaded or saved.</summary>
    public static bool Dirty { get; set; }
    /// <summary>Called when they change.</summary>
    public static event Action? Changed;

    /// <summary>The catalog's area kinds ("areas": {id: {label, unreal, tag, color}}), else the defaults.</summary>
    public static IReadOnlyList<Kind> Kinds(JsonObject? catalog) =>
        catalog?["areas"] is JsonObject a && a.Count > 0
            ? [.. a.Select(kv => new Kind(kv.Key, kv.Value?["label"]?.GetValue<string>() ?? kv.Key, kv.Value?["unreal"]?.GetValue<string>() ?? "TriggerVolume", kv.Value?["tag"]?.GetValue<string>(),
                kv.Value?["color"] is JsonArray c && c.Count >= 3 ? new Vector3(c[0]!.GetValue<float>(), c[1]!.GetValue<float>(), c[2]!.GetValue<float>()) : new Vector3(1, 0.75f, 0.1f)))]
            : Defaults;

    /// <summary>Reads the map's areas file (none: no areas).</summary>
    public static void Load(string file)
    {
        Read(File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null);
        Dirty = false;
        Changed?.Invoke();
    }

    private static void Read(JsonObject? o)
    {
        All.Clear();
        foreach (var a in o?["areas"]?.AsArray() ?? [])
        {
            float F(string k, float d) => a![k]?.GetValue<float>() ?? d;
            All.Add(new Area { Name = a!["name"]!.GetValue<string>(), Kind = a["kind"]?.GetValue<string>() ?? "trigger", X = F("x", 0), Y = F("y", 0), Width = F("width", 10), Depth = F("depth", 10), Bottom = F("bottom", 0), Top = F("top", 5), Yaw = F("yaw", 0) });
        }
    }

    /// <summary>Writes the map's areas file.</summary>
    public static void Save(string file)
    {
        File.WriteAllText(file, Snapshot());
        Dirty = false;
    }

    /// <summary>The areas as the file holds them (an undo step keeps two of these).</summary>
    public static string Snapshot()
    {
        static float R(float v) => MathF.Round(v * 1000f) / 1000f;
        return new JsonObject
        {
            ["note"] = "Unreal metres: x, y the middle; width along the turn (yaw, degrees), depth across; bottom and top heights",
            ["areas"] = new JsonArray([.. All.Select(a => (JsonNode)new JsonObject
            {
                ["name"] = a.Name, ["kind"] = a.Kind, ["x"] = R(a.X), ["y"] = R(a.Y), ["width"] = R(a.Width), ["depth"] = R(a.Depth),
                ["bottom"] = R(a.Bottom), ["top"] = R(a.Top), ["yaw"] = R(a.Yaw),
            })]),
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Back to a snapshot (undo, redo).</summary>
    public static void Restore(string snapshot)
    {
        Read(JsonNode.Parse(snapshot) as JsonObject);
        Touch();
    }

    /// <summary>Marks them changed and draws them again.</summary>
    public static void Touch()
    {
        Dirty = true;
        Changed?.Invoke();
    }

    /// <summary>A name no area has yet: the kind's label and the first free number.</summary>
    public static string NewName(string label)
    {
        int n = 1;
        while (All.Any(a => a.Name == $"{label} {n}"))
        {
            n++;
        }
        return $"{label} {n}";
    }

    /// <summary>The box's four ground corners (Unreal X, Y), round from its first.</summary>
    public static Vector2[] Corners(Area a)
    {
        float c = MathF.Cos(a.Yaw * MathF.PI / 180), s = MathF.Sin(a.Yaw * MathF.PI / 180), hw = a.Width / 2, hd = a.Depth / 2;
        Vector2 P(float u, float v) => new(a.X + (u * c) - (v * s), a.Y + (u * s) + (v * c));
        return [P(-hw, -hd), P(hw, -hd), P(hw, hd), P(-hw, hd)];
    }

    /// <summary>Whether a point (Unreal X, Y) is inside the box, seen from above.</summary>
    public static bool Holds(Area a, Vector2 p)
    {
        float c = MathF.Cos(-a.Yaw * MathF.PI / 180), s = MathF.Sin(-a.Yaw * MathF.PI / 180), dx = p.X - a.X, dy = p.Y - a.Y;
        return MathF.Abs((dx * c) - (dy * s)) <= a.Width / 2 && MathF.Abs((dx * s) + (dy * c)) <= a.Depth / 2;
    }

    /// <summary>The areas as the bridge's /groundwork/areas takes them: each one's class, tags, middle and size in centimetres, turn.</summary>
    public static JsonObject ForUnreal(IReadOnlyList<Kind> kinds)
    {
        var list = new JsonArray();
        foreach (var a in All)
        {
            var k = kinds.FirstOrDefault(k => k.Id == a.Kind) ?? Defaults[0];
            list.Add(new JsonObject
            {
                ["name"] = a.Name,
                ["class"] = k.Unreal,
                ["tag"] = k.Tag,
                ["x"] = a.X * 100,
                ["y"] = a.Y * 100,
                ["z"] = (a.Bottom + a.Top) * 50,
                ["sx"] = a.Width * 100,
                ["sy"] = a.Depth * 100,
                ["sz"] = (a.Top - a.Bottom) * 100,
                ["yaw"] = a.Yaw,
            });
        }
        return new JsonObject { ["areas"] = list };
    }
}
