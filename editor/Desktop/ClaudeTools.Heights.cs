namespace Groundwork.Desktop;

/// <summary>
/// The ground's height over a rectangle or a point of the open area, for building on it.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// The ground's height at a world point of the area, between the grid points as ground_at gives it (Picking.HeightAt),
    /// the point held to the area's edge (a coordinate that is not a number: its west or south edge).
    /// </summary>
    private static float GroundAt(WorldScene scene, float x, float z)
    {
        float hx = (scene.W - 1) / 2f, hz = (scene.H - 1) / 2f, vx = x - scene.Cx, vz = z - scene.Cz;
        vx = vx > -hx ? (vx < hx ? vx : hx) : -hx;
        vz = vz > -hz ? (vz < hz ? vz : hz) : -hz;
        return Picking.HeightAt(scene, vx, -vz);
    }
}
