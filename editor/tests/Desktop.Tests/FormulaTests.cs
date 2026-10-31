using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Shape tool and its formula language (the web editor's editor/shapes.js).
/// </summary>
public class FormulaTests
{
    /// <summary>
    /// Compiles the formula and evaluates it at (x, z).
    /// </summary>
    private static double Eval(string src, double x = 0, double z = 0)
    {
        var f = Formula.Compile(src, ["x", "z"]);
        var env = new Formula.Env();
        env.Vars["x"] = x;
        env.Vars["z"] = z;
        return f(env);
    }

    [Fact]
    public void AFormulaRunAgainAndOnTwoThreadsGivesEachPointItsOwnNumbers()
    {
        // Functions inside functions of the same arity, run point after point with one compiled formula.
        var f = Formula.Compile("max(min(x, 2), min(3, z)) + pow(max(x, z), 2)", ["x", "z"]);
        double At(double x, double z)
        {
            var env = new Formula.Env();
            env.Vars["x"] = x;
            env.Vars["z"] = z;
            return f(env);
        }
        double Want(double x, double z) => Math.Max(Math.Min(x, 2), Math.Min(3, z)) + Math.Pow(Math.Max(x, z), 2);
        for (int k = 0; k < 20; k++)
        {
            Assert.Equal(Want(k * 0.3, 5 - k * 0.4), At(k * 0.3, 5 - k * 0.4));
        }
        Parallel.For(0, 2000, k => Assert.Equal(Want(k % 7, k % 5), At(k % 7, k % 5)));
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("-2 ^ 2", -4)]
    [InlineData("7 % 4", 3)]
    [InlineData("x > 1 ? 10 : 20", 10)]
    [InlineData("2 && 3", 3)]
    [InlineData("0 || 4", 4)]
    [InlineData("1 || 0 && 0", 1)]
    [InlineData("0 && 0 || 5", 5)]
    [InlineData("!0", 1)]
    [InlineData("smooth(0.5)", 0.5)]
    [InlineData("clamp(5, 0, 2)", 2)]
    [InlineData("max(1, x, 3)", 3)]
    [InlineData("round(2.5)", 3)]
    [InlineData("1e2 + .5", 100.5)]
    [InlineData("2E3 + .5e1", 2005)]
    [InlineData("sign(-3)", -1)]
    public void FormulasWorkOutLikeTheWebEditors(string src, double want)
    {
        Assert.Equal(want, Eval(src, x: 2), 9);
    }

    [Fact]
    public void TheSignOfNaNIsNaN() =>
        // Math.Sign threw on it, at every point, so every point counted as bad.
        Assert.True(double.IsNaN(Eval("sign(0 / 0)")));

    [Theory]
    [InlineData("", "the formula is empty")]
    [InlineData("1 +", "the formula ends too early")]
    [InlineData("(1 + 2", "“)” expected at the end")]
    [InlineData("y + 1", "there is no “y” (you can use x, z, pi)")]
    [InlineData("foo(1)", "there is no function “foo”")]
    [InlineData("1 2", "“2” is out of place")]
    [InlineData("1 # 2", "I do not understand “# 2”")]
    [InlineData("n(1)", "n(x, z) takes two numbers")]
    [InlineData("sin()", "sin(…) takes one number")]
    [InlineData("pow(x)", "pow(…) takes 2 numbers")]
    [InlineData("clamp(x, 0)", "clamp(…) takes 3 numbers")]
    public void MistakesAreExplained(string src, string message)
    {
        var ex = Assert.Throws<FormatException>(() => Formula.Compile(src, ["x", "z"]));
        Assert.Equal(message, ex.Message);
    }

    // A pasted formula nested thousands deep is refused (it ran out of stack: the app closed).
    [Fact]
    public void DeepOrHugeFormulasAreRefused()
    {
        string[] names = ["x", "z"];
        Assert.Equal("the formula is nested too deeply", Assert.Throws<FormatException>(() => Formula.Compile(new string('(', 1000) + "1" + new string(')', 1000), names)).Message);
        Assert.Equal("the formula is nested too deeply", Assert.Throws<FormatException>(() => Formula.Compile(new string('-', 3000) + "1", names)).Message);
        Assert.Equal("the formula is too long", Assert.Throws<FormatException>(() => Formula.Compile(string.Join("+", Enumerable.Repeat("1", 3000)), names)).Message);
        Assert.Equal(40, Eval(new string('(', 50) + "40" + new string(')', 50)), 9);
    }

    [Fact]
    public void ShapesGoAsHighAsTheyAsk()
    {
        var s = TestScenes.Flat(2);
        _ = s.Shape(64, 64, Formula.Compile("h", ["h"]), 5, 20, "Shape");
        Assert.Equal(50, s.Scene.Heights[64 * s.Scene.W + 64], 3);
    }

    [Fact]
    public void FormulasThatGiveNoNumberAreCounted()
    {
        var s = TestScenes.Flat(2);
        var (touched, bad) = s.Shape(64, 64, Formula.Compile("sqrt(x) / 0 - 1 / 0", ["x"]), 3, 1, "Shape");
        Assert.Equal(0, touched);
        Assert.True(bad > 0);
    }
}
