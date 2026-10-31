using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>Pasting onto the ground (PasteTool.Apply.cs): the copy is laid over the ground and its objects added.</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Pastes at a grid point: the ground (shape and paint, with the copy's soft edge) and the objects, as new objects
    /// with the data they held. Returns the ground points changed and their rectangle, and the objects to add (Piece: a
    /// building part).
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="at">The grid point pasted at.</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, List<(NewObject, bool)> Add) Apply(Ground g, Vector2 at, Func<int, float>? mask = null)
    {
        var c = Clip!;
        var touched = new List<int>();
        var add = new List<(NewObject, bool)>();
        var shaped = new HashSet<int>();
        int half = (int)MathF.Ceiling(MathF.Sqrt(c.W * c.W + c.H * c.H) / 2) + 1;
        int bx0 = g.W, bx1 = 0, bz0 = g.H, bz1 = 0;
        float baseH = g.HeightOf(Index(g, at)) + Offset;
        bool up = Direction == Along.Up;
        // Each copy's height read from the ground as it is before any copy shapes it, as the preview reads it: copies
        // overlapping (a negative gap) would otherwise stand on what the copy before them pasted.
        float[] anchors = new float[Count];
        for (int k = 0; k < Count; k++)
        {
            var (ca, lift) = CopyAt(at, k);
            anchors[k] = (up ? baseH : g.HeightOf(Index(g, ca)) + Offset) + lift;
        }
        for (int k = 0; k < Count; k++)
        {
            var (ca, lift) = CopyAt(at, k);
            float anchor = anchors[k];
            int x0 = Math.Max(1, (int)MathF.Floor(ca.X - half)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(ca.X + half));
            int z0 = Math.Max(1, (int)MathF.Floor(ca.Y - half)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(ca.Y + half));
            // Stacked upwards, only the first copy shapes the ground.
            if (Ground && !(up && k > 0))
            {
                (bx0, bx1, bz0, bz1) = (Math.Min(bx0, x0), Math.Max(bx1, x1), Math.Min(bz0, z0), Math.Max(bz1, z1));
                ShapeGround(g, c, ca, anchor, x0, x1, z0, z1, mask, shaped, touched);
            }
            if (Objects)
            {
                AddObjects(g, c, ca, lift, anchor, add);
            }
        }
        return (touched, (Math.Min(bx0, bx1) - 1, Math.Min(bz0, bz1) - 1, bx1 + 1, bz1 + 1), add);
    }

    /// <summary>The ground point nearest a grid position, clamped to the ground.</summary>
    private static int Index(Ground g, Vector2 p) => Math.Clamp((int)MathF.Round(p.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(p.X), 0, g.W - 1);
}
