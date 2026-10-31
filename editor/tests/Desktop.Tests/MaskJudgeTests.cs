using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The mask's judgement of one ground point: the biome, height, slope and paint rules, and that the mask's settings and
/// the ground are copied when the judge is made.
/// </summary>
public class MaskJudgeTests
{
    /// <summary>The ground's width and height in grid points.</summary>
    private const int N = 5;

    /// <summary>A flat ground of 5 by 5 points at 30 m, with no paint.</summary>
    private static Ground Flat()
    {
        var g = new Ground(N, N, 0, 0, 1);
        for (int p = 0; p < N * N; p++)
        {
            g.Base[p] = 30;
        }
        return g;
    }

    [Fact]
    public void AMaskWithNoLimitsPassesEveryPoint()
    {
        var judge = new MaskJudge(new Mask { On = true }, Flat(), _ => 0);
        Assert.Equal(1, judge.Test(0));
        Assert.Equal(1, judge.Test(N * N - 1));
    }

    [Fact]
    public void ALayerNotChosenFails()
    {
        var mask = new Mask { On = true };
        mask.Layers.Add(2);
        int[] layers = new int[N * N];
        Array.Fill(layers, 2);
        layers[3] = 0;
        var judge = new MaskJudge(mask, Flat(), p => layers[p]);
        Assert.Equal(1, judge.Test(4));
        Assert.Equal(0, judge.Test(3));
    }

    [Fact]
    public void APointPastTheGroundFails()
    {
        var mask = new Mask { On = true };
        mask.Layers.Add(2);
        var judge = new MaskJudge(mask, Flat(), _ => 2);
        Assert.Equal(1, judge.Test(N * N - 1));
        Assert.Equal(0, judge.Test(N * N));
    }

    [Fact]
    public void AHeightOutsideTheRangeFails()
    {
        var low = new MaskJudge(new Mask { On = true, HeightMin = 31 }, Flat(), _ => 0);
        Assert.Equal(0, low.Test(0));
        var high = new MaskJudge(new Mask { On = true, HeightMax = 30 }, Flat(), _ => 0);
        Assert.Equal(1, high.Test(0));
    }

    [Fact]
    public void ASlopeOnFlatGroundIsNoSlope()
    {
        Assert.Equal(0, new MaskJudge(new Mask { On = true, SlopeMin = 1 }, Flat(), _ => 0).Test(12));
        Assert.Equal(1, new MaskJudge(new Mask { On = true, SlopeMax = 45 }, Flat(), _ => 0).Test(12));
    }

    [Fact]
    public void PaintFollowsTheRuleAndTheGroundsOwnPaint()
    {
        var g = Flat();
        g.PMod[1] = 1;
        g.Paint[1 * 4] = 1;
        var dirt = new MaskJudge(new Mask { On = true, Paint = Mask.PaintRule.Layer2 }, g, _ => 0);
        Assert.Equal(1, dirt.Test(1));
        Assert.Equal(0, dirt.Test(2));
    }

    [Fact]
    public void LaterChangesToTheMaskDoNotChangeTheJudgement()
    {
        var mask = new Mask { On = true, HeightMin = 31 };
        var judge = new MaskJudge(mask, Flat(), _ => 0);
        mask.HeightMin = null;
        Assert.Equal(0, judge.Test(0));
    }

    [Fact]
    public void TheSlopeIsTheSteepnessOfTheNeighbours()
    {
        var g = CliffGround.Make(N, 3);
        var judge = new MaskJudge(new Mask { On = true, SlopeMax = 90 }, g, _ => 0);
        // Column 2: the neighbours are 30 m (x 1) and 40 m (x 3), 10 m over 2 m, so 5 m a metre.
        Assert.Equal(MathF.Atan(5) * 180 / MathF.PI, judge.Slope(2 * N + 2), 3);
        // Flat away from the step: no slope.
        Assert.Equal(0, judge.Slope(2 * N + 0), 3);
    }

    [Fact]
    public void EachPaintRuleJudgesThePaintOfItsPoint()
    {
        var g = Flat();
        float[][] paints = [[1, 0, 0, 1], [0, 1, 0, 1], [0, 0, 1, 1], [0, 0, 0, 1]];
        for (int i = 0; i < paints.Length; i++)
        {
            g.PMod[i] = 1;
            Array.Copy(paints[i], 0, g.Paint, i * 4, 4);
        }
        // Point 4 is never painted.
        bool[] Pass(Mask.PaintRule rule)
        {
            var judge = new MaskJudge(new Mask { On = true, Paint = rule }, g, _ => 0);
            return [.. Enumerable.Range(0, 5).Select(judge.PaintOk)];
        }
        PaintRuleChecks.AssertFivePointsPassEachRule(Pass);
    }
}
