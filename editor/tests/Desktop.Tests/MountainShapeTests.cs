using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Mountain tool's shapes and the Shape tool's presets.</summary>
public class MountainShapeTests
{
    private static MountainSpec Of(MountainKind kind) => new(kind, 80, 150, 0.5f, 30, 7);

    [Fact]
    public void APeakRisesInTheMiddleAndIsNothingPastItsReach()
    {
        var f = Mountain.Shape(Of(MountainKind.Peak));
        Assert.InRange(f(0, 0), 50, 110);
        Assert.True(f(0, 0) > f(60, 0) && f(60, 0) > f(120, 0));
        Assert.Equal(0, f(200, 0));
        // the same numbers make the same mountain
        Assert.Equal(f(33, -41), Mountain.Shape(Of(MountainKind.Peak))(33, -41));
    }

    [Fact]
    public void AVolcanoHasACraterAndAMesaAFlatTop()
    {
        var volcano = Mountain.Shape(Of(MountainKind.Volcano));
        Assert.True(volcano(0, 0) < volcano(40, 0) - 5, $"crater {volcano(0, 0)} m, rim {volcano(40, 0)} m");
        var mesa = Mountain.Shape(Of(MountainKind.Mesa));
        Assert.InRange(mesa(10, 0) - mesa(-10, 5), -4, 4);
        Assert.True(mesa(0, 0) > 60);
    }

    [Fact]
    public void EveryKindRollsWithinItsPresetAndRaisesGround()
    {
        var rng = new Random(3);
        foreach (var p in Mountain.Presets)
        {
            var m = Mountain.Roll(p, rng);
            Assert.InRange(m.Height, p.Height.Min, p.Height.Max);
            Assert.InRange(m.Radius, p.Radius.Min, p.Radius.Max);
            var f = Mountain.Shape(m);
            float most = 0;
            for (float x = -m.Radius; x <= m.Radius; x += m.Radius / 20)
            {
                most = MathF.Max(most, f(x, 0));
            }
            Assert.True(most > m.Height * 0.3f, $"{p.Name}: at most {most} m of {m.Height}");
        }
    }

    [Fact]
    public void AMountainAtTheMapsEdgeIsCutOffThere()
    {
        var s = TestScenes.Flat(1);
        var (touched, _) = EditSession.RaiseGround(s.Ground, 2, 2, 40, (_, _) => 1f, null);
        Assert.NotEmpty(touched);
        Assert.All(touched, p => Assert.False(s.Ground.Locked(p % s.Ground.W, p / s.Ground.W)));
    }

    [Fact]
    public void EveryShapePresetCompilesAndShapesTheGround()
    {
        foreach (var (name, src) in MapTools.ShapeTool.Presets)
        {
            var s = TestScenes.Flat(1);
            var (touched, bad) = s.Shape(32, 32, Formula.Compile(src, ["x", "z", "d", "r", "h"]), 12, 5, name);
            Assert.True(touched > 50, $"{name}: {touched} points");
            Assert.Equal(0, bad);
        }
    }
}
