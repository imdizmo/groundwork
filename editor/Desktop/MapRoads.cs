using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The map's kept roads: each a line made with Path and how it was made, so it can be picked, changed and made again,
/// or taken away. With each, the ground as it was before the road (heights and paint of the points it changed), so
/// making it again puts that back first. Lines and settings in the roads file (world metres), the ground before in a
/// file beside it. A bridge stands where a road crosses a river, when the road has a bridge model.
/// </summary>
public static class MapRoads
{
    /// <summary>The data key the road's own objects (edge objects, bridges) carry: the road's name.</summary>
    public const string DataKey = "road";

    /// <summary>A grid point's ground before a road: its height and its paint (one value a layer).</summary>
    public readonly record struct Point(float Height, float[] Paint);

    /// <summary>
    /// A kept road. Immutable: a change makes a new one, so an undo step keeps the list as it was by reference.
    /// </summary>
    /// <param name="Name">Its name, unique (its objects carry it).</param>
    /// <param name="Points">The line, world x and z in metres.</param>
    /// <param name="Width">The road's width, metres.</param>
    /// <param name="Shoulder">How far each side it blends into the ground, metres.</param>
    /// <param name="Even">Straight slopes between the points (a ramp), else the ground's rise and fall smoothed.</param>
    /// <param name="Smoothing">Over how many metres the ground is smoothed.</param>
    /// <param name="Layer">The layer painted on it by name, or null.</param>
    /// <param name="Edge">The model along its edges, or null.</param>
    /// <param name="Spacing">Metres between edge objects.</param>
    /// <param name="Offset">How far past the edge they stand.</param>
    /// <param name="BothSides">Edge objects on both sides (else the right one, walking from the first point).</param>
    /// <param name="Bridge">The model put where it crosses a river, or null.</param>
    public sealed record Road(string Name, IReadOnlyList<Vector2> Points, float Width, float Shoulder, bool Even, float Smoothing, string? Layer, string? Edge, float Spacing, float Offset, bool BothSides, string? Bridge)
    {
        /// <summary>The ground before the road, by grid point; null when not known (another grid: the map grew).</summary>
        public IReadOnlyDictionary<int, Point>? Before { get; init; }

        /// <summary>The grid the points are numbered on: its width and height and its first zone.</summary>
        public (int W, int H, int X0, int Z0) Grid { get; init; }

        /// <summary>Its length along the line, metres.</summary>
        public float Length => Points.Zip(Points.Skip(1), Vector2.Distance).Sum();
    }

    /// <summary>The roads.</summary>
    public static List<Road> All { get; } = [];

    /// <summary>Called when they change.</summary>
    public static event Action? Changed;

    /// <summary>A name no road has yet.</summary>
    public static string NewName()
    {
        int n = 1;
        while (All.Any(r => r.Name == $"Road {n}"))
        {
            n++;
        }
        return $"Road {n}";
    }

    /// <summary>The roads now, to put back later (an undo step).</summary>
    public static Road[] Snapshot() => [.. All];

    /// <summary>Back to a list of roads.</summary>
    public static void Restore(IEnumerable<Road> roads)
    {
        All.Clear();
        All.AddRange(roads);
        Touch();
    }

    /// <summary>Tells the tool, the picture and the save that they changed.</summary>
    public static void Touch()
    {
        Changed?.Invoke();
        ModExtensions.Redraw();
    }

    /// <summary>The lines and settings as the roads file holds them (the save's "changed" check too).</summary>
    public static string Text()
    {
        static float R(float v) => MathF.Round(v * 100f) / 100f;
        return new JsonObject
        {
            ["note"] = "World metres (x east, z north). The ground before each road is in roads.bin beside this file.",
            ["roads"] = new JsonArray([.. All.Select(r => (JsonNode)new JsonObject
            {
                ["name"] = r.Name,
                ["points"] = new JsonArray([.. r.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y)))]),
                ["width"] = r.Width, ["shoulder"] = r.Shoulder, ["even"] = r.Even, ["smoothing"] = r.Smoothing,
                ["layer"] = r.Layer, ["edge"] = r.Edge, ["spacing"] = r.Spacing, ["offset"] = r.Offset, ["bothSides"] = r.BothSides,
                ["bridge"] = r.Bridge,
            })]),
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The ground-before file beside the roads file.</summary>
    private static string BeforeFile(string file) => Path.ChangeExtension(file, ".bin");

    /// <summary>Writes the roads file and the ground before them (nothing when there are no roads and no file yet).</summary>
    public static void Save(string file)
    {
        if (All.Count == 0 && !File.Exists(file))
        {
            return;
        }
        File.WriteAllText(file, Text());
        using var w = new BinaryWriter(File.Create(BeforeFile(file)));
        w.Write("GWROADS1");
        w.Write(All.Count(r => r.Before != null));
        foreach (var r in All.Where(r => r.Before != null))
        {
            w.Write(r.Name);
            w.Write(r.Grid.W);
            w.Write(r.Grid.H);
            w.Write(r.Grid.X0);
            w.Write(r.Grid.Z0);
            int c = r.Before!.Values.FirstOrDefault().Paint?.Length ?? 0;
            w.Write(c);
            w.Write(r.Before.Count);
            foreach (var (p, (h, paint)) in r.Before)
            {
                w.Write(p);
                w.Write(h);
                for (int k = 0; k < c; k++)
                {
                    w.Write(k < paint.Length ? paint[k] : 0f);
                }
            }
        }
    }

    /// <summary>Reads the roads file and the ground before them (none: no roads; a damaged ground-before file: roads without it).</summary>
    public static void Load(string file)
    {
        All.Clear();
        var o = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null;
        static float F(JsonNode? n, float d) => n is JsonValue v && v.TryGetValue(out float f) && float.IsFinite(f) ? f : d;
        static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) && s.Length > 0 ? s : null;
        var befores = new Dictionary<string, (IReadOnlyDictionary<int, Point>, (int, int, int, int))>();
        try
        {
            if (o != null && File.Exists(BeforeFile(file)))
            {
                using var r = new BinaryReader(File.OpenRead(BeforeFile(file)));
                if (r.ReadString() == "GWROADS1")
                {
                    for (int n = r.ReadInt32(); n > 0; n--)
                    {
                        string name = r.ReadString();
                        var grid = (r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
                        int c = r.ReadInt32(), count = r.ReadInt32();
                        if (c is < 0 or > 64 || count is < 0 or > 64_000_000)
                        {
                            break;
                        }
                        var points = new Dictionary<int, Point>(count);
                        for (int i = 0; i < count; i++)
                        {
                            int p = r.ReadInt32();
                            float h = r.ReadSingle();
                            float[] paint = new float[c];
                            for (int k = 0; k < c; k++)
                            {
                                paint[k] = r.ReadSingle();
                            }
                            points[p] = new Point(h, paint);
                        }
                        befores[name] = (points, grid);
                    }
                }
            }
        }
        catch (Exception e) when (e is EndOfStreamException or IOException)
        {
            // ponytail: a damaged ground-before file costs the roads their undo of the old ground, not the roads
        }
        foreach (var n in o?["roads"] as JsonArray ?? [])
        {
            string name = S(n?["name"]) ?? NewName();
            var points = (n?["points"] as JsonArray ?? []).OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vector2(F(p[0], 0), F(p[1], 0))).ToList();
            if (points.Count < 2 || All.Any(r => r.Name == name))
            {
                continue;
            }
            var road = new Road(name, points, F(n?["width"], 3), F(n?["shoulder"], 3), n?["even"] is JsonValue e && e.TryGetValue(out bool even) && even, F(n?["smoothing"], 15),
                S(n?["layer"]), S(n?["edge"]), F(n?["spacing"], 3), F(n?["offset"], 0.5f), n?["bothSides"] is not JsonValue b || !b.TryGetValue(out bool both) || both, S(n?["bridge"]));
            if (befores.TryGetValue(name, out var before))
            {
                road = road with { Before = before.Item1, Grid = before.Item2 };
            }
            All.Add(road);
        }
        Touch();
    }

    /// <summary>
    /// Where a line crosses the rivers: the point, the line's direction there (degrees: 0 north, 90 east, as objects
    /// turn), the river's half-width, and the river's stretch crossed (world x, z).
    /// </summary>
    public static List<(Vector2 At, float Yaw, float HalfWidth, Vector2 A, Vector2 B)> Crossings(IReadOnlyList<Vector2> line)
    {
        var found = new List<(Vector2, float, float, Vector2, Vector2)>();
        foreach (var river in MapWater.Rivers)
        {
            for (int j = 0; j + 1 < river.Points.Count; j++)
            {
                // the water file is Unreal metres: the editor's z = -Y
                var (ra, rb) = (river.Points[j], river.Points[j + 1]);
                Vector2 a = new(ra.X, -ra.Y), b = new(rb.X, -rb.Y);
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    if (Cross(line[i], line[i + 1], a, b) is float t)
                    {
                        var d = line[i + 1] - line[i];
                        float yaw = MathF.Atan2(d.X, d.Y) * 180 / MathF.PI;
                        found.Add((line[i] + (d * t), yaw, MathF.Max(ra.HalfWidth, rb.HalfWidth), a, b));
                    }
                }
            }
        }
        return found;
    }

    /// <summary>Where along p→q (0..1) it crosses a→b, or null.</summary>
    private static float? Cross(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
    {
        Vector2 r = q - p, s = b - a;
        float den = (r.X * s.Y) - (r.Y * s.X);
        if (MathF.Abs(den) < 1e-6f)
        {
            return null;
        }
        Vector2 ap = a - p;
        float t = ((ap.X * s.Y) - (ap.Y * s.X)) / den, u = ((ap.X * r.Y) - (ap.Y * r.X)) / den;
        return t is >= 0 and <= 1 && u is >= 0 and <= 1 ? t : null;
    }

    /// <summary>How far a point is from a segment.</summary>
    public static float Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.LengthSquared() < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + (ab * t));
    }
}
