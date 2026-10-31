
namespace Groundwork.Desktop;

/// <summary>
/// The ground being edited, one point a metre: per point the map's height (Base), its edit (Level and Smooth, used where
/// Mod is set), the map's paint (BaseMask) and the paint over it (Paint, used where PMod is set). The grid is the map's
/// heightmap padded to whole 64 m blocks (X0, Z0, ZonesX, ZonesZ).
/// </summary>
public sealed partial class Ground
{
    /// <summary>
    /// Grid points across (ZonesX × 64 + 1 when read from zones: neighbouring zones share their edge points).
    /// </summary>
    public int W { get; }
    /// <summary>Grid points down (ZonesZ × 64 + 1), as <c>W</c>.</summary>
    public int H { get; }
    /// <summary>The block's first zone (world zone X).</summary>
    public int X0 { get; }
    /// <summary>The block's first zone (world zone Z).</summary>
    public int Z0 { get; }
    /// <summary>World x of grid point 0 (the block's west edge), as WorldScene.OriginX.</summary>
    public float OriginX => X0 * 64f - 32f;
    /// <summary>World z of grid point 0 (the block's south edge).</summary>
    public float OriginZ => Z0 * 64f - 32f;
    /// <summary>The block's size in zones, west to east.</summary>
    public int ZonesX { get; }
    /// <summary>The block's size in zones, south to north.</summary>
    public int ZonesZ { get; }
    /// <summary>The generated height per point, in metres.</summary>
    public float[] Base { get; }
    /// <summary>The level change per point (metres, used where <c>Mod</c> is set).</summary>
    public float[] Level { get; }
    /// <summary>The smoothing change per point (metres, used where <c>Mod</c> is set).</summary>
    public float[] Smooth { get; }
    /// <summary>
    /// The paint, four channels per point (dirt, cultivated, paved, vegetation), used where <c>PMod</c> is set.
    /// </summary>
    public float[] Paint { get; }
    /// <summary>The generated paint mask, four channels per point, shown where the point is not painted.</summary>
    public float[] BaseMask { get; }
    /// <summary>1 where the point's height was changed.</summary>
    public byte[] Mod { get; }
    /// <summary>1 where the point was painted.</summary>
    public byte[] PMod { get; }

    /// <summary>An empty, unchanged ground of <paramref name="w"/> × <paramref name="h"/> points.</summary>
    /// <param name="w">Points across.</param>
    /// <param name="h">Points down.</param>
    /// <param name="x0">The block's first zone (X).</param>
    /// <param name="z0">The block's first zone (Z).</param>
    /// <param name="zonesX">The block's size in zones, west to east.</param>
    /// <param name="zonesZ">The block's size in zones, south to north.</param>
    public Ground(int w, int h, int x0, int z0, int zonesX, int zonesZ)
    {
        W = w;
        H = h;
        X0 = x0;
        Z0 = z0;
        ZonesX = zonesX;
        ZonesZ = zonesZ;
        int n = w * h;
        Base = new float[n];
        Level = new float[n];
        Smooth = new float[n];
        Mod = new byte[n];
        Paint = new float[n * 4];
        PMod = new byte[n];
        BaseMask = new float[n * 4];
    }

    /// <summary>An empty, unchanged ground of a square block of <paramref name="size"/> × <paramref name="size"/> zones.</summary>
    /// <param name="w">Points across.</param>
    /// <param name="h">Points down.</param>
    /// <param name="x0">The block's first zone (X).</param>
    /// <param name="z0">The block's first zone (Z).</param>
    /// <param name="size">The block's size in zones, both ways.</param>
    public Ground(int w, int h, int x0, int z0, int size)
        : this(w, h, x0, z0, size, size)
    {
    }
}
