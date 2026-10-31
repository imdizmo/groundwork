using Vector3 = System.Numerics.Vector3;

namespace Groundwork.Desktop;

/// <summary>
/// What the 3D view shows: a map's ground (heights and layer weights, edits included) over its grid, and its objects.
/// </summary>
/// <remarks>
/// Coordinates: the grid starts at its south-west corner (one point per metre, padded to whole 64 m blocks); the 3D
/// view puts the grid's middle at 0 and mirrors z (OpenGL is right-handed): view = (x - Cx, y, -(z - Cz)) in metres.
/// </remarks>
public sealed partial class WorldScene
{
    /// <summary>The map's name.</summary>
    public required string Name { get; init; }
    /// <summary>The block's south-west zone, x.</summary>
    public int X0 { get; init; }
    /// <summary>The block's south-west zone, z.</summary>
    public int Z0 { get; init; }
    /// <summary>The block's size in zones, west to east.</summary>
    public int ZonesX { get; init; }
    /// <summary>The block's size in zones, south to north.</summary>
    public int ZonesZ { get; init; }
    /// <summary>Grid points west to east (ZonesX × 64 + 1).</summary>
    public int W { get; init; }
    /// <summary>Grid points south to north (ZonesZ × 64 + 1).</summary>
    public int H { get; init; }
    /// <summary>The ground height in metres per grid point, edits included, row by row from the south (z × W + x).</summary>
    public required float[] Heights { get; init; }
    /// <summary>The map's layers' colours (linear): the first everywhere, the others painted over it (<see cref="Mask"/>).</summary>
    public IReadOnlyList<Vector3> LayerColours { get; init; } = [new(0.5f, 0.5f, 0.5f)];

    /// <summary>The ground colour (linear) a grid point is drawn with: the first layer's, the painted layers' over it by their weight.</summary>
    /// <param name="g">The grid point.</param>
    public Vector3 GroundColor(int g)
    {
        var c = LayerColours[0];
        for (int k = 1; k < LayerColours.Count; k++)
        {
            c = Vector3.Lerp(c, LayerColours[k], Mask[g * 4 + k - 1] / 255f);
        }
        return c;
    }

    /// <summary>The layer a grid point mostly is (0: the first, unless a painted one weighs at least half).</summary>
    /// <param name="g">The grid point.</param>
    public int LayerAt(int g)
    {
        int best = 0, most = 127;
        for (int k = 1; k < 4; k++)
        {
            if (Mask[g * 4 + k - 1] > most)
            {
                (best, most) = (k, Mask[g * 4 + k - 1]);
            }
        }
        return best;
    }

    /// <summary>World x of grid point 0 (the block's west edge: half a zone west of its first zone's middle).</summary>
    public float OriginX => X0 * 64f - 32f;
    /// <summary>World z of grid point 0 (the block's south edge).</summary>
    public float OriginZ => Z0 * 64f - 32f;
    /// <summary>World x of the block's middle, in metres.</summary>
    public float Cx { get; init; }
    /// <summary>World z of the block's middle, in metres.</summary>
    public float Cz { get; init; }
    /// <summary>The sea level in metres.</summary>
    public float Water { get; init; }
    /// <summary>
    /// The map's objects; only ever added to while it is open (see <see cref="Thing"/>). Lock it to read.
    /// </summary>
    public required List<Thing> Things { get; init; }

    /// <summary>
    /// How many times the things were read again (ids handed out before, to Claude, are out of date then); a map's
    /// stay read while it is open.
    /// </summary>
    public int Reads { get; set; }

    /// <summary>A map project's scene (MapProject): its tools are on the rail.</summary>
    public bool IsMap { get; init; }
    /// <summary>
    /// The painted layers' weights per grid point, RGBA bytes: red the second layer, green the third, blue the fourth
    /// (the paint where it was edited, the map's otherwise).
    /// </summary>
    public required byte[] Mask { get; init; }

    /// <summary>
    /// Where mods' extensions cut the ground, per grid point (1: not drawn, see GroundCuts), made with their shapes
    /// (GlView.RebuildShapes); empty until then.
    /// </summary>
    public float[] Cut { get; set; } = [];
    /// <summary>A line on how the block was read (timings, zones, objects), for the status bar.</summary>
    public string LoadInfo { get; init; } = "";
    /// <summary>Editing the ground (null in tests that only draw).</summary>
    public EditSession? Session { get; set; }

    /// <summary>
    /// An object of the map. Things are only ever added to the list, so indices stay valid while the map is open: a
    /// deleted one is kept, Gone, so undo can bring it back; a moved one is a new thing.
    /// </summary>
    /// <param name="Id">The object's id (negative: the editor's own, MapEdits holds its data).</param>
    /// <param name="Prefab">Its asset's number (AssetIds).</param>
    /// <param name="Position">Position in metres.</param>
    /// <param name="Rotation">Euler angles in degrees.</param>
    /// <param name="Scale">Uniform scale; zero when the asset's own scale applies.</param>
    /// <param name="Piece">A building part (shown with Buildings) rather than a plain object.</param>
    public readonly record struct Thing(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, bool Piece)
    {
        /// <summary>Deleted (or added and undone): kept so undo can bring it back, not drawn.</summary>
        public bool Gone { get; init; }
    }
}
