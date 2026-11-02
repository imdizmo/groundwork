using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>The shapes some zones have beyond rolling hills: shelves, canyons, a coast or the world's edge, rivers, rocks in the air, snow.</summary>
public static partial class ZoneGenerator
{
    /// <summary>The rolling ground stepped into shelves (where the terraces reach) and cut by canyons.</summary>
    private static float Shaped(Spec spec, float ground, float x, float y)
    {
        if (spec.Rough > 0)
        {
            ground += spec.Rough * 2.5f * Fbm(x / 30, y / 30, spec.Seed + 101, 3);   // Fbm stays mostly within ±0.4
        }
        if (spec.Terraces > 0)
        {
            // a shelf every Terraces metres, flat for most of the step, then a steep riser; the steps wander a little
            float wander = 0.25f * Fbm(x / 140, y / 140, spec.Seed + 71, 3);
            float v = (ground / spec.Terraces) + wander, k = MathF.Floor(v), f = v - k;
            float stepped = (k + Smooth(0.72f, 0.95f, f) - wander) * spec.Terraces;
            float where = spec.TerraceShare >= 1 ? 1 : Smooth(-0.05f, 0.05f, Fbm(x / 500, y / 500, spec.Seed + 73, 2) - Quantile01(1 - spec.TerraceShare));
            ground += (stepped - ground) * where;
        }
        if (spec.Canyons > 0)
        {
            // where the noise crosses zero: winding lines about CanyonSpacing apart, flat-floored, steep-walled
            float n = MathF.Abs(Fbm(x / spec.CanyonSpacing, y / spec.CanyonSpacing, spec.Seed + 77, 3));
            float half = spec.CanyonWidth / spec.CanyonSpacing * 0.22f;
            ground -= spec.Canyons * Smooth(half * 2.2f, half, n);
        }
        return ground;
    }

    /// <summary>About the value of the noise a share of the ground lies under (Fbm is roughly even about 0, mostly within ±0.5).</summary>
    private static float Quantile01(float share) => (Math.Clamp(share, 0, 1) - 0.5f) * 0.9f;

    /// <summary>A noise level that the share asked of the map's open-ground patch noise lies under (sampled).</summary>
    private static float Quantile(Spec spec, int cols, int rows, float x0, float y0, float share)
    {
        var sample = new List<float>();
        for (int r = 0; r < rows; r += 9)
        {
            for (int c = 0; c < cols; c += 9)
            {
                sample.Add(Fbm((c + x0) / 45, (r + y0) / 45, spec.Seed + 41, 3));
            }
        }
        sample.Sort();
        return share >= 1 ? float.MaxValue : sample[Math.Clamp((int)(share * sample.Count), 0, sample.Count - 1)];
    }

    /// <summary>How far a point is from the coast's edge, wandering in and out (metres).</summary>
    private static float FromCoast(Spec spec, int c, int r, int cols, int rows, float x, float y)
    {
        float d = spec.CoastSide switch
        {
            "east" => cols - 1 - c,
            "north" => r,
            "south" => rows - 1 - r,
            _ => c,
        };
        // bays and headlands a few hundred metres across, however wide the coast is
        float across = MathF.Min(spec.Coast * 2.5f, 700), deep = MathF.Min(spec.Coast * 0.35f, 220);
        return d + (deep * Fbm(x / across, y / across, spec.Seed + 83, 3) / 0.4f);
    }

    /// <summary>The distance to the nearest edge that is not the coast's (the rim stands only there).</summary>
    private static float FarEdge(Spec spec, int c, int r, int cols, int rows)
    {
        float d = float.MaxValue;
        if (spec.CoastSide != "west")
        {
            d = MathF.Min(d, c);
        }
        if (spec.CoastSide != "east")
        {
            d = MathF.Min(d, cols - 1 - c);
        }
        if (spec.CoastSide != "north")
        {
            d = MathF.Min(d, r);
        }
        if (spec.CoastSide != "south")
        {
            d = MathF.Min(d, rows - 1 - r);
        }
        return d;
    }

    /// <summary>The ground going down to the sea floor towards the coast's edge, or over a cliff into nothing (CoastDry).</summary>
    private static float Coasted(Spec spec, float ground, int c, int r, int cols, int rows, float x, float y)
    {
        if (spec.Coast <= 0)
        {
            return ground;
        }
        float d = FromCoast(spec, c, r, cols, rows, x, y);
        if (spec.CoastDry)
        {
            // the world's edge: the ground runs on, then breaks off in a cliff (a band of 15 to 60 m)
            float edge = spec.Coast * 0.3f, band = Math.Clamp(spec.Coast * 0.02f, 15, 60);
            float t = Smooth(edge + band, edge, d);
            return ground + ((-400 - ground) * t);
        }
        // a beach: the ground eases down to the water, then the sea floor drops away
        float down = Smooth(spec.Coast, 0, d);
        return ground + ((-spec.CoastDepth - ground) * MathF.Pow(down, 0.8f));
    }

    /// <summary>Low ground (under Marsh metres) flattened towards wet flats, pools of it dipping under the sea's level (0).</summary>
    private static float Marshed(Spec spec, float ground, float x, float y)
    {
        if (spec.Marsh <= 0 || ground >= spec.Marsh || ground < -2)
        {
            return ground;
        }
        float low = Smooth(spec.Marsh, spec.Marsh * 0.35f, ground);
        float n = Fbm(x / 70, y / 70, spec.Seed + 97, 4), q = Quantile01(1 - spec.MarshShare);
        float flat = 0.6f + (0.8f * Smooth(-0.3f, 0.3f, n));   // the flats: a metre or so above the water
        float pool = Smooth(q - 0.03f, q + 0.03f, n);
        ground += (flat - ground) * low * 0.8f;
        return ground + ((-1.2f - ground) * pool * low);
    }

    /// <summary>
    /// Rivers: each from high open ground, downhill (and on towards the nearest lake, the coast or the far edge),
    /// winding, until it reaches a lake, the sea or the edge. Its surface falls along it, never rising; it is kept as
    /// stretches of about 150 m whose surfaces fall evenly (as the map's rivers are), its bed carved under them and its
    /// banks eased down to it.
    /// </summary>
    private static List<River> MakeRivers(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes, Random rng)
    {
        var all = new List<River>();
        float margin = spec.RimHeight > 0 ? spec.RimWidth * 0.5f : 20;
        for (int n = 0; n < spec.Rivers; n++)
        {
            // the source: the highest of a few open places, not too steep, away from the edge
            Vector2? start = null;
            float best = float.MinValue;
            for (int k = 0; k < 200; k++)
            {
                var at = new Vector2(margin + (rng.NextSingle() * (cols - (2 * margin))), margin + (rng.NextSingle() * (rows - (2 * margin))));
                if (cols <= 2 * margin || rows <= 2 * margin)
                {
                    break;
                }
                float g = At(h, cols, rows, at.X, at.Y);
                if (g > 1 && g > best && Slope(h, cols, rows, (int)at.X, (int)at.Y) < 20 && !all.Any(r => r.Points.Any(p => Vector2.Distance(new(p.X - x0, p.Y - y0), at) < 200)))
                {
                    (start, best) = (at, g);
                }
            }
            if (start is not Vector2 s0)
            {
                continue;
            }
            // where it heads: the nearest lake, else the coast, else the far side
            Vector2 goal = lakes.Count > 0 ? lakes.OrderBy(l => Vector2.Distance(l.Middle - new Vector2(x0, y0), s0)).First().Middle - new Vector2(x0, y0)
                : spec.Coast > 0 && !spec.CoastDry ? spec.CoastSide switch { "east" => new(cols, s0.Y), "north" => new(s0.X, 0), "south" => new(s0.X, rows), _ => new(0, s0.Y) }
                : new Vector2(cols - s0.X, rows - s0.Y);
            var path = new List<Vector2> { s0 };
            var dir = Vector2.Normalize(goal - s0 + new Vector2(1e-3f));
            float endLevel = float.MinValue;
            for (int step = 0; step < 800; step++)
            {
                var p = path[^1];
                float gx = At(h, cols, rows, p.X + 3, p.Y) - At(h, cols, rows, p.X - 3, p.Y), gy = At(h, cols, rows, p.X, p.Y + 3) - At(h, cols, rows, p.X, p.Y - 3);
                var down = new Vector2(-gx, -gy);
                down = down.LengthSquared() > 1e-6f ? Vector2.Normalize(down) : Vector2.Zero;
                var toGoal = Vector2.Normalize(goal - p + new Vector2(1e-3f));
                float bend = 0.9f * Fbm((p.X + x0) / 180, (p.Y + y0) / 180, spec.Seed + 89 + n, 2);
                var want = (dir * 1.2f) + (down * 0.9f) + (toGoal * 0.7f);
                want = Vector2.Normalize(Vector2.Transform(want, Matrix3x2.CreateRotation(bend * 0.6f)) + new Vector2(1e-4f));
                dir = want;
                var next = p + (dir * 12);
                if (next.X < 2 || next.Y < 2 || next.X > cols - 3 || next.Y > rows - 3)
                {
                    path.Add(Vector2.Clamp(next, Vector2.Zero, new Vector2(cols - 1, rows - 1)));
                    break;
                }
                path.Add(next);
                if (lakes.FirstOrDefault(l => InsideBy(l.Outline, next + new Vector2(x0, y0)) > 2) is { } lake)
                {
                    endLevel = lake.Level;
                    break;
                }
                if (spec.Coast > 0 && !spec.CoastDry && At(h, cols, rows, next.X, next.Y) < -0.5f)
                {
                    endLevel = 0;
                    break;
                }
            }
            if (path.Count < 6)
            {
                continue;
            }
            // the surface: under the ground along it, never rising, not below where it ends
            float[] surface = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                float g = At(h, cols, rows, path[i].X, path[i].Y) - 0.8f;
                surface[i] = i == 0 ? g : MathF.Min(surface[i - 1] - 0.01f, g);
            }
            for (int i = 0; i < path.Count; i++)
            {
                surface[i] = MathF.Max(surface[i], endLevel);
            }
            // stretches of a dozen points, each falling evenly from its first point's surface to its last's
            float half = spec.RiverWidth / 2;
            var stretches = new List<(int From, int To)>();
            for (int i = 0; i < path.Count - 1; i += 12)
            {
                stretches.Add((i, Math.Min(path.Count - 1, i + 12)));
            }
            float[] even = new float[path.Count];
            foreach (var (a, b) in stretches)
            {
                for (int i = a; i <= b; i++)
                {
                    even[i] = surface[a] + ((surface[b] - surface[a]) * (i - a) / Math.Max(1, b - a));
                }
            }
            Carve(h, cols, rows, path, even, half, MathF.Max(1.2f, half * 0.35f));
            foreach (var (a, b) in stretches)
            {
                float hw(int i) => half * (0.8f + (0.4f * (i / (float)(path.Count - 1))));   // wider downstream
                all.Add(new River([.. Enumerable.Range(a, b - a + 1).Select(i => new MapWater.Point(path[i].X + x0, path[i].Y + y0, hw(i), MathF.Max(1.2f, half * 0.35f), 1.2f))], even[a], even[b]));
            }
        }
        return all;
    }

    /// <summary>A river's bed cut under its surface (deepest in the middle), its banks eased down to just above the water.</summary>
    private static void Carve(float[] h, int cols, int rows, List<Vector2> path, float[] surface, float half, float depth)
    {
        // each pixel near the river: its distance to the nearest stretch of it, and the surface there (only the pixels
        // round each stretch are visited)
        float reach = (half * 3) + 30;
        var near = new Dictionary<int, (float D, float S)>();
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            int c0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), c1 = Math.Min(cols - 1, (int)(MathF.Max(a.X, b.X) + reach));
            int r0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), r1 = Math.Min(rows - 1, (int)(MathF.Max(a.Y, b.Y) + reach));
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    float d = MapWater.SegmentDistance(new Vector2(c, r), a, b, out float t);
                    int p = (r * cols) + c;
                    if (d <= reach && (!near.TryGetValue(p, out var was) || d < was.D))
                    {
                        near[p] = (d, surface[i] + ((surface[i + 1] - surface[i]) * t));
                    }
                }
            }
        }
        foreach (var (p, (d, s)) in near)
        {
            if (d <= half * 1.15f)
            {
                float u = MathF.Min(1, d / (half * 1.15f));
                h[p] = MathF.Min(h[p], s - (depth * (1 - (u * u))) - 0.15f);
            }
            else
            {
                // the bank: just above the water at the edge, rising gently, then the ground as it was
                float bank = s + 0.35f + ((d - (half * 1.15f)) * 0.2f);
                if (h[p] > bank)
                {
                    h[p] = bank + ((h[p] - bank) * Smooth(half * 1.2f, reach, d));
                }
                else if (d < half * 1.6f)
                {
                    h[p] = bank;   // a low bank lifted just enough to hold the water
                }
            }
        }
    }

    /// <summary>Where the ground is under water (the sea), in a river's channel or past a dry edge: nothing grows or stands there.</summary>
    private static bool[] Wet(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<River> rivers)
    {
        bool[] wet = new bool[cols * rows];
        if ((spec.Coast > 0 && !spec.CoastDry) || spec.Marsh > 0)
        {
            for (int p = 0; p < wet.Length; p++)
            {
                wet[p] = h[p] < 0.3f;
            }
        }
        else if (spec.CoastDry)
        {
            for (int p = 0; p < wet.Length; p++)
            {
                wet[p] = h[p] < -150;   // the cliff face and past it, at the world's edge: nothing stands there
            }
        }
        foreach (var river in rivers)
        {
            foreach (var (a, b) in river.Points.Zip(river.Points.Skip(1)))
            {
                float reach = a.HalfWidth + 2;
                for (float t = 0; t <= 1; t += 0.1f)
                {
                    var m = Vector2.Lerp(new(a.X - x0, a.Y - y0), new(b.X - x0, b.Y - y0), t);
                    for (int r = Math.Max(0, (int)(m.Y - reach)); r <= Math.Min(rows - 1, (int)(m.Y + reach)); r++)
                    {
                        for (int c = Math.Max(0, (int)(m.X - reach)); c <= Math.Min(cols - 1, (int)(m.X + reach)); c++)
                        {
                            if (Vector2.Distance(new(c, r), m) <= reach)
                            {
                                wet[(r * cols) + c] = true;
                            }
                        }
                    }
                }
            }
        }
        return wet;
    }

    /// <summary>
    /// Rocks in the air, 50 to 200 m over the ground: floating islands where the map has them (their tops planted with its
    /// trees, bushes and rocks), else the largest of the Rocks kinds, scaled up.
    /// </summary>
    private static void Float(Spec spec, float[] h, int cols, int rows, float x0, float y0, Planting kinds, Random rng, List<(string, Vector3, float, float)> objects)
    {
        if (kinds.Islands.Count > 0)
        {
            Islands(spec, h, cols, rows, x0, y0, kinds, rng, objects);
            return;
        }
        var big = kinds.Rocks.Where(k => k.Model.Contains("Big", StringComparison.OrdinalIgnoreCase) || k.Label.Contains("Big", StringComparison.OrdinalIgnoreCase)).ToList();
        var from = big.Count > 0 ? big : [.. kinds.Rocks];
        if (from.Count == 0)
        {
            return;
        }
        for (int i = 0; i < spec.Floaters; i++)
        {
            float c = 40 + (rng.NextSingle() * (cols - 80)), r = 40 + (rng.NextSingle() * (rows - 80));
            float up = 50 + (rng.NextSingle() * 150);
            objects.Add((from[rng.Next(from.Count)].Model, new Vector3(c + x0, At(h, cols, rows, c, r) + up, -(r + y0)), rng.NextSingle() * 360, 8 + (rng.NextSingle() * 22)));
        }
    }

    /// <summary>A point of a model turned by its yaw (degrees), as the editor turns it.</summary>
    internal static Vector2 Turn(float x, float z, float yaw)
    {
        float a = yaw * MathF.PI / 180, cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new((x * cos) + (z * sin), (z * cos) - (x * sin));
    }

    /// <summary>Floating islands, mostly small and a few large, apart from each other, their tops planted.</summary>
    private static void Islands(Spec spec, float[] h, int cols, int rows, float x0, float y0, Planting kinds, Random rng, List<(string, Vector3, float, float)> objects)
    {
        var placed = new List<(Vector2 At, float Reach)>();
        for (int i = 0; i < spec.Floaters; i++)
        {
            var kind = kinds.Islands[rng.Next(kinds.Islands.Count)];
            int variant = FloatingIsland.VariantOf(kind.Model);
            float scale = 0.5f + (2.2f * rng.NextSingle() * rng.NextSingle()), reach = FloatingIsland.Radius * scale * 1.3f;
            Vector2 at = default;
            bool free = false;
            for (int tries = 0; tries < 30 && !free; tries++)
            {
                at = new(reach + (rng.NextSingle() * (cols - (2 * reach))), reach + (rng.NextSingle() * (rows - (2 * reach))));
                free = placed.All(p => Vector2.Distance(p.At, at) > p.Reach + reach);
            }
            if (!free || variant < 0)
            {
                continue;
            }
            placed.Add((at, reach));
            float yaw = rng.NextSingle() * 360, y = At(h, cols, rows, at.X, at.Y) + 50 + (rng.NextSingle() * 150);
            objects.Add((kind.Model, new Vector3(at.X + x0, y, -(at.Y + y0)), yaw, scale));
            // its top: trees, bushes and a rock or two, as many as its size holds
            float area = MathF.PI * MathF.Pow(FloatingIsland.Radius * scale, 2);
            foreach (var (from, each) in new[] { (kinds.Trees, 350f), (kinds.Bushes, 120f), (kinds.Rocks.Where(k => !k.Model.Contains("Big", StringComparison.OrdinalIgnoreCase)).ToList(), 600f) })
            {
                for (int n = (int)MathF.Round(area / each * (0.6f + (0.8f * rng.NextSingle()))); n > 0 && from.Count > 0; n--)
                {
                    float ang = rng.NextSingle() * MathF.Tau, t = 0.75f * MathF.Sqrt(rng.NextSingle());
                    float lx = MathF.Cos(ang) * t * FloatingIsland.Edge(variant, ang), lz = MathF.Sin(ang) * t * FloatingIsland.Edge(variant, ang);
                    if (FloatingIsland.TopAt(variant, lx, lz) is not { } top)
                    {
                        continue;
                    }
                    var k = from[rng.Next(from.Count)];
                    var w = Turn(lx * scale, lz * scale, yaw);
                    objects.Add((k.Model, new Vector3(at.X + x0 + w.X, y + (top * scale) - 0.3f, -(at.Y + y0) + w.Y), rng.NextSingle() * 360, k.MinScale + (rng.NextSingle() * (k.MaxScale - k.MinScale))));
                }
            }
        }
    }
}
