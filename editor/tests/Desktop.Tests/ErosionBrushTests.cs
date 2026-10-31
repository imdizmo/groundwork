using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Erode brush and the drop speed: a drop slows going uphill and speeds up going downhill, and one Erode stroke, and its key and area action.
/// </summary>
public class ErosionBrushTests
{
    [Fact]
    public void ADropSlowsGoingUphillAndSpeedsUpGoingDownhill()
    {
        Assert.True(Erosion.DropSpeed(2, 0.5f) < 2);
        Assert.True(Erosion.DropSpeed(2, -0.5f) > 2);
        Assert.Equal(0, Erosion.DropSpeed(1, 10));
    }

    [Fact]
    public void TheErodeBrushIsOneStroke()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("d < 4 ? 6 : 0", ["d"]), 4, 0, "tower");
        float top = s.Scene.Heights[64 * s.Scene.W + 64];
        s.Brush.Radius = 10;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Erode, 64, 64);
        for (int i = 0; i < 30; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        Assert.True(s.Scene.Heights[64 * s.Scene.W + 64] < top - 0.5f);
        Assert.True(s.Scene.Heights[64 * s.Scene.W + 69] > 30.1f);
        Assert.Equal("Erode", s.UndoLabel);
    }
}
