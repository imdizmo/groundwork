
namespace Groundwork.Desktop;

public sealed partial class Ground
{
    /// <summary>The height of a point: the map's ground, plus its edit where it has one.</summary>
    public float HeightOf(int g) => Base[g] + (Mod[g] != 0 ? Level[g] + Smooth[g] : 0);

    /// <summary>The map's ground at a point, as it was opened.</summary>
    public float Original(int g) => Base[g];

    /// <summary>The outer line of points stays as it is, so the block always joins its neighbours.</summary>
    public bool Locked(int gx, int gz) => gx == 0 || gz == 0 || gx == W - 1 || gz == H - 1;

    /// <summary>Moves a point to a height (its smoothing part kept, the level part takes the rest).</summary>
    public void SetHeight(int g, float h)
    {
        if (Mod[g] == 0)
        {
            Smooth[g] = 0;
            Level[g] = 0;
            Mod[g] = 1;
        }
        Level[g] = h - Base[g] - Smooth[g];
    }

    /// <summary>
    /// The layers' weight at a point: the paint where painted, the map's otherwise.
    /// </summary>
    public float MaskOf(int g, int c) => PMod[g] != 0 ? Paint[(g * C) + c] : BaseMask[(g * C) + c];

    /// <summary>
    /// Marks a point's paint as changed, starting from what it shows: the map's paint when it was not painted yet, else
    /// the paint it has.
    /// </summary>
    /// <param name="g">The ground point index.</param>
    public void StartPaint(int g)
    {
        if (PMod[g] == 0)
        {
            Array.Copy(BaseMask, g * C, Paint, g * C, C);
            PMod[g] = 1;
        }
    }

    /// <summary>
    /// The editable arrays, for every point (Snapshot) or only some points (an undo step keeps just the ones it
    /// touched).
    /// </summary>
    public sealed record State(float[] Level, float[] Smooth, byte[] Mod, float[] Paint, byte[] PMod);

    /// <summary>A copy of the editable arrays as they are now.</summary>
    public State Snapshot() => new((float[])Level.Clone(), (float[])Smooth.Clone(), (byte[])Mod.Clone(), (float[])Paint.Clone(), (byte[])PMod.Clone());
}
