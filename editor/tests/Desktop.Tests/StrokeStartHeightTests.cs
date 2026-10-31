using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Stroke.StartHeight: a point's height as the stroke found it: its base, plus its edit's level and smoothing when the
/// point was edited (no limit).
/// </summary>
public class StrokeStartHeightTests
{
    /// <summary>A 2 × 2 ground whose first point is at 5 m, and a stroke whose start has point 0 with the given edit.</summary>
    private static (Ground Ground, Stroke Stroke) Start(float level, float smooth, byte mod)
    {
        var ground = new Ground(2, 2, 0, 0, 1);
        ground.Base[0] = 5f;
        var start = ground.Snapshot();
        (start.Level[0], start.Smooth[0], start.Mod[0]) = (level, smooth, mod);
        return (ground, new Stroke { Tool = BrushTool.Raise, Start = start });
    }

    [Fact]
    public void AnUneditedPointStartsAtItsBase()
    {
        var (ground, stroke) = Start(level: 9f, smooth: 9f, mod: 0);

        Assert.Equal(5f, stroke.StartHeight(ground, 0));
    }

    [Fact]
    public void AnEditedPointAddsItsLevelAndSmooth()
    {
        var (ground, stroke) = Start(level: 1f, smooth: 2f, mod: 1);

        Assert.Equal(8f, stroke.StartHeight(ground, 0));
    }

    [Fact]
    public void AnEditGoesAsFarAsItGoes()
    {
        var (ground, stroke) = Start(level: 120f, smooth: 0f, mod: 1);

        Assert.Equal(125f, stroke.StartHeight(ground, 0));
    }
}
