using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Mask's rules one by one on a ground made up for them: layers (and points past the ground), the height and
/// slope ranges at both ends, every paint rule, a frozen mask, the warnings and Alt + Shift + click's height range.
/// </summary>
public class MaskBranchesTests
{
    /// <summary>The ground's width and height in grid points.</summary>
    private const int N = 65;

    /// <summary>A grid point's index.</summary>
    private static int P(int x, int z) => z * N + x;

    /// <summary>
    /// One zone at 30 m with a 10 m cliff up to 40 m from x = 40 east, and five painted test points on row 10: dirt,
    /// cultivated, paved, painted black, and never painted.
    /// </summary>
    private static Ground Ground()
    {
        var g = CliffGround.Make(N, 40);
        float[][] paints = [[1, 0, 0, 1], [0, 1, 0, 1], [0, 0, 1, 1], [0, 0, 0, 1]];
        for (int i = 0; i < paints.Length; i++)
        {
            g.PMod[P(i, 10)] = 1;
            Array.Copy(paints[i], 0, g.Paint, P(i, 10) * 4, 4);
        }
        return g;
    }

    /// <summary>Whether the five test points on row 10 pass under a paint rule.</summary>
    private static bool[] Pass(Mask.PaintRule rule)
    {
        Func<int, float> f = new Mask { On = true, Paint = rule }.For(Ground(), _ => 0)!;
        return [.. Enumerable.Range(0, 5).Select(x => f(P(x, 10)) > 0)];
    }

    [Fact]
    public void EachPaintRuleLetsItsGroundThrough()
    {
        PaintRuleChecks.AssertFivePointsPassEachRule(Pass);
    }

    [Fact]
    public void TheHeightRangeHasTwoEnds()
    {
        Ground g = Ground();
        Func<int, float> f = new Mask { On = true, HeightMin = 29, HeightMax = 35 }.For(g, _ => 0)!;
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(0, f(P(50, 30)));
        f = new Mask { On = true, HeightMin = 31 }.For(g, _ => 0)!;
        Assert.Equal(0, f(P(10, 30)));
        Assert.Equal(1, f(P(50, 30)));
        f = new Mask { On = true, HeightMax = 35 }.For(g, _ => 0)!;
        Assert.Equal(1, f(P(10, 30)));
    }

    [Fact]
    public void TheSlopeRangeHasTwoEnds()
    {
        Ground g = Ground();
        // At the cliff: atan(10 m over 2 m) is about 79°; elsewhere flat.
        Func<int, float> f = new Mask { On = true, SlopeMin = 10, SlopeMax = 80 }.For(g, _ => 0)!;
        Assert.Equal(1, f(P(39, 30)));
        Assert.Equal(0, f(P(10, 30)));
        f = new Mask { On = true, SlopeMax = 45 }.For(g, _ => 0)!;
        Assert.Equal(0, f(P(39, 30)));
        Assert.Equal(1, f(P(10, 30)));
        // The block's corners use the points there are.
        Assert.Equal(1, f(0));
        Assert.Equal(1, f(N * N - 1));
    }

    [Fact]
    public void LayersPassOnlyOnTheGround()
    {
        var m = new Mask { On = true };
        m.Layers.Add(2);
        // Points mostly of another layer, or past the ground, do not pass.
        Func<int, float> f = m.For(Ground(), p => p == 3 ? 0 : 2)!;
        Assert.Equal(1, f(5));
        Assert.Equal(0, f(3));
        Assert.Equal(0, f(N * N));
    }

    [Fact]
    public void AFrozenMaskRemembersItsAnswers()
    {
        Func<int, float> f = new Mask { On = true, HeightMax = 35 }.For(Ground(), _ => 0, frozen: true)!;
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(0, f(P(50, 30)));
        Assert.Equal(0, f(P(50, 30)));
    }

    [Fact]
    public void TheWarningsSayWhatLetsNothingThrough()
    {
        Assert.Equal("", new Mask().Warning());
        Assert.Equal("", new Mask { SlopeMin = 10, SlopeMax = 20, HeightMin = 10, HeightMax = 20 }.Warning());
        Assert.StartsWith("Slope max 0° only lets perfectly flat ground through", new Mask { SlopeMax = 0 }.Warning());
        Assert.Equal("Height min is above max: nothing matches.", new Mask { HeightMin = 50, HeightMax = 20 }.Warning());
        string both = new Mask { SlopeMin = 5, SlopeMax = -1, HeightMin = 9, HeightMax = 1 }.Warning();
        Assert.Contains("Slope min is above max", both);
        Assert.Contains("Height min is above max", both);
    }

    [Fact]
    public void PickingAHeightTurnsTheMaskOnAndTellsListeners()
    {
        var m = new Mask();
        // Nobody listening yet: nothing happens.
        m.Notify();
        int told = 0;
        m.Changed += () => told++;
        m.PickHeight(41.26f);
        Assert.True(m.On);
        Assert.Equal(39.3f, m.HeightMin!.Value, 3);
        Assert.Equal(43.3f, m.HeightMax!.Value, 3);
        Assert.Equal(1, told);
    }
}
