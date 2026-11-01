using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Whole-map passes: a layer painted over the whole map where the mask lets it.</summary>
public class WholeMapTests
{
    [Fact]
    public void FillPaintPaintsEveryPointButTheLockedEdge()
    {
        var g = TestScenes.Flat(1).Ground;
        var (touched, _) = Sculpt.FillPaint(g, [1f, 0, 0, 1], null);
        Assert.Equal((g.W - 2) * (g.H - 2), touched.Count);
        Assert.Equal(1, g.MaskOf((10 * g.W) + 10, 0), 0.001f);
        Assert.Equal(0, g.MaskOf(0, 0), 0.001f);
    }

    [Fact]
    public void FillPaintFollowsTheMask()
    {
        var g = TestScenes.Flat(1).Ground;
        var (touched, _) = Sculpt.FillPaint(g, [0f, 1, 0, 1], p => p % g.W < 32 ? 1 : 0);
        Assert.All(touched, p => Assert.True(p % g.W < 32));
        Assert.Equal(1, g.MaskOf((10 * g.W) + 10, 1), 0.001f);
        Assert.Equal(0, g.MaskOf((10 * g.W) + 40, 1), 0.001f);
    }
}
