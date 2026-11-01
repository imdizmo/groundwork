using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// The map's rivers and lakes: its water file (map.json's files.water), in Unreal metres (X, Y; the editor's x = X,
/// z = -Y). A river: points (X, Y, half-width, depth below its surface, current m/s)
/// and its surface height at its first and last point (heights between follow the distance along it). A lake: an
/// outline (X, Y), its surface height and its depth. Drawn always (GlView.SetWater); the River tool
/// edits them, Save writes them, Live sends them to Unreal (the bridge makes its water bodies).
/// </summary>
public static class MapWater
{
    /// <summary>A river's point: Unreal metres, half-width and depth in metres, current in m/s.</summary>
    public sealed record Point(float X, float Y, float HalfWidth, float Depth, float Current);

    /// <summary>A river.</summary>
    public sealed class River
    {
        /// <summary>Its name (Unreal's water body is named after it).</summary>
        public string Name { get; set; } = "River";
        /// <summary>Its surface height at its first point, metres.</summary>
        public float Top { get; set; } = 0.35f;
        /// <summary>Its surface height at its last point, metres.</summary>
        public float End { get; set; } = 0.12f;
        /// <summary>Its points, from its source down.</summary>
        public List<Point> Points { get; } = [];
    }

    /// <summary>A lake.</summary>
    public sealed class Lake
    {
        /// <summary>Its name.</summary>
        public string Name { get; set; } = "Lake";
        /// <summary>Its surface height, metres.</summary>
        public float Level { get; set; } = 2f;
        /// <summary>How deep it is in the middle, metres.</summary>
        public float Depth { get; set; } = 3f;
        /// <summary>Its outline, Unreal metres.</summary>
        public List<Vector2> Outline { get; } = [];
    }

    /// <summary>The rivers.</summary>
    public static List<River> Rivers { get; } = [];
    /// <summary>The lakes.</summary>
    public static List<Lake> Lakes { get; } = [];
    /// <summary>Changed since loaded or saved.</summary>
    public static bool Dirty { get; set; }
    /// <summary>Called when they change (the view draws them again).</summary>
    public static event Action? Changed;

    /// <summary>Reads the map's water file (none: no rivers or lakes).</summary>
    public static void Load(string file)
    {
        if (System.IO.File.Exists(file))
        {
            Read(JsonNode.Parse(System.IO.File.ReadAllText(file)) as JsonObject);
        }
        else
        {
            Rivers.Clear();
            Lakes.Clear();
        }
        Dirty = false;
        Changed?.Invoke();
    }

    private static void Read(JsonObject? o)
    {
        Rivers.Clear();
        Lakes.Clear();
        if (o != null)
        {
            foreach (var r in o["rivers"]?.AsArray() ?? [])
            {
                var river = new River { Name = r!["name"]!.GetValue<string>(), Top = r["top"]!.GetValue<float>(), End = r["end"]!.GetValue<float>() };
                river.Points.AddRange(r["points"]!.AsArray().Select(p => new Point(F(p, 0), F(p, 1), F(p, 2), F(p, 3), F(p, 4))));
                Rivers.Add(river);
            }
            foreach (var l in o["lakes"]?.AsArray() ?? [])
            {
                var lake = new Lake { Name = l!["name"]!.GetValue<string>(), Level = l["level"]!.GetValue<float>(), Depth = l["depth"]!.GetValue<float>() };
                lake.Outline.AddRange(l["outline"]!.AsArray().Select(p => new Vector2(F(p, 0), F(p, 1))));
                Lakes.Add(lake);
            }
        }
    }

    private static float F(JsonNode? p, int i) => p![i]!.GetValue<float>();

    /// <summary>Writes the map's water file.</summary>
    public static void Save(string file)
    {
        System.IO.File.WriteAllText(file, Snapshot());
        Dirty = false;
    }

    /// <summary>The rivers and lakes as rivers.json holds them (an undo step keeps two of these).</summary>
    public static string Snapshot()
    {
        static float R(float v) => MathF.Round(v * 1000f) / 1000f;
        var o = new JsonObject
        {
            ["note"] = "Unreal metres; river points: X, Y, half-width, depth, current m/s; top/end: surface height at the first/last point",
            ["rivers"] = new JsonArray([.. Rivers.Select(r => new JsonObject
            {
                ["name"] = r.Name,
                ["top"] = R(r.Top),
                ["end"] = R(r.End),
                ["points"] = new JsonArray([.. r.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.HalfWidth), R(p.Depth), R(p.Current)))]),
            })]),
            ["lakes"] = new JsonArray([.. Lakes.Select(l => new JsonObject
            {
                ["name"] = l.Name,
                ["level"] = R(l.Level),
                ["depth"] = R(l.Depth),
                ["outline"] = new JsonArray([.. l.Outline.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y)))]),
            })]),
        };
        return o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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

    /// <summary>A river's surface height at each point (Top to End along its length).</summary>
    public static float[] Surfaces(River r)
    {
        float[] along = new float[r.Points.Count];
        for (int i = 1; i < r.Points.Count; i++)
        {
            along[i] = along[i - 1] + Vector2.Distance(new(r.Points[i].X, r.Points[i].Y), new(r.Points[i - 1].X, r.Points[i - 1].Y));
        }
        float total = Math.Max(along.LastOrDefault(), 1e-3f);
        return [.. along.Select(a => r.Top + ((r.End - r.Top) * a / total))];
    }

    /// <summary>The editor point of an Unreal point (metres; its height is the editor's).</summary>
    public static Vector3 Editor(float x, float y, float height) => new(x, height, -y);

    /// <summary>The water's shapes: each river a strip at its surface, each lake its outline filled at its level.</summary>
    public static List<Shape> Shapes()
    {
        var water = new Vector4(0.22f, 0.55f, 0.62f, 0.55f);
        var shapes = new List<Shape>();
        foreach (var r in Rivers.Where(r => r.Points.Count > 1))
        {
            float[] h = Surfaces(r);
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < r.Points.Count; i++)
            {
                var a = new Vector2(r.Points[Math.Max(0, i - 1)].X, r.Points[Math.Max(0, i - 1)].Y);
                var b = new Vector2(r.Points[Math.Min(r.Points.Count - 1, i + 1)].X, r.Points[Math.Min(r.Points.Count - 1, i + 1)].Y);
                var d = Vector2.Normalize(b - a);
                var side = new Vector2(-d.Y, d.X) * r.Points[i].HalfWidth;
                var c = new Vector2(r.Points[i].X, r.Points[i].Y);
                v.Add(Editor(c.X - side.X, c.Y - side.Y, h[i] + 0.03f));
                v.Add(Editor(c.X + side.X, c.Y + side.Y, h[i] + 0.03f));
                if (i > 0)
                {
                    int k = v.Count - 4;
                    t.AddRange([k, k + 1, k + 2, k + 1, k + 3, k + 2]);
                }
            }
            shapes.Add(new Shape([.. v], [.. t], water) { BothSides = true });
        }
        foreach (var l in Lakes.Where(l => l.Outline.Count > 2))
        {
            shapes.Add(new Shape([.. l.Outline.Select(p => Editor(p.X, p.Y, l.Level + 0.03f))], Triangulate(l.Outline), water) { BothSides = true });
        }
        return shapes;
    }

    /// <summary>A simple polygon's triangles (ear clipping; either winding).</summary>
    public static int[] Triangulate(IReadOnlyList<Vector2> p)
    {
        var idx = Enumerable.Range(0, p.Count).ToList();
        float area = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % p.Count]);
            area += (a.X * b.Y) - (b.X * a.Y);
        }
        if (area < 0)
        {
            idx.Reverse();
        }
        var tris = new List<int>();
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var (a, b, c) = (p[ia], p[ib], p[ic]);
                if (Cross(b - a, c - b) <= 0 || idx.Any(j => j != ia && j != ib && j != ic && Inside(p[j], a, b, c)))
                {
                    continue;
                }
                tris.AddRange([ia, ib, ic]);
                idx.RemoveAt(i);
                cut = true;
                break;
            }
            if (!cut)
            {
                break;
            }
        }
        if (idx.Count == 3)
        {
            tris.AddRange(idx);
        }
        return [.. tris];
    }

    private static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0;

    /// <summary>The surface of the river or lake over an editor point (x, z), the highest where they meet; null: neither.</summary>
    public static float? SurfaceAt(float x, float z)
    {
        var p = new Vector2(x, -z);
        float? best = null;
        foreach (var r in Rivers)
        {
            float[] surfaces = Surfaces(r);
            for (int i = 0; i + 1 < r.Points.Count; i++)
            {
                var (a, b) = (r.Points[i], r.Points[i + 1]);
                if (SegmentDistance(p, new(a.X, a.Y), new(b.X, b.Y), out float t) <= a.HalfWidth + ((b.HalfWidth - a.HalfWidth) * t))
                {
                    best = MathF.Max(best ?? float.MinValue, surfaces[i] + ((surfaces[i + 1] - surfaces[i]) * t));
                }
            }
        }
        foreach (var l in Lakes.Where(l => l.Outline.Count >= 3 && InsideBy(l.Outline, p) >= 0))
        {
            best = MathF.Max(best ?? float.MinValue, l.Level);
        }
        return best;
    }

    /// <summary>How far a point is inside a polygon's outline (negative outside), metres.</summary>
    public static float InsideBy(IReadOnlyList<Vector2> outline, Vector2 p)
    {
        bool inside = false;
        float d = float.MaxValue;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            var (a, b) = (outline[i], outline[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
            d = MathF.Min(d, SegmentDistance(p, a, b, out _));
        }
        return inside ? d : -d;
    }

    /// <summary>The distance from a point to a segment, and how far along it (0..1) the nearest point is.</summary>
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        var ab = b - a;
        t = ab.LengthSquared() < 1e-9f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + (ab * t));
    }

    /// <summary>
    /// The bed a river cuts at a point (height, or null away from it): the surface less its depth in the
    /// middle, rising to the surface at its edge and to the ground over a bank as wide again (only ever lower).
    /// </summary>
    public static float? RiverBed(River r, float[] surfaces, Vector2 p)
    {
        float? best = null;
        for (int i = 0; i + 1 < r.Points.Count; i++)
        {
            var (a, b) = (r.Points[i], r.Points[i + 1]);
            float d = SegmentDistance(p, new(a.X, a.Y), new(b.X, b.Y), out float t);
            float hw = a.HalfWidth + ((b.HalfWidth - a.HalfWidth) * t), depth = a.Depth + ((b.Depth - a.Depth) * t);
            float surface = surfaces[i] + ((surfaces[i + 1] - surfaces[i]) * t);
            if (d > hw * 2)
            {
                continue;
            }
            float u = d / hw;   // 0 middle, 1 edge, 2 top of the bank
            float bed = u <= 1 ? surface - (depth * (1 - (u * u))) : surface + ((u - 1) * hw * 0.25f);
            best = best is float x ? MathF.Min(x, bed) : bed;
        }
        return best;
    }

    /// <summary>The bed a lake makes at a point (height, or null outside it and its bank).</summary>
    public static float? LakeBed(Lake l, Vector2 p)
    {
        float inside = InsideBy(l.Outline, p);
        if (inside < -6)
        {
            return null;
        }
        return inside >= 0 ? l.Level - (l.Depth * Math.Clamp(inside / 8f, 0.15f, 1f)) : l.Level + (-inside * 0.25f);
    }

    /// <summary>
    /// The ground cut down to a river's or lake's bed (never raised; one undo step), over its bounds.
    /// </summary>
    public static int Carve(EditSession session, string label, Func<Vector2, float?> bed, (float MinX, float MinY, float MaxX, float MaxY) bounds)
    {
        var g = session.Ground;
        return session.EditGround(label, ground =>
        {
            var touched = new List<int>();
            // Unreal (X, Y) -> editor (x, z = -Y) -> grid.
            int x0 = Math.Max(1, (int)MathF.Floor(bounds.MinX - g.OriginX)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(bounds.MaxX - g.OriginX));
            int z0 = Math.Max(1, (int)MathF.Floor(-bounds.MaxY - g.OriginZ)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(-bounds.MinY - g.OriginZ));
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    int p = (gz * g.W) + gx;
                    if (bed(new Vector2(g.OriginX + gx, -(g.OriginZ + gz))) is float b && b < ground.HeightOf(p) - 0.005f)
                    {
                        ground.SetHeight(p, b);
                        touched.Add(p);
                    }
                }
            }
            return (touched, (x0, z0, x1, z1));
        }).Count;
    }
}
