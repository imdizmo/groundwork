namespace Groundwork.Desktop;

public static partial class Sculpt
{
    /// <summary>
    /// A paint over the whole map (the locked edge aside), as much as the mask lets it at each point (no mask: fully):
    /// the points changed and the grid box round them.
    /// </summary>
    internal static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect) FillPaint(Ground g, float[] colour, Func<int, float>? mask)
    {
        var touched = new List<int>();
        for (int gz = 1; gz < g.H - 1; gz++)
        {
            for (int gx = 1; gx < g.W - 1; gx++)
            {
                int p = (gz * g.W) + gx;
                float w = mask?.Invoke(p) ?? 1;
                if (w > 0)
                {
                    PaintPoint(g, p, colour, MathF.Min(1, w));
                    touched.Add(p);
                }
            }
        }
        return (touched, (0, 0, g.W - 1, g.H - 1));
    }
}

public static partial class Sculpt
{
    /// <summary>
    /// Terrace: the height a point is pulled to, shelves a step apart joined by short steep rises (the top third of
    /// each step).
    /// </summary>
    internal static float TerraceTarget(float h, float step)
    {
        float k = MathF.Floor(h / step), f = (h / step) - k, t = Math.Clamp((f - 0.65f) / 0.35f, 0, 1);
        return (k + (t * t * (3 - (2 * t)))) * step;
    }

    /// <summary>
    /// Erosion over the whole map in one go, as much as the mask lets it at each point: Thermal (slopes slide to the
    /// rest angle) or Water (rain), at a strength 0..1. The points changed and the grid box round them.
    /// </summary>
    internal static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect) ErodeWhole(Ground g, bool water, float restAngle, float strength, Func<int, float>? mask, Random random)
    {
        var touched = new List<int>();
        var rect = Erosion.Run(g, 0, 0, g.W - 1, g.H - 1, (_, _, p) => mask?.Invoke(p) ?? 1, (h, wt, w, d) =>
        {
            if (water)
            {
                Erosion.Hydraulic(h, wt, w, d, (int)(w * d * strength * 0.4f), 1, random);
            }
            else
            {
                Erosion.Thermal(h, wt, w, d, Erosion.Talus(restAngle), 1, 4 + (int)(strength * 36));
            }
        }, touched);
        return (touched, rect);
    }
}
