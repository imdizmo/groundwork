using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// A path or road along a line of points: the ground under it levelled to its own slope (cut and fill), its shoulders
/// blended into the ground round it, one of the map's layers painted on it, objects (posts, a fence) along its edges.
/// Two points with an even slope make a ramp.
/// </summary>
public static class Paths
{
    /// <summary>
    /// How a path is made: its width and shoulders (m); Even: straight slopes between its points, else the ground's own
    /// heights smoothed over Smoothing metres; the layer painted (-1: none); the model put along its edges every Spacing
    /// metres, Offset past the edge, on both sides or the right one.
    /// </summary>
    public sealed record Spec(float Width, float Shoulder, bool Even, float Smoothing, int Layer, string? Edge, float Spacing, float Offset, bool BothSides);

    /// <summary>The path's ground: the points changed and the grid box around them; its edge objects go into adds.</summary>
    /// <param name="g">The ground.</param>
    /// <param name="points">The line, world (x, z) in metres.</param>
    /// <param name="spec">How it is made.</param>
    /// <param name="adds">Gets the edge objects, standing on the ground as it is after.</param>
    public static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect) Apply(Ground g, IReadOnlyList<Vector2> points, Spec spec, List<(NewObject Object, bool Piece)> adds)
    {
        var (at, heights) = Profile(points, (x, z) => HeightAt(g, x, z), spec.Even, spec.Smoothing);
        if (at.Count < 2)
        {
            return ([], (0, 0, 0, 0));
        }
        // each ground point near the line: its distance to the nearest sample and that sample's height
        float half = spec.Width / 2, reach = half + spec.Shoulder;
        var near = new Dictionary<int, (float D, float H)>();
        for (int i = 0; i < at.Count; i++)
        {
            float cx = at[i].X - g.OriginX, cz = at[i].Y - g.OriginZ;
            for (int gz = Math.Max(0, (int)MathF.Floor(cz - reach)); gz <= Math.Min(g.H - 1, (int)MathF.Ceiling(cz + reach)); gz++)
            {
                for (int gx = Math.Max(0, (int)MathF.Floor(cx - reach)); gx <= Math.Min(g.W - 1, (int)MathF.Ceiling(cx + reach)); gx++)
                {
                    float d = MathF.Sqrt(((gx - cx) * (gx - cx)) + ((gz - cz) * (gz - cz)));
                    int p = (gz * g.W) + gx;
                    if (d <= reach && !g.Locked(gx, gz) && (!near.TryGetValue(p, out var best) || d < best.D))
                    {
                        near[p] = (d, heights[i]);
                    }
                }
            }
        }
        float[]? colour = spec.Layer >= 0 ? Brush.PaintOf(Brush.PaintFor(spec.Layer)) : null;
        var touched = new List<int>();
        int x0 = int.MaxValue, z0 = int.MaxValue, x1 = 0, z1 = 0;
        foreach (var (p, (d, h)) in near)
        {
            float w = d <= half ? 1 : 1 - Smooth(half, reach, d), now = g.HeightOf(p);
            bool lifted = MathF.Abs(h - now) * w > 0.005f;
            if (lifted)
            {
                g.SetHeight(p, now + ((h - now) * w));
            }
            bool painted = colour != null && d <= half + 0.5f;
            if (painted)
            {
                Sculpt.PaintPoint(g, p, colour!, 1 - Smooth(half - 0.5f, half + 0.5f, d));
            }
            if (lifted || painted)
            {
                touched.Add(p);
                int gx = p % g.W, gz = p / g.W;
                (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
            }
        }
        if (spec.Edge is { } model && spec.Spacing > 0)
        {
            Edges(g, points, spec, model, adds);
        }
        return (touched, touched.Count > 0 ? (x0 - 1, z0 - 1, x1 + 1, z1 + 1) : (0, 0, 0, 0));
    }

    /// <summary>
    /// Samples along the line half a metre apart and the path's height at each: straight between the points' own
    /// ground heights (even), or the ground's heights averaged over smoothing metres, less towards the ends so they meet
    /// the ground.
    /// </summary>
    internal static (List<Vector2> At, float[] Heights) Profile(IReadOnlyList<Vector2> points, Func<float, float, float> ground, bool even, float smoothing)
    {
        const float Step = 0.5f;
        var at = new List<Vector2>();
        var even1 = new List<float>();
        for (int k = 0; k + 1 < points.Count; k++)
        {
            var (a, b) = (points[k], points[k + 1]);
            float ha = ground(a.X, a.Y), hb = ground(b.X, b.Y), len = Vector2.Distance(a, b);
            int n = Math.Max(1, (int)MathF.Ceiling(len / Step));
            for (int i = k == 0 ? 0 : 1; i <= n; i++)
            {
                at.Add(Vector2.Lerp(a, b, i / (float)n));
                even1.Add(ha + ((hb - ha) * i / n));
            }
        }
        if (even || at.Count == 0)
        {
            return (at, [.. even1]);
        }
        float[] raw = [.. at.Select(p => ground(p.X, p.Y))], smoothed = new float[raw.Length];
        int window = Math.Max(1, (int)(smoothing / Step / 2));
        for (int i = 0; i < raw.Length; i++)
        {
            int r = Math.Min(window, Math.Min(i, raw.Length - 1 - i));
            float sum = 0;
            for (int j = i - r; j <= i + r; j++)
            {
                sum += raw[j];
            }
            smoothed[i] = sum / ((2 * r) + 1);
        }
        return (at, smoothed);
    }

    /// <summary>The edge objects: every spacing metres along the line, offset past its edge, facing along it.</summary>
    private static void Edges(Ground g, IReadOnlyList<Vector2> points, Spec spec, string model, List<(NewObject Object, bool Piece)> adds)
    {
        float carry = 0;   // how far into the next segment the next object is
        for (int k = 0; k + 1 < points.Count; k++)
        {
            var (a, b) = (points[k], points[k + 1]);
            float len = Vector2.Distance(a, b);
            if (len < 1e-3f)
            {
                continue;
            }
            var dir = (b - a) / len;
            var side = new Vector2(dir.Y, -dir.X) * ((spec.Width / 2) + spec.Offset);   // to the right, walking along it
            float yaw = MathF.Atan2(dir.X, dir.Y) * 180 / MathF.PI;
            float s = carry;
            for (; s < len - 1e-3f; s += spec.Spacing)
            {
                foreach (int sign in spec.BothSides ? (int[])[1, -1] : [1])
                {
                    var q = a + (dir * s) + (side * sign);
                    adds.Add((new NewObject(0, Groundwork.App.AssetIds.Of(model), new Vector3(q.X, HeightAt(g, q.X, q.Y), q.Y), new Vector3(0, yaw, 0), 0), false));
                }
            }
            carry = s - len;
        }
        // and one at the far end, where a fence needs its last post
        var (y0, y1) = (points[^2], points[^1]);
        if (Vector2.Distance(y0, y1) >= 1e-3f)
        {
            var dir = Vector2.Normalize(y1 - y0);
            var side = new Vector2(dir.Y, -dir.X) * ((spec.Width / 2) + spec.Offset);
            foreach (int sign in spec.BothSides ? (int[])[1, -1] : [1])
            {
                var q = y1 + (side * sign);
                adds.Add((new NewObject(0, Groundwork.App.AssetIds.Of(model), new Vector3(q.X, HeightAt(g, q.X, q.Y), q.Y), new Vector3(0, MathF.Atan2(dir.X, dir.Y) * 180 / MathF.PI, 0), 0), false));
            }
        }
    }

    /// <summary>The ground's height at a world point, between the grid's points.</summary>
    internal static float HeightAt(Ground g, float x, float z)
    {
        float fx = Math.Clamp(x - g.OriginX, 0, g.W - 1.001f), fz = Math.Clamp(z - g.OriginZ, 0, g.H - 1.001f);
        int gx = (int)fx, gz = (int)fz;
        float tx = fx - gx, tz = fz - gz;
        float h00 = g.HeightOf((gz * g.W) + gx), h10 = g.HeightOf((gz * g.W) + gx + 1), h01 = g.HeightOf(((gz + 1) * g.W) + gx), h11 = g.HeightOf(((gz + 1) * g.W) + gx + 1);
        return ((h00 * (1 - tx)) + (h10 * tx)) * (1 - tz) + (((h01 * (1 - tx)) + (h11 * tx)) * tz);
    }

    /// <summary>A smooth step from 0 at e0 to 1 at e1.</summary>
    private static float Smooth(float e0, float e1, float v)
    {
        float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - (2 * t));
    }
}
