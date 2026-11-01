using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// A whole zone generated from a few numbers: rolling ground between two heights with hills so far apart, a mountain
/// rim round it, lakes of given sizes in its low ground, woods covering a share of it, and the map's four layers painted
/// by rule (rock on steep ground, dirt under the woods, sand on the shores, grass elsewhere). Every seed is a new zone.
/// </summary>
public static class ZoneGenerator
{
    /// <summary>
    /// What to make: the rolling ground's lowest and highest heights and how far apart its hills are (m); the rim's
    /// height above the rolling ground and its width (0: none); the lakes' sizes (hectares); the share of the ground
    /// that is wooded (0..1) and its trees per hectare; trees per hectare outside the woods; small objects (bushes,
    /// plants, rocks) per hectare; how much bigger the trees are than their kinds' own sizes (a game's trees are often
    /// several times a real tree's: the canopy, not the count, is what makes a wood); the sand band round the lakes (m,
    /// 0: none); the seed.
    /// </summary>
    public sealed record Spec(float Low = 20, float High = 70, float HillSize = 300, float RimHeight = 200, float RimWidth = 220,
        float[]? LakeHectares = null, float Woods = 0.35f, float WoodDensity = 17, float OpenTrees = 0.3f, float Small = 20, float TreeScale = 1, float Shore = 6, int Seed = 1);

    /// <summary>A lake made: its middle and outline (Unreal X, Y metres), its surface height and depth.</summary>
    public sealed record Lake(Vector2 Middle, Vector2[] Outline, float Level, float Depth);

    /// <summary>What was made, on a map of cols by rows metres whose first pixel is at (x0, y0) in Unreal metres.</summary>
    public sealed record Zone(float[] Heights, byte[] Mask, List<(string Model, Vector3 At, float Yaw, float Scale)> Objects, List<Lake> Lakes, float[] Wooded);

    /// <summary>The kinds the zone is planted with, by the catalog's groups.</summary>
    public sealed record Planting(IReadOnlyList<MapTools.Kind> Trees, IReadOnlyList<MapTools.Kind> Bushes, IReadOnlyList<MapTools.Kind> Rocks, IReadOnlyList<MapTools.Kind> Plants)
    {
        /// <summary>A catalog's kinds sorted into its Trees, Bushes, Rocks and Plants groups.</summary>
        public static Planting From(IEnumerable<MapTools.Kind> kinds)
        {
            var all = kinds.ToList();
            List<MapTools.Kind> Of(string group) => [.. all.Where(k => string.Equals(k.Category, group, StringComparison.OrdinalIgnoreCase))];
            return new(Of("Trees"), Of("Bushes"), Of("Rocks"), Of("Plants"));
        }
    }

    /// <summary>Generates a zone (the layers' channels: Sand r, Dirt g, Rock b, as a new map's).</summary>
    public static Zone Generate(Spec spec, int cols, int rows, float x0, float y0, Planting planting)
    {
        var rng = new Random(spec.Seed);
        float[] h = new float[cols * rows];
        float Edge(int c, int r) => MathF.Min(MathF.Min(c, cols - 1 - c), MathF.Min(r, rows - 1 - r));
        // the rolling ground: noise of the hills' size and smaller, stretched so that it reaches from Low to High (its
        // lowest and highest hundredth), more low ground than high
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                h[(r * cols) + c] = Fbm((c + x0) / spec.HillSize, (r + y0) / spec.HillSize, spec.Seed, 5);
            }
        }
        float[] sample = [.. h.Where((_, i) => i % 97 == 0).Order()];
        float lo = sample[sample.Length / 100], hi = sample[sample.Length * 99 / 100];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = c + x0, y = r + y0;
                float t = Math.Clamp((h[(r * cols) + c] - lo) / MathF.Max(1e-4f, hi - lo), 0, 1);
                float ground = spec.Low + ((spec.High - spec.Low) * MathF.Pow(t, 1.3f));
                // the rim: a wall of ridged mountains round the edge, its foot wandering in and out
                float rim = 0;
                if (spec.RimHeight > 0 && spec.RimWidth > 0)
                {
                    float wander = 1 + (0.45f * Fbm(x / (spec.RimWidth * 2), y / (spec.RimWidth * 2), spec.Seed + 7, 3));
                    float d = Edge(c, r) / (spec.RimWidth * wander);
                    if (d < 1)
                    {
                        float rise = Smooth(1, 0.35f, d);
                        float ridges = 0.65f + (0.35f * Ridged(x / 90, y / 90, spec.Seed + 11, 4));
                        rim = spec.RimHeight * rise * ridges;
                    }
                }
                h[(r * cols) + c] = ground + rim;
            }
        }
        var lakes = PlaceLakes(spec, h, cols, rows, x0, y0, rng);
        float[] wooded = Woods(spec, h, cols, rows, x0, y0, lakes);
        byte[] mask = Paint(spec, h, cols, rows, x0, y0, lakes, wooded);
        var objects = Plant(spec, h, cols, rows, x0, y0, lakes, wooded, planting, rng);
        return new Zone(h, mask, objects, lakes, wooded);
    }

    /// <summary>The lakes: each in the lowest open ground found away from the rim and the others, its bed carved.</summary>
    private static List<Lake> PlaceLakes(Spec spec, float[] h, int cols, int rows, float x0, float y0, Random rng)
    {
        var lakes = new List<Lake>();
        foreach (float ha in (spec.LakeHectares ?? []).OrderDescending())
        {
            float radius = MathF.Sqrt(ha * 10000 / MathF.PI), margin = (spec.RimHeight > 0 ? spec.RimWidth : 0) + radius + 30;
            (Vector2 At, float Low)? best = null;
            for (int k = 0; k < 400; k++)
            {
                var at = new Vector2(margin + (rng.NextSingle() * (cols - (2 * margin))), margin + (rng.NextSingle() * (rows - (2 * margin))));
                if (cols <= 2 * margin || rows <= 2 * margin || lakes.Any(l => Vector2.Distance(l.Middle - new Vector2(x0, y0), at) < radius + Radius(l) + 60))
                {
                    continue;
                }
                float low = 0;
                for (int s = 0; s < 9; s++)
                {
                    float a = MathF.Tau * s / 9, d = s == 0 ? 0 : radius * 0.7f;
                    low += At(h, cols, rows, at.X + (d * MathF.Cos(a)), at.Y + (d * MathF.Sin(a)));
                }
                if (best == null || low / 9 < best.Value.Low)
                {
                    best = (at, low / 9);
                }
            }
            if (best is not { } b)
            {
                continue;
            }
            var outline = new Vector2[28];
            for (int i = 0; i < outline.Length; i++)
            {
                float a = MathF.Tau * i / outline.Length, wobble = 1 + (0.28f * Fbm((MathF.Cos(a) * 1.7f) + (lakes.Count * 13), MathF.Sin(a) * 1.7f, spec.Seed + 19, 3));
                outline[i] = b.At + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * wobble);
            }
            // the surface just under the lowest shore, so the water stays in; the bed down from it, shallow at the edge
            float level = outline.Min(p => At(h, cols, rows, p.X, p.Y)) - 0.4f, depth = MathF.Min(8, 2 + (radius / 40));
            int c0 = Math.Max(0, (int)(b.At.X - (radius * 1.4f))), c1 = Math.Min(cols - 1, (int)(b.At.X + (radius * 1.4f)));
            int r0 = Math.Max(0, (int)(b.At.Y - (radius * 1.4f))), r1 = Math.Min(rows - 1, (int)(b.At.Y + (radius * 1.4f)));
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    float inside = InsideBy(outline, new Vector2(c, r));
                    int p = (r * cols) + c;
                    if (inside >= 0)
                    {
                        h[p] = MathF.Min(h[p], level - (depth * Math.Clamp(inside / 15, 0.12f, 1)));
                    }
                    else if (inside > -10)
                    {
                        h[p] = MathF.Min(h[p], level + 0.3f + (-inside * 0.25f));   // a gentle shore
                    }
                }
            }
            lakes.Add(new Lake(b.At + new Vector2(x0, y0), [.. outline.Select(p => p + new Vector2(x0, y0))], level, depth));
        }
        return lakes;
    }

    private static float Radius(Lake l) => l.Outline.Max(p => Vector2.Distance(p, l.Middle));

    /// <summary>How wooded each point is (0..1): noise patches covering the asked share of the ground the woods may grow on.</summary>
    private static float[] Woods(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes)
    {
        float[] n = new float[cols * rows];
        var growable = new List<float>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                n[p] = Fbm((c + x0) / 160, (r + y0) / 160, spec.Seed + 31, 4);
                if ((r % 7 == 0) && (c % 7 == 0) && Grows(h, cols, rows, c, r, lakes, x0, y0))
                {
                    growable.Add(n[p]);
                }
            }
        }
        if (growable.Count == 0 || spec.Woods <= 0)
        {
            return new float[cols * rows];
        }
        growable.Sort();
        float cut = growable[Math.Clamp((int)((1 - spec.Woods) * growable.Count), 0, growable.Count - 1)];
        float[] w = new float[cols * rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                w[p] = Grows(h, cols, rows, c, r, lakes, x0, y0) ? Smooth(cut - 0.03f, cut + 0.03f, n[p]) : 0;
            }
        }
        return w;
    }

    /// <summary>Whether trees may stand at a point: not a cliff (woods climb steep hillsides), not in a lake.</summary>
    private static bool Grows(float[] h, int cols, int rows, int c, int r, List<Lake> lakes, float x0, float y0) =>
        Slope(h, cols, rows, c, r) < 45 && !lakes.Any(l => InsideBy(l.Outline, new Vector2(c + x0, r + y0)) > -4);

    /// <summary>The layers: rock on steep ground, dirt under the woods (and a little in the open), sand on the shores.</summary>
    private static byte[] Paint(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes, float[] wooded)
    {
        byte[] mask = new byte[cols * rows * 4];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float slope = Slope(h, cols, rows, c, r), x = c + x0, y = r + y0;
                float rock = Smooth(30, 45, slope) * (1 - (0.6f * wooded[p]));   // steep grass too, and wooded slopes mostly earth
                float patches = Smooth(0.35f, 0.6f, Fbm(x / 45, y / 45, spec.Seed + 41, 3));
                float dirt = MathF.Max(wooded[p] * 0.4f, patches * 0.35f) * (1 - rock);   // a forest floor, grass showing through
                float sand = 0;
                foreach (var l in lakes)
                {
                    // a band along the shore, out to Shore metres and a little way into the water
                    float d = -InsideBy(l.Outline, new Vector2(x, y));
                    if (spec.Shore > 0 && d > -4 && d < spec.Shore)
                    {
                        sand = MathF.Max(sand, 1 - Smooth(spec.Shore * 0.5f, spec.Shore, d));
                    }
                }
                sand *= 1 - rock;
                mask[p * 4] = Byte(sand);
                mask[(p * 4) + 1] = Byte(dirt * (1 - sand));
                mask[(p * 4) + 2] = Byte(rock);
                mask[(p * 4) + 3] = 255;
            }
        }
        return mask;
    }

    /// <summary>The trees (dense in the woods, a few in the open) and the small things (bushes and plants, rocks on steep ground).</summary>
    private static List<(string, Vector3, float, float)> Plant(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes, float[] wooded, Planting kinds, Random rng)
    {
        var placed = new List<(string, Vector3, float, float)>();
        float ha = cols * rows / 10000f;
        void Scatter(IReadOnlyList<MapTools.Kind> from, float perHa, Func<int, int, float> chance, float times = 1)
        {
            if (from.Count == 0 || perHa <= 0)
            {
                return;
            }
            int tries = (int)(perHa * ha);
            for (int k = 0; k < tries; k++)
            {
                float fc = rng.NextSingle() * (cols - 1), fr = rng.NextSingle() * (rows - 1);
                int c = (int)fc, r = (int)fr;
                if (rng.NextSingle() >= chance(c, r))
                {
                    continue;
                }
                var kind = from[rng.Next(from.Count)];
                float s = (kind.MinScale + (rng.NextSingle() * (kind.MaxScale - kind.MinScale))) * times;
                // editor axes: x = X, z = -Y
                placed.Add((kind.Model, new Vector3(fc + x0, At(h, cols, rows, fc, fr), -(fr + y0)), rng.NextSingle() * 360, s));
            }
        }
        bool Dry(int c, int r) => !lakes.Any(l => InsideBy(l.Outline, new Vector2(c + x0, r + y0)) > -2);
        Scatter(kinds.Trees, spec.WoodDensity, (c, r) => wooded[(r * cols) + c], spec.TreeScale);
        Scatter(kinds.Trees, spec.OpenTrees, (c, r) => wooded[(r * cols) + c] < 0.5f && Slope(h, cols, rows, c, r) < 25 && Dry(c, r) ? 1 : 0, spec.TreeScale);
        Scatter(kinds.Bushes, spec.Small * 0.35f, (c, r) => Dry(c, r) && Slope(h, cols, rows, c, r) < 30 ? 0.3f + (0.7f * wooded[(r * cols) + c]) : 0);
        Scatter(kinds.Plants, spec.Small * 0.45f, (c, r) => Dry(c, r) && Slope(h, cols, rows, c, r) < 25 ? 1 - (0.6f * wooded[(r * cols) + c]) : 0);
        Scatter(kinds.Rocks, spec.Small * 0.2f, (c, r) => Dry(c, r) ? 0.15f + (0.85f * Smooth(15, 35, Slope(h, cols, rows, c, r))) : 0);
        return placed;
    }

    /// <summary>The ground's slope at a point, degrees.</summary>
    internal static float Slope(float[] h, int cols, int rows, int c, int r)
    {
        int cl = Math.Max(0, c - 1), cr = Math.Min(cols - 1, c + 1), ru = Math.Max(0, r - 1), rd = Math.Min(rows - 1, r + 1);
        float dx = (h[(r * cols) + cr] - h[(r * cols) + cl]) / Math.Max(1, cr - cl), dy = (h[(rd * cols) + c] - h[(ru * cols) + c]) / Math.Max(1, rd - ru);
        return MathF.Atan(MathF.Sqrt((dx * dx) + (dy * dy))) * 180 / MathF.PI;
    }

    /// <summary>The height between the pixels.</summary>
    private static float At(float[] h, int cols, int rows, float c, float r)
    {
        c = Math.Clamp(c, 0, cols - 1.001f);
        r = Math.Clamp(r, 0, rows - 1.001f);
        int ci = (int)c, ri = (int)r;
        float tc = c - ci, tr = r - ri;
        float a = h[(ri * cols) + ci], b = h[(ri * cols) + ci + 1], d = h[((ri + 1) * cols) + ci], e = h[((ri + 1) * cols) + ci + 1];
        return ((a * (1 - tc)) + (b * tc)) * (1 - tr) + (((d * (1 - tc)) + (e * tc)) * tr);
    }

    /// <summary>How far inside an outline a point is (metres; negative outside), from its nearest edge.</summary>
    internal static float InsideBy(Vector2[] outline, Vector2 p) => MapWater.InsideBy(outline, p);

    /// <summary>Smooth value noise in octaves, about -1..1.</summary>
    internal static float Fbm(float x, float y, int seed, int octaves)
    {
        float v = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            v += amp * Noise(x, y, seed + (i * 101));
            norm += amp;
            (x, y, amp) = ((x * 2.03f) + 17.1f, (y * 2.03f) - 9.7f, amp * 0.5f);
        }
        return v / norm;
    }

    /// <summary>Ridged noise 0..1: sharp crests where the noise crosses zero (mountain ridges).</summary>
    private static float Ridged(float x, float y, int seed, int octaves)
    {
        float v = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1 - MathF.Abs(Noise(x, y, seed + (i * 53)));
            v += amp * n * n;
            norm += amp;
            (x, y, amp) = (x * 2.1f, y * 2.1f, amp * 0.5f);
        }
        return v / norm;
    }

    private static float Noise(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi, ux = fx * fx * (3 - (2 * fx)), uy = fy * fy * (3 - (2 * fy));
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return a + ((b - a) * ux) + ((c - a) * uy) + ((a - b - c + d) * ux * uy);
    }

    private static float Hash(int x, int y, int seed)
    {
        uint h = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
        h = (h ^ (h >> 13)) * 1274126177;
        return (((h ^ (h >> 16)) & 0xFFFF) / 32767.5f) - 1f;
    }

    private static float Smooth(float e0, float e1, float v)
    {
        float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
