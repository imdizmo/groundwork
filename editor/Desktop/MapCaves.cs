using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The map's tunnels and caves, kept in its caves file: each a line of points under the ground (x, z in world metres,
/// how far its roof lies under the surface, how wide), its ends open to the air (a mouth) unless the far end is
/// closed (a cave). The ground is a height map, so a tunnel is a tube of rock drawn under it (MapCaves.Scene) with the
/// ground left undrawn where the tube breaks the surface; Cut paints those mouths into the hole channel, which Unreal's
/// landscape hides.
/// </summary>
public static partial class MapCaves
{
    /// <summary>A tunnel's point: world metres (x east, z north), how far its roof lies under the ground, and its radius.</summary>
    public sealed record Point(float X, float Z, float Depth, float Radius);

    /// <summary>A tunnel or cave.</summary>
    public sealed class Tunnel
    {
        /// <summary>Its name.</summary>
        public string Name { get; set; } = "Tunnel";

        /// <summary>Whether its far end opens to the air (false: a cave, closed at the far end).</summary>
        public bool EndOpen { get; set; } = true;

        /// <summary>Its points, from the mouth in.</summary>
        public List<Point> Points { get; } = [];
    }

    /// <summary>The tunnels.</summary>
    public static List<Tunnel> Tunnels { get; } = [];

    /// <summary>Changed since loaded or saved.</summary>
    public static bool Dirty { get; set; }

    /// <summary>Called when they change (the tool's list follows).</summary>
    public static event Action? Changed;

    /// <summary>Reads the map's caves file (none: no tunnels).</summary>
    public static void Load(string file)
    {
        Read(File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null);
        Dirty = false;
        Changed?.Invoke();
        ModExtensions.Redraw();
    }

    private static void Read(JsonObject? o)
    {
        Tunnels.Clear();
        foreach (var t in o?["tunnels"]?.AsArray() ?? [])
        {
            var tunnel = new Tunnel { Name = t!["name"]?.GetValue<string>() ?? "Tunnel", EndOpen = t["endOpen"]?.GetValue<bool>() ?? true };
            tunnel.Points.AddRange(t["points"]!.AsArray().Select(p => new Point(F(p, 0), F(p, 1), F(p, 2), F(p, 3))));
            Tunnels.Add(tunnel);
        }
    }

    private static float F(JsonNode? p, int i) => p![i]!.GetValue<float>();

    /// <summary>Writes the map's caves file.</summary>
    public static void Save(string file)
    {
        File.WriteAllText(file, Snapshot());
        Dirty = false;
    }

    /// <summary>The tunnels as caves.json holds them (an undo step keeps two of these).</summary>
    public static string Snapshot()
    {
        static float R(float v) => MathF.Round(v * 1000f) / 1000f;
        var o = new JsonObject
        {
            ["note"] = "World metres (x east, z north); points: x, z, the floor's depth under the ground, radius; endOpen: the far end is a mouth too",
            ["tunnels"] = new JsonArray([.. Tunnels.Select(t => new JsonObject
            {
                ["name"] = t.Name,
                ["endOpen"] = t.EndOpen,
                ["points"] = new JsonArray([.. t.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Z), R(p.Depth), R(p.Radius)))]),
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

    /// <summary>Marks them changed and has them drawn again.</summary>
    public static void Touch()
    {
        Dirty = true;
        Changed?.Invoke();
        ModExtensions.Redraw();
    }

    /// <summary>Metres between the tube's rings.</summary>
    public const float Step = 1.5f;

    /// <summary>Corners round a ring.</summary>
    public const int Sides = 14;

    /// <summary>
    /// The tube's middle line, sampled every <see cref="Step"/> or so: a smooth curve through the points, each at
    /// ground − depth − 1.3 × radius (the roof's bumps the depth asked under the ground) and the mouths at ground +
    /// 0.55 × radius (their floor at the ground), the radius running between the points'; run on half a radius past
    /// each mouth so the arch stands clear of the hillside.
    /// </summary>
    /// <param name="t">The tunnel.</param>
    /// <param name="ground">The ground's height at a point.</param>
    public static List<(Vector3 P, float R)> Line(Tunnel t, Func<float, float, float> ground)
    {
        var line = new List<(Vector3, float)>();
        int n = t.Points.Count;
        if (n < 2)
        {
            return line;
        }
        // The points as middles: the first always a mouth, the last when open; a mouth dives to the next point's depth
        // at a slope one can walk (1.7 m along for each metre down, a radius and a half at least), so the ground is
        // opened only there, not along a long slow slope.
        var pts = new List<(Vector3 M, float R)>();
        for (int i = 0; i < n; i++)
        {
            var p = t.Points[i];
            bool mouth = i == 0 || (i == n - 1 && t.EndOpen);
            var at = new Vector3(p.X, ground(p.X, p.Z) + (mouth ? 0.55f * p.Radius : -p.Depth - (1.3f * p.Radius)), p.Z);
            if (mouth && i == n - 1 && n >= 2)
            {
                pts.Add(Dive(t.Points[i - 1], p, p));
            }
            pts.Add((at, p.Radius));
            if (mouth && i == 0 && n >= 2)
            {
                pts.Add(Dive(t.Points[1], p, t.Points[1]));
            }
        }
        (Vector3, float) Dive(Point towards, Point mouth, Point deep)
        {
            var dir = new Vector2(towards.X - mouth.X, towards.Z - mouth.Z);
            float len = dir.Length(), drop = deep.Depth + (1.85f * mouth.Radius);
            float along = MathF.Min(MathF.Max(1.5f * mouth.Radius, 1.7f * drop), len * 0.5f);
            dir = len < 1e-3f ? Vector2.UnitX : dir / len;
            float x = mouth.X + (dir.X * along), z = mouth.Z + (dir.Y * along);
            return (new Vector3(x, ground(x, z) - deep.Depth - (1.3f * mouth.Radius), z), mouth.Radius);
        }
        n = pts.Count;
        Vector3 At(int i) => pts[Math.Clamp(i, 0, n - 1)].M;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
            float r1 = pts[i].R, r2 = pts[i + 1].R;
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(p1, p2) / Step));
            for (int k = 0; k < steps; k++)
            {
                float u = k / (float)steps;
                line.Add((CatmullRom(p0, p1, p2, p3, u), r1 + ((r2 - r1) * u)));
            }
        }
        line.Add(pts[n - 1]);
        // The mouths' lips: on past the end, along the last stretch, half a radius.
        Vector3 Lip(Vector3 end, Vector3 before, float r) => end + (Vector3.Normalize(end - before) * (0.5f * r));
        if (line.Count >= 2)
        {
            line.Insert(0, (Lip(line[0].Item1, line[1].Item1, line[0].Item2), line[0].Item2));
            if (t.EndOpen)
            {
                line.Add((Lip(line[^1].Item1, line[^2].Item1, line[^1].Item2), line[^1].Item2));
            }
        }
        return line;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2 * p1) + ((p2 - p0) * t) + (((2 * p0) - (5 * p1) + (4 * p2) - p3) * t2) + (((3 * p1) - p0 - (3 * p2) + p3) * t3));
    }

    /// <summary>
    /// The tube's rock: rings of corners round the middle line (a flat floor, an arched roof, the walls bumpy), the
    /// inside the front (it is seen from within), capped at a closed far end. Positions, normals and colours, and the
    /// triangles wound as a scene mesh takes them.
    /// </summary>
    /// <param name="line">The middle line (<see cref="Line"/>), at least two samples.</param>
    /// <param name="endOpen">Whether the far end is a mouth (no cap).</param>
    /// <param name="seed">Varies the bumps.</param>
    public static (Vector3[] Positions, Vector3[] Normals, Vector3[] Colors, int[] Triangles) Tube(IReadOnlyList<(Vector3 P, float R)> line, bool endOpen, int seed)
    {
        int rings = line.Count;
        var pos = new List<Vector3>(rings * Sides);
        var col = new List<Vector3>(rings * Sides);
        var rock = new Vector3(0.34f, 0.30f, 0.27f);
        for (int k = 0; k < rings; k++)
        {
            var (c, r) = line[k];
            var tangent = line[Math.Min(k + 1, rings - 1)].P - line[Math.Max(k - 1, 0)].P;
            tangent = tangent.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(tangent);
            var side = Vector3.Cross(Vector3.UnitY, tangent);
            side = side.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(side);
            var up = Vector3.Normalize(Vector3.Cross(tangent, side));
            for (int i = 0; i < Sides; i++)
            {
                float a = MathF.Tau * i / Sides, cos = MathF.Cos(a), sin = MathF.Sin(a);
                bool floor = sin < -0.55f;
                // A circle with its bottom cut flat, bumps on the walls and roof, few on the floor.
                float y = MathF.Max(sin, -0.55f);
                var flat = c + (side * (cos * r)) + (up * (y * r));
                float bump = (MapTools.Base.Noise01(flat.X * 0.45f, (flat.Z * 0.45f) + (flat.Y * 0.31f), seed) - 0.5f) * (floor ? 0.08f : 0.3f);
                var outward = Vector3.Normalize((side * cos) + (up * y));
                pos.Add(flat + (outward * (bump * r)));
                float tone = 0.85f + (0.3f * MapTools.Base.Noise01(flat.X * 0.9f, flat.Z * 0.9f, seed + 7));
                col.Add((floor ? new Vector3(0.36f, 0.31f, 0.25f) : rock) * tone);
            }
        }
        var tri = new List<int>();
        void Face(int a, int b, int c, Vector3 inward)
        {
            // The front faces the inside: wound so its normal (as the scene mesh winds) points inward.
            var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            if (Vector3.Dot(n, inward) < 0)
            {
                (b, c) = (c, b);
            }
            tri.AddRange([a, b, c]);
        }
        for (int k = 0; k < rings - 1; k++)
        {
            var inward = line[k].P;
            for (int i = 0; i < Sides; i++)
            {
                int a = (k * Sides) + i, b = (k * Sides) + ((i + 1) % Sides), c = a + Sides, d = b + Sides;
                Face(a, b, c, inward - pos[a]);
                Face(b, d, c, inward - pos[b]);
            }
        }
        if (!endOpen)
        {
            // A rock wall across the far end, a little further in.
            var (c, r) = line[^1];
            int middle = pos.Count;
            var back = c + (Vector3.Normalize(line[^1].P - line[^2].P) * (0.3f * r));
            pos.Add(back);
            col.Add(rock);
            for (int i = 0; i < Sides; i++)
            {
                int a = ((rings - 1) * Sides) + i, b = ((rings - 1) * Sides) + ((i + 1) % Sides);
                Face(a, b, middle, line[^2].P - back);
            }
        }
        // Normals from the faces, so the bumps are lit as they lie.
        var nrm = new Vector3[pos.Count];
        for (int t = 0; t < tri.Count; t += 3)
        {
            int a = tri[t], b = tri[t + 1], c = tri[t + 2];
            var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            nrm[a] += n;
            nrm[b] += n;
            nrm[c] += n;
        }
        for (int i = 0; i < nrm.Length; i++)
        {
            nrm[i] = nrm[i].LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(nrm[i]);
        }
        return ([.. pos], nrm, [.. col], [.. tri]);
    }

    /// <summary>
    /// The ground cells the tube breaks through (its mouths, and any stretch too shallow for its roof): each whole-metre
    /// cell within the radius of a sample whose roof (bumps and all) is above the ground, one rectangle a cell (x from X0 to X1, z
    /// from Z0 to Z1, the far edges out), each once.
    /// </summary>
    /// <param name="line">The middle line.</param>
    /// <param name="ground">The ground's height at a point.</param>
    public static List<(int X0, int Z0, int X1, int Z1)> Mouths(IReadOnlyList<(Vector3 P, float R)> line, Func<float, float, float> ground)
    {
        var cells = new HashSet<(int, int)>();
        // Not under the lips (the first and last samples): they stand clear of the hillside, in the air.
        for (int k = 1; k < line.Count - 1; k++)
        {
            var (c, r) = line[k];
            if (ground(c.X, c.Z) >= c.Y + (1.3f * r))
            {
                continue;   // the roof, bumps and all, under the ground
            }
            float w = r * 0.8f;   // the floor's half-width: the walls above it meet the ground
            for (int z = (int)MathF.Floor(c.Z - w); z < MathF.Ceiling(c.Z + w); z++)
            {
                for (int x = (int)MathF.Floor(c.X - w); x < MathF.Ceiling(c.X + w); x++)
                {
                    if (Vector2.DistanceSquared(new Vector2(x + 0.5f, z + 0.5f), new Vector2(c.X, c.Z)) <= w * w)
                    {
                        _ = cells.Add((x, z));
                    }
                }
            }
        }
        return [.. cells.Order().Select(c => (c.Item1, c.Item2, c.Item1 + 1, c.Item2 + 1))];
    }

    /// <summary>
    /// Paints a tunnel's mouths into the ground's hole channel (Unreal hides its landscape there), one undo step;
    /// how many ground points it touched.
    /// </summary>
    /// <param name="session">The map's session.</param>
    /// <param name="t">The tunnel.</param>
    /// <param name="height">The ground's height at a point (the area's).</param>
    public static int Cut(EditSession session, Tunnel t, Func<float, float, float> height)
    {
        var g = session.Ground;
        var line = Line(t, height);
        var mouths = Mouths(line, height);
        if (mouths.Count == 0)
        {
            return 0;
        }
        return session.EditGround("Cut the mouths of " + t.Name, ground =>
        {
            var touched = new List<int>();
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            foreach (var (mx0, mz0, mx1, mz1) in mouths)
            {
                // A cell's four corners: Unreal hides a landscape quad when all its corners are holes.
                for (int wz = mz0; wz <= mz1; wz++)
                {
                    for (int wx = mx0; wx <= mx1; wx++)
                    {
                        int gx = wx - (int)g.OriginX, gz = wz - (int)g.OriginZ;
                        if (gx < 1 || gz < 1 || gx >= g.W - 1 || gz >= g.H - 1)
                        {
                            continue;
                        }
                        int p = (gz * g.W) + gx;
                        if (ground.MaskOf(p, Ground.HoleChannel) < 0.5f)
                        {
                            Sculpt.HolePoint(ground, p, 1, fill: false);
                            touched.Add(p);
                            (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
                        }
                    }
                }
            }
            return (touched, touched.Count == 0 ? (0, 0, 0, 0) : (x0, z0, x1, z1));
        }).Count;
    }
}
