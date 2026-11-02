using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>Camps (buildings round levelled ground) and the roads between them.</summary>
public static partial class ZoneGenerator
{
    /// <summary>A camp: its middle (map pixels) and how far its levelled ground reaches.</summary>
    private readonly record struct Camp(Vector2 At, float Radius);

    /// <summary>
    /// The camps: each on open, fairly even, dry ground away from the edge and the others, its ground levelled to its
    /// own middle height and eased back into the ground round it.
    /// </summary>
    private static List<Camp> PlaceCamps(Spec spec, float[] h, int cols, int rows, float[] lakeIn, bool[] wet, Random rng)
    {
        var camps = new List<Camp>();
        float radius = 14 + (spec.CampSize * 2.5f), margin = (spec.RimHeight > 0 ? spec.RimWidth * 0.6f : 30) + radius;
        if (cols <= 2 * margin || rows <= 2 * margin)
        {
            return camps;
        }
        for (int k = 0; k < 2500 && camps.Count < spec.Camps; k++)
        {
            var at = new Vector2(margin + (rng.NextSingle() * (cols - (2 * margin))), margin + (rng.NextSingle() * (rows - (2 * margin))));
            if (camps.Any(c => Vector2.Distance(c.At, at) < 250))
            {
                continue;
            }
            // even enough: the ground round it within a few metres of its middle, nothing of it wet
            float mid = At(h, cols, rows, at.X, at.Y), spread = 0;
            bool dry = true;
            for (int i = 0; i < 12 && dry; i++)
            {
                float a = MathF.Tau * i / 12;
                var q = at + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
                int p = ((int)q.Y * cols) + (int)q.X;
                spread = MathF.Max(spread, MathF.Abs(At(h, cols, rows, q.X, q.Y) - mid));
                dry = !wet[p] && lakeIn[p] < -20;
            }
            int m = ((int)at.Y * cols) + (int)at.X;
            if (dry && !wet[m] && lakeIn[m] < -20 && spread < MathF.Min(20, 6 + (k / 150f)))   // pickier at first; rugged ground levels more
            {
                camps.Add(new Camp(at, radius));
            }
        }
        foreach (var c in camps)
        {
            float level = At(h, cols, rows, c.At.X, c.At.Y), reach = c.Radius + 30;
            for (int r = Math.Max(0, (int)(c.At.Y - reach)); r <= Math.Min(rows - 1, (int)(c.At.Y + reach)); r++)
            {
                for (int x = Math.Max(0, (int)(c.At.X - reach)); x <= Math.Min(cols - 1, (int)(c.At.X + reach)); x++)
                {
                    int p = (r * cols) + x;
                    h[p] += (level - h[p]) * Smooth(reach, c.Radius, Vector2.Distance(new(x, r), c.At));
                }
            }
        }
        return camps;
    }

    /// <summary>
    /// Roads joining the camps one to the next (nearest first), winding a little, the ground under them levelled along
    /// them (their own height smoothed over some 60 m) and eased into the ground beside; none across water. The road's
    /// paint strength goes into <paramref name="road"/>.
    /// </summary>
    private static List<List<Vector2>> MakeRoads(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Camp> camps, float[] lakeIn, bool[] wet, float[] road)
    {
        var roads = new List<List<Vector2>>();
        if (camps.Count < 2)
        {
            return roads;
        }
        var order = new List<Camp> { camps[0] };
        var left = camps.Skip(1).ToList();
        while (left.Count > 0)
        {
            var next = left.OrderBy(c => Vector2.Distance(c.At, order[^1].At)).First();
            order.Add(next);
            _ = left.Remove(next);
        }
        const float half = 3.5f, shoulder = 9;
        for (int n = 0; n + 1 < order.Count; n++)
        {
            var (a, b) = (order[n].At, order[n + 1].At);
            float length = Vector2.Distance(a, b);
            var side = Vector2.Normalize(new Vector2(-(b - a).Y, (b - a).X));
            int steps = Math.Max(2, (int)(length / 10));
            var path = new List<Vector2>();
            bool crosses = false;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = Vector2.Lerp(a, b, t) + (side * 45 * MathF.Sin(MathF.PI * t) * Fbm(t * 3, n * 7.1f, spec.Seed + 113, 2) / 0.4f);
                p = Vector2.Clamp(p, Vector2.One, new Vector2(cols - 2, rows - 2));
                int q = ((int)p.Y * cols) + (int)p.X;
                crosses |= wet[q] || lakeIn[q] > -6;
                path.Add(p);
            }
            if (crosses)
            {
                continue;
            }
            // its height: the ground along it, smoothed
            float[] ground = [.. path.Select(p => At(h, cols, rows, p.X, p.Y))];
            float[] level = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                int i0 = Math.Max(0, i - 3), i1 = Math.Min(path.Count - 1, i + 3);
                level[i] = ground[i0..(i1 + 1)].Average();
            }
            var near = new Dictionary<int, (float D, float S)>();
            float reach = half + shoulder;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var (p0, p1) = (path[i], path[i + 1]);
                for (int r = Math.Max(0, (int)(MathF.Min(p0.Y, p1.Y) - reach)); r <= Math.Min(rows - 1, (int)(MathF.Max(p0.Y, p1.Y) + reach)); r++)
                {
                    for (int c = Math.Max(0, (int)(MathF.Min(p0.X, p1.X) - reach)); c <= Math.Min(cols - 1, (int)(MathF.Max(p0.X, p1.X) + reach)); c++)
                    {
                        float d = MapWater.SegmentDistance(new Vector2(c, r), p0, p1, out float t);
                        int p = (r * cols) + c;
                        if (d <= reach && (!near.TryGetValue(p, out var was) || d < was.D))
                        {
                            near[p] = (d, level[i] + ((level[i + 1] - level[i]) * t));
                        }
                    }
                }
            }
            foreach (var (p, (d, s)) in near)
            {
                h[p] += (s - h[p]) * Smooth(reach, half, d);
                road[p] = MathF.Max(road[p], Smooth(half + 1.5f, half - 0.5f, d));
            }
            roads.Add([.. path.Select(p => p + new Vector2(x0, y0))]);
        }
        return roads;
    }

    /// <summary>Where nothing is planted: the water, the camps' ground and the roads.</summary>
    private static bool[] Busy(int cols, int rows, List<Camp> camps, float[] road, bool[] wet)
    {
        bool[] busy = (bool[])wet.Clone();
        for (int p = 0; p < busy.Length; p++)
        {
            busy[p] |= road[p] > 0.05f;
        }
        foreach (var c in camps)
        {
            for (int r = Math.Max(0, (int)(c.At.Y - c.Radius)); r <= Math.Min(rows - 1, (int)(c.At.Y + c.Radius)); r++)
            {
                for (int x = Math.Max(0, (int)(c.At.X - c.Radius)); x <= Math.Min(cols - 1, (int)(c.At.X + c.Radius)); x++)
                {
                    busy[(r * cols) + x] |= Vector2.Distance(new(x, r), c.At) <= c.Radius;
                }
            }
        }
        return busy;
    }

    /// <summary>The roads and the camps' trodden ground painted with a layer (its channel: 0 the first painted one, 1 the second).</summary>
    private static void PaintRoads(byte[] mask, float[] road, List<Camp> camps, int cols, int rows, int channel)
    {
        void Put(int p, float v)
        {
            if (v > mask[(p * 4) + channel] / 255f)
            {
                for (int k = 0; k < 3; k++)
                {
                    mask[(p * 4) + k] = k == channel ? Byte(v) : Byte(mask[(p * 4) + k] / 255f * (1 - v));
                }
            }
        }
        for (int p = 0; p < road.Length; p++)
        {
            if (road[p] > 0)
            {
                Put(p, road[p]);
            }
        }
        foreach (var c in camps)
        {
            for (int r = Math.Max(0, (int)(c.At.Y - c.Radius)); r <= Math.Min(rows - 1, (int)(c.At.Y + c.Radius)); r++)
            {
                for (int x = Math.Max(0, (int)(c.At.X - c.Radius)); x <= Math.Min(cols - 1, (int)(c.At.X + c.Radius)); x++)
                {
                    Put((r * cols) + x, 0.8f * Smooth(c.Radius, c.Radius * 0.6f, Vector2.Distance(new(x, r), c.At)));
                }
            }
        }
    }

    /// <summary>Each camp's buildings in a ring round its middle, facing in, and its props about the middle.</summary>
    private static void Furnish(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Camp> camps, Planting kinds, Random rng, List<(string, Vector3, float, float)> objects)
    {
        if (kinds.Buildings.Count == 0)
        {
            return;
        }
        foreach (var c in camps)
        {
            float turn = rng.NextSingle() * MathF.Tau;
            for (int i = 0; i < spec.CampSize; i++)
            {
                float a = turn + (MathF.Tau * i / spec.CampSize) + ((rng.NextSingle() - 0.5f) * 0.4f), d = c.Radius * (0.5f + (0.15f * rng.NextSingle()));
                var at = c.At + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * d);
                var kind = kinds.Buildings[rng.Next(kinds.Buildings.Count)];
                float yaw = 90 - (a * 180 / MathF.PI);   // facing the middle
                objects.Add((kind.Model, new Vector3(at.X + x0, At(h, cols, rows, at.X, at.Y), -(at.Y + y0)), yaw, kind.MinScale + (rng.NextSingle() * (kind.MaxScale - kind.MinScale))));
            }
            for (int i = 0; kinds.Props.Count > 0 && i < 3 + rng.Next(4); i++)
            {
                var at = c.At + (new Vector2(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * c.Radius * 0.6f);
                var kind = kinds.Props[rng.Next(kinds.Props.Count)];
                objects.Add((kind.Model, new Vector3(at.X + x0, At(h, cols, rows, at.X, at.Y), -(at.Y + y0)), rng.NextSingle() * 360, kind.MinScale + (rng.NextSingle() * (kind.MaxScale - kind.MinScale))));
            }
        }
    }
}
