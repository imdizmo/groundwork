using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Paths: the ground levelled along a line, its shoulders blended, a layer painted, objects along its edges.</summary>
public class PathsTests
{
    // TestScenes.Flat: ground 30 m high, grid point (gx, gz) at world (gx - 32, gz - 32)
    private static int P(Ground g, float x, float z) => ((int)(z + 32) * g.W) + (int)(x + 32);

    private static Paths.Spec Spec(bool even = true, int layer = -1, string? edge = null) => new(Width: 4, Shoulder: 3, Even: even, Smoothing: 20, Layer: layer, Edge: edge, Spacing: 5, Offset: 1, BothSides: true);

    /// <summary>Raises a square of ground to a height (its Base: as the map was opened).</summary>
    private static void Hill(Ground g, float x, float z, int r, float h)
    {
        for (int dz = -r; dz <= r; dz++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                g.Base[P(g, x + dx, z + dz)] = h;
            }
        }
    }

    [Fact]
    public void AnEvenPathRampsStraightBetweenItsPoints()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        Hill(g, 30, 0, 3, 40);
        _ = s.EditGround("Path", gr => Paths.Apply(gr, [new(-10, 0), new(30, 0)], Spec(), []));
        Assert.Equal(35, g.HeightOf(P(g, 10, 0)), 0.3f);
        Assert.Equal(32.5f, g.HeightOf(P(g, 0, 1)), 0.3f);
    }

    [Fact]
    public void TheShoulderBlendsAndGroundBeyondItStays()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        Hill(g, 30, 0, 3, 40);
        _ = s.EditGround("Path", gr => Paths.Apply(gr, [new(-10, 0), new(30, 0)], Spec(), []));
        float edge = g.HeightOf(P(g, 10, 2)), shoulder = g.HeightOf(P(g, 10, 4)), beyond = g.HeightOf(P(g, 10, 6));
        Assert.Equal(35, edge, 0.3f);
        Assert.InRange(shoulder, 30.2f, 34.8f);
        Assert.Equal(30, beyond, 0.01f);
    }

    [Fact]
    public void AFollowingPathSmoothsABumpAndKeepsItsEnds()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        Hill(g, 0, 0, 0, 33);
        _ = s.EditGround("Path", gr => Paths.Apply(gr, [new(-20, 0), new(20, 0)], Spec(even: false), []));
        Assert.True(g.HeightOf(P(g, 0, 0)) < 30.6f);
        Assert.Equal(30, g.HeightOf(P(g, -20, 0)), 0.05f);
    }

    [Fact]
    public void ItPaintsItsLayerOnThePathOnly()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        _ = s.EditGround("Path", gr => Paths.Apply(gr, [new(-10, 0), new(10, 0)], Spec(layer: 1), []));
        Assert.Equal(1, g.MaskOf(P(g, 0, 0), 0), 0.01f);
        Assert.Equal(0, g.MaskOf(P(g, 0, 5), 0), 0.01f);
    }

    [Fact]
    public void ItPutsObjectsAlongBothEdgesFacingAlongIt()
    {
        var s = TestScenes.Flat(2);
        var adds = new List<(NewObject Object, bool Piece)>();
        _ = s.EditGround("Path", gr => Paths.Apply(gr, [new(-10, 0), new(10, 0)], Spec(edge: "Post"), adds));
        Assert.Equal(10, adds.Count);   // every 5 m over 20 m: 5 a side
        Assert.All(adds, a => Assert.Equal(3, MathF.Abs(a.Object.Position.Z), 0.01f));   // half the width + the offset
        Assert.All(adds, a => Assert.Equal(30, a.Object.Position.Y, 0.05f));
        Assert.All(adds, a => Assert.Equal(90, a.Object.Rotation.Y, 0.5f));   // along +x
    }

    [Fact]
    public void OnePointMakesNothing()
    {
        var s = TestScenes.Flat(2);
        var touched = s.EditGround("Path", gr => Paths.Apply(gr, [new(0, 0)], Spec(layer: 1), []));
        Assert.Empty(touched);
    }
}
