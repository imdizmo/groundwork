using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Holes: ground the Hole brush takes away (Unreal's landscape visibility), kept apart from the paint.</summary>
public class HolesTests
{
    [Fact]
    public void TheHoleBrushMakesAHoleAndFillsItBack()
    {
        var g = TestScenes.Flat(1).Ground;
        int p = (32 * g.W) + 32;
        Sculpt.HolePoint(g, p, 1, fill: false);
        Assert.True(g.MaskOf(p, 3) > 0.5f);
        Sculpt.HolePoint(g, p, 1, fill: true);
        Assert.True(g.MaskOf(p, 3) < 0.5f);
    }

    [Fact]
    public void PaintingDoesNotFillAHole()
    {
        var g = TestScenes.Flat(1).Ground;
        int p = (32 * g.W) + 32;
        Sculpt.HolePoint(g, p, 1, fill: false);
        Sculpt.PaintPoint(g, p, [1f, 0, 0, 1], 1);
        Assert.Equal(1, g.MaskOf(p, 0), 0.001f);
        Assert.True(g.MaskOf(p, 3) > 0.5f);
    }

    [Fact]
    public void AHoleIsNotDrawn()
    {
        var s = TestScenes.Flat(1);
        int p = (32 * s.Ground.W) + 32;
        _ = s.EditGround("Hole", g =>
        {
            Sculpt.HolePoint(g, p, 1, fill: false);
            return ([p], (31, 31, 33, 33));
        });
        float[] rows = GlView.TerrainRows(s.Scene, 32, 32);
        Assert.True(rows[(32 * 9) + 6] < 0);    // the hole's colour says "not drawn"
        Assert.True(rows[(31 * 9) + 6] >= 0);   // its neighbour is ground
    }
}
