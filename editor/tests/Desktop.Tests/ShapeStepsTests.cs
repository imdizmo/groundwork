using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The generator's shaping steps: their order, switched off, run twice, and kept in map.json.</summary>
public class ShapeStepsTests
{
    private static readonly ZoneGenerator.Planting None = new([], [], [], []);
    private static readonly ZoneGenerator.Spec Worn = new(Low: 10, High: 160, RimHeight: 0, LakeHectares: [], Seed: 4, Valleys: 1, Strata: 6, Erosion: 1, Talus: 38);

    private static float[] Ground(ZoneGenerator.Spec spec) => ZoneGenerator.Generate(spec, 500, 500, 0, 0, None).Heights;

    private static ZoneGenerator.ShapeStep[] Order(params string[] kinds) => [.. kinds.Select(k => new ZoneGenerator.ShapeStep(k))];

    [Fact]
    public void TheUsualOrderIsTheSameAsNoSteps() =>
        Assert.Equal(Ground(Worn), Ground(Worn with { Steps = ZoneGenerator.DefaultSteps }));

    [Fact]
    public void TheOrderChangesTheGround() =>
        Assert.NotEqual(Ground(Worn with { Steps = Order("valleys", "strata", "erosion", "scree") }), Ground(Worn with { Steps = Order("scree", "erosion", "valleys", "strata") }));

    [Fact]
    public void AStepSwitchedOffIsAsItsSliderAtNothing()
    {
        var off = ZoneGenerator.DefaultSteps.Select(s => s.Kind == "erosion" ? s with { On = false } : s).ToArray();
        Assert.Equal(Ground(Worn with { Erosion = 0 }), Ground(Worn with { Steps = off }));
        var none = ZoneGenerator.DefaultSteps.Select(s => s.Kind == "erosion" ? s with { Times = 0 } : s).ToArray();
        Assert.Equal(Ground(Worn with { Erosion = 0 }), Ground(Worn with { Steps = none }));
    }

    [Fact]
    public void AStepRunTwiceWearsMore()
    {
        float[] once = Ground(Worn with { Steps = Order("erosion") }), twice = Ground(Worn with { Steps = Order("erosion", "erosion") });
        float[] bare = Ground(Worn with { Steps = Order() });
        float Moved(float[] g) => g.Zip(bare, (a, b) => MathF.Abs(a - b)).Sum();
        Assert.True(Moved(twice) > Moved(once) * 1.2f, $"{Moved(once)} once, {Moved(twice)} twice");
    }

    [Fact]
    public void TheStepsAreKeptInMapJson()
    {
        string dir = Directory.CreateTempSubdirectory("gw-steps-").FullName;
        try
        {
            var steps = new ZoneGenerator.ShapeStep[] { new("erosion", Times: 0.5f), new("valleys", On: false), new("erosion") };
            var m = MapProject.CreateZone("Steps", 2, Worn with { Steps = steps }, None, dir);
            var back = MapProject.Open(m.Folder).Generated!.Value.Spec.Steps!;
            Assert.Equal(steps, back);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
