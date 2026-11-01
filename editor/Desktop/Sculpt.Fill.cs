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
