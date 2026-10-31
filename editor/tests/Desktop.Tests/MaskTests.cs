using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Mask: brushes, paths, shapes and area actions only change ground that matches it.
/// </summary>
public class MaskTests
{
    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// Flat at 30 m, with the east half (x &gt;= 64) mostly the third layer (painted green) and the west the first.
    /// </summary>
    private static EditSession Area()
    {
        var s = TestScenes.Flat(2);
        for (int i = 0; i < s.Scene.W * s.Scene.H; i++)
        {
            s.Scene.Mask[i * 4 + 1] = (byte)(i % s.Scene.W >= 64 ? 255 : 0);
        }
        return s;
    }

    [Fact]
    public void LayersLimitABrush()
    {
        var s = Area();
        s.Mask.On = true;
        s.Mask.Layers.Add(2);
        s.Brush.Radius = 10;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        for (int i = 0; i < 20; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        Assert.Equal("", s.EndStroke().Shown);
        Assert.True(H(s, 67, 64) > 30);
        Assert.Equal(30, H(s, 61, 64), 4);
    }

    [Fact]
    public void TheHeightRangeIsJudgedAsTheStrokeStarted()
    {
        var s = Area();
        s.Mask.On = true;
        s.Mask.HeightMax = 31;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        for (int i = 0; i < 30; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        // It kept rising past 31 m: the ground was below it when the stroke started.
        Assert.True(H(s, 64, 64) > 35);
    }

    [Fact]
    public void AStrokeTheMaskStopsSaysSo()
    {
        var s = Area();
        s.Mask.On = true;
        s.Mask.HeightMin = 100;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        s.StrokeStep(64, 64, 0.1f);
        Assert.StartsWith("Nothing changed: the Mask", s.EndStroke().Shown);
    }

    [Fact]
    public void TheBorderSlopeIsMeasuredOverItsOwnStep()
    {
        // A 45° ramp west to east: the border points have one neighbour 1 m away, not 2 m.
        var g = new Ground(129, 129, 0, 0, 2);
        for (int p = 0; p < g.Base.Length; p++)
        {
            g.Base[p] = p % g.W;
        }
        var f = new Mask { On = true, SlopeMin = 40 }.For(g, _ => 0)!;
        Assert.Equal(1, f(64 * g.W));
        Assert.Equal(1, f(64 * g.W + 64));
        Assert.Equal(1, f(65 * g.W - 1));
    }

    // A one-shot edit is judged against the ground before it, not its own changes: flat ground under
    // "slope max 20°" all passes, so a raised block is whole (the points after the first ones saw the
    // raised neighbours as a steep slope and were left out, in scan order).
    [Fact]
    public void AShapeIsJudgedOnTheGroundBeforeIt()
    {
        var s = Area();
        s.Mask.On = true;
        s.Mask.SlopeMax = 20;
        s.Shape(40, 40, Formula.Compile("6", []), 6, 0, "x");
        for (int dx = -4; dx <= 4; dx++)
        {
            for (int dz = -4; dz <= 4; dz++)
            {
                Assert.Equal(36, H(s, 40 + dx, 40 + dz), 3);
            }
        }
    }
}
