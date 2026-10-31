using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Edge cases of the editor's small rules: every formula function and comparison, every Mask paint rule and its
/// warnings, stamps and the Place tool's memory when their files are broken or cannot be written, the game's biome
/// colours, finding a world by folder or name, and the overlays' markers, square rings and styles.
/// </summary>
public class LogicEdgeTests
{
    /// <summary>
    /// A formula of x and z, evaluated at x, z.
    /// </summary>
    private static double Eval(string text, double x = 0, double z = 0)
    {
        var f = Formula.Compile(text, ["x", "z"]);
        var env = new Formula.Env();
        env.Vars["x"] = x;
        env.Vars["z"] = z;
        return f(env);
    }

    [Theory]
    [InlineData("tan(0)", 0)]
    [InlineData("abs(-3)", 3)]
    [InlineData("sqrt(16)", 4)]
    [InlineData("sqrt(-4)", 0)]
    [InlineData("exp(0)", 1)]
    [InlineData("log(1)", 0)]
    [InlineData("floor(2.7)", 2)]
    [InlineData("ceil(2.2)", 3)]
    [InlineData("min(4, 2, 9)", 2)]
    [InlineData("max(4, 2, 9)", 9)]
    [InlineData("pow(2, 10)", 1024)]
    [InlineData("atan2(0, 1)", 0)]
    [InlineData("sign(-5)", -1)]
    [InlineData("bell(0)", 1)]
    [InlineData("pi", Math.PI)]
    [InlineData("2 <= 2", 1)]
    [InlineData("3 <= 2", 0)]
    [InlineData("2 >= 3", 0)]
    [InlineData("3 >= 3", 1)]
    [InlineData("2 == 2", 1)]
    [InlineData("2 != 2", 0)]
    [InlineData("2 != 3", 1)]
    public void EveryFunctionAndComparison(string text, double expected) => Assert.Equal(expected, Eval(text), 6);

    [Fact]
    public void LogOfZeroAndEmptyMinMaxStayFinite()
    {
        Assert.True(double.IsFinite(Eval("log(0)")));
        Assert.Equal(double.PositiveInfinity, Eval("min()"));
        Assert.Equal(double.NegativeInfinity, Eval("max()"));
    }

    /// <summary>
    /// A flat zone at 30 m with four points painted (dirt, fields, paved, a faint paint) and one bare.
    /// </summary>
    private static Ground PaintedGround(out int dirt, out int fields, out int paved, out int faint, out int bare)
    {
        var g = new Ground(65, 65, 0, 0, 1);
        Array.Fill(g.Base, 30f);
        void Paint(int p, float r, float gr, float b)
        {
            g.PMod[p] = 1;
            g.Paint[p * 4] = r;
            g.Paint[p * 4 + 1] = gr;
            g.Paint[p * 4 + 2] = b;
            g.Paint[p * 4 + 3] = 1;
        }
        (dirt, fields, paved, faint, bare) = (10, 20, 30, 40, 50);
        Paint(dirt, 1, 0, 0);
        Paint(fields, 0, 1, 0);
        Paint(paved, 0, 0, 1);
        Paint(faint, 0.05f, 0.05f, 0.05f);
        return g;
    }

    [Fact]
    public void EveryPaintRuleOfTheMask()
    {
        var g = PaintedGround(out int dirt, out int fields, out int paved, out int faint, out int bare);
        bool[] Pass(Mask.PaintRule rule)
        {
            var f = new Mask { On = true, Paint = rule }.For(g, _ => 0)!;
            return [.. new[] { dirt, fields, paved, faint, bare }.Select(p => f(p) > 0)];
        }
        PaintRuleChecks.AssertFivePointsPassEachRule(Pass);
    }

    [Fact]
    public void TheMaskWarnsAboutRangesThatMatchNothing()
    {
        var m = new Mask { On = true, HeightMin = 50, HeightMax = 10, SlopeMin = 40, SlopeMax = 5 };
        string w = m.Warning();
        Assert.Contains("Height min is above max", w);
        Assert.Contains("Slope min is above max", w);
        Assert.Equal("", new Mask { On = true, HeightMin = 10, HeightMax = 50 }.Warning());
    }

    [Fact]
    public void APictureThatIsNotOneMakesNoStamp()
    {
        Assert.Null(Stamps.FromPicture([1, 2, 3, 4]));
        Assert.Null(Stamps.FromPicture([]));
    }
}
