using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Beds: a bedded object's ground rises to its underside, moves with it, comes back when it goes, and undoes.</summary>
public class EditSessionBedsTests
{
    /// <summary>A flat 65 x 65 m map at 30 m where prefab 7 is bedded: a slab 10 m square and 2 m thick, 1 m over its origin.</summary>
    private static EditSession Session()
    {
        var s = TestScenes.Flat(1);
        s.Bedded = prefab => prefab == 7;
        float[] verts = [.. new (float X, float Y, float Z)[] { (-5, 1, -5), (5, 1, -5), (5, 1, 5), (-5, 1, 5), (-5, 3, -5), (5, 3, -5), (5, 3, 5), (-5, 3, 5) }.SelectMany(p => new float[] { p.X, p.Y, p.Z, 0, 1, 0, 0, 0 })];
        uint[] tris = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7];
        s.MeshesOf = prefab => prefab == 7 ? ([(verts, tris, Matrix4x4.Identity)], Vector3.One) : null;
        return s;
    }

    private static float At(EditSession s, float x, float z) => s.Ground.HeightOf(((int)(z - s.Ground.OriginZ) * s.Ground.W) + (int)(x - s.Ground.OriginX));

    [Fact]
    public void ABedRisesToTheUndersideMovesWithTheObjectAndComesBackWhenItGoes()
    {
        var s = Session();
        var added = s.Commit("placed", null, [], [(new NewObject(0, 7, new Vector3(0, 30, 0), Vector3.Zero, 1), false)]);
        Assert.Equal(31, At(s, 0, 0), 2);
        Assert.Equal(31, At(s, 4, -4), 2);
        Assert.True(At(s, 6, 0) is > 30 and < 31, $"the skirt: {At(s, 6, 0)}");
        Assert.Equal(30, At(s, 20, 0), 3);
        Assert.True(s.Lifted.Count > 100);
        // moved 20 m east in one step: the old bed gone, a new one there
        var moved = s.Move([(added[0], new Vector3(20, 30, 0), Vector3.Zero)]);
        Assert.Equal(30, At(s, 0, 0), 3);
        Assert.Equal(31, At(s, 20, 0), 2);
        Assert.True(s.Undo());
        Assert.Equal(31, At(s, 0, 0), 2);
        Assert.Equal(30, At(s, 20, 0), 3);
        Assert.True(s.Redo());
        Assert.Equal(30, At(s, 0, 0), 3);
        Assert.Equal(31, At(s, 20, 0), 2);
        // deleted: flat again, nothing kept
        s.Delete([moved[0]]);
        Assert.Equal(30, At(s, 20, 0), 3);
        Assert.Empty(s.Lifted);
        Assert.True(s.Undo());
        Assert.Equal(31, At(s, 20, 0), 2);
    }

    [Fact]
    public void AnUnbeddedKindLeavesTheGroundAlone()
    {
        var s = Session();
        _ = s.Commit("placed", null, [], [(new NewObject(0, 8, new Vector3(0, 30, 0), Vector3.Zero, 1), false)]);
        Assert.Equal(30, At(s, 0, 0), 3);
        Assert.Empty(s.Lifted);
    }
}
