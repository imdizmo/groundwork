using System.Numerics;
using Groundwork.App;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Maps made up for the tests: flat ground and the objects given.
/// </summary>
internal static class TestScenes
{
    /// <summary>
    /// A flat block of size × size zones at 30 m, with nothing edited, and its edit session.
    /// </summary>
    internal static EditSession Flat(int size = 2, params WorldScene.Thing[] things)
    {
        int w = size * 64 + 1;
        var ground = new Ground(w, w, 0, 0, size);
        Array.Fill(ground.Base, 30f);
        var scene = new WorldScene
        {
            Name = "test",
            X0 = 0,
            Z0 = 0,
            ZonesX = size,
            ZonesZ = size,
            W = w,
            H = w,
            Heights = [.. Enumerable.Repeat(30f, w * w)],
            Things = [.. things],
            Cx = -32 + (w - 1) / 2f,
            Cz = -32 + (w - 1) / 2f,
            Mask = new byte[w * w * 4],
        };
        var session = new EditSession(scene, ground);
        scene.Session = session;
        return session;
    }

    /// <summary>
    /// A flat 1 × 1 zone area at 35 m, with the given objects.
    /// </summary>
    internal static WorldScene FlatScene(params WorldScene.Thing[] things) => new()
    {
        Name = "test",
        X0 = 0,
        Z0 = 0,
        ZonesX = 1,
        ZonesZ = 1,
        W = 65,
        H = 65,
        Heights = [.. Enumerable.Repeat(35f, 65 * 65)],
        Things = [.. things],
        Mask = new byte[65 * 65 * 4],
    };

    /// <summary>
    /// A beech (Beech1) standing upright at full size at the point given, not yet saved in the world.
    /// </summary>
    internal static WorldScene.Thing Beech(int id, Vector3 at) => new(id, AssetIds.Of("Beech1"), at, Vector3.Zero, 1, false);

    /// <summary>
    /// A beech (see <see cref="Beech"/>) <paramref name="dx"/> and <paramref name="dz"/> metres from the origin of a flat
    /// one-zone area (Flat(1)), at this height.
    /// </summary>
    internal static WorldScene.Thing BeechInFlat(int id, float dx, float dz, float y = 30)
    {
        var probe = Flat(1).Scene;
        return Beech(id, new Vector3(probe.OriginX + dx, y, probe.OriginZ + dz));
    }
}
