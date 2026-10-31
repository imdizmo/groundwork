using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The ground parts of a step: a ground change's points and their values, and a mountain's raise.
/// </summary>
public class EditSessionGroundStepTests
{
    [Fact]
    public void NoGroundChangeMakesAnEmptyGroundPart()
    {
        var s = TestScenes.Flat(1);
        var (points, _, _) = s.ChangeGround(null);
        Assert.Empty(points);
    }

    [Fact]
    public void AGroundChangeThatTouchesNothingMakesAnEmptyGroundPart()
    {
        var s = TestScenes.Flat(1);
        var (points, _, _) = s.ChangeGround(_ => (new List<int>(), (0, 0, 0, 0)));
        Assert.Empty(points);
    }

    [Fact]
    public void AGroundChangeReportsItsPointsAndTheirValues()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        var (points, before, after) = s.ChangeGround(g =>
        {
            g.SetHeight(p, g.HeightOf(p) + 2);
            return (new List<int> { p }, (31, 31, 33, 33));
        });
        Assert.Equal(new[] { p }, points);
        Assert.True(before.Level[0] != after.Level[0] || before.Smooth[0] != after.Smooth[0]);
    }

    [Fact]
    public void RaiseGroundRaisesTheSquareInReach()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        var (touched, rect) = EditSession.RaiseGround(g, 32, 32, 2, (_, _) => 1f, null);
        Assert.Equal(25, touched.Count);
        Assert.Equal((29, 29, 35, 35), rect);
        Assert.Equal(31, g.HeightOf(32 * g.W + 32), 0.001f);
    }

    [Fact]
    public void RaiseGroundLeavesPointsTheMaskCloses()
    {
        var s = TestScenes.Flat(1);
        var (touched, _) = EditSession.RaiseGround(s.Ground, 32, 32, 2, (_, _) => 1f, _ => 0f);
        Assert.Empty(touched);
    }

    [Fact]
    public void RaiseGroundSkipsAZeroHeight()
    {
        var s = TestScenes.Flat(1);
        var (touched, _) = EditSession.RaiseGround(s.Ground, 32, 32, 2, (_, _) => 0f, null);
        Assert.Empty(touched);
    }
}
