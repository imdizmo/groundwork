using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The zone generator: hills between their heights, a rim, lakes that hold water, woods of the share asked, layers by rule.</summary>
public class ZoneGeneratorTests
{
    private static readonly MapTools.Kind Tree = new("Tree", "Tree", "Trees", 1, 1, null), Rock = new("Rock", "Rock", "Rocks", 1, 1, null);
    private static readonly ZoneGenerator.Planting Kinds = new([Tree], [], [Rock], []);

    private static ZoneGenerator.Zone Make(ZoneGenerator.Spec spec, int n = 400) => ZoneGenerator.Generate(spec, n, n, -200, -200, Kinds);

    [Fact]
    public void TheRollingGroundStaysBetweenItsHeights()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 60, RimHeight: 0, LakeHectares: []));
        Assert.InRange(z.Heights.Min(), 19.9f, 30);
        Assert.InRange(z.Heights.Max(), 40, 60.1f);
    }

    [Fact]
    public void TheRimRisesAtTheEdgeAndIsSteep()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 60, RimHeight: 200, RimWidth: 80, LakeHectares: []));
        // the rim changes along the edge (high in places, low in others): its highest ground near the edge is far up
        float edge = Enumerable.Range(0, 400).Max(c => MathF.Max(z.Heights[(5 * 400) + c], z.Heights[(394 * 400) + c]));
        Assert.True(edge > 150, $"{edge}");
        Assert.True(z.Heights[(200 * 400) + 200] < 61);   // the middle
        int steep = Enumerable.Range(0, 400 * 400).Count(p => ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) > 30);
        Assert.True(steep > 400 * 400 / 20, $"{steep}");
    }

    [Fact]
    public void ALakeHoldsItsWaterAndNothingStandsInIt()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [3]));
        var l = Assert.Single(z.Lakes);
        // the shore a few metres out all round is above the surface, the middle below it
        Assert.All(l.Outline, p => Assert.True(Height(z, p + (Vector2.Normalize(p - l.Middle) * 3)) > l.Level, $"{Height(z, p)} vs {l.Level}"));
        Assert.True(Height(z, l.Middle) < l.Level - 1);
        Assert.DoesNotContain(z.Objects, o => MapWater.InsideBy(l.Outline, new Vector2(o.At.X, -o.At.Z)) > 0);
    }

    [Fact]
    public void TheWoodsCoverTheShareAsked()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [], Woods: 0.4f));
        float share = z.Wooded.Count(w => w > 0.5f) / (float)z.Wooded.Length;
        Assert.InRange(share, 0.32f, 0.48f);
        Assert.Contains(z.Objects, o => o.Model == "Tree");
    }

    [Fact]
    public void SteepGroundIsRock()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 200, RimWidth: 80, LakeHectares: []));
        int p = Enumerable.Range(0, 400 * 400).First(i => ZoneGenerator.Slope(z.Heights, 400, 400, i % 400, i / 400) > 46);
        Assert.True(z.Mask[(p * 4) + 2] > 200);
    }

    [Fact]
    public void ASeedMakesTheSameZoneAgain()
    {
        var spec = new ZoneGenerator.Spec(LakeHectares: [2], Seed: 5);
        Assert.Equal(Make(spec).Heights, Make(spec).Heights);
        Assert.NotEqual(Make(spec).Heights, Make(spec with { Seed = 6 }).Heights);
    }

    private static float Height(ZoneGenerator.Zone z, Vector2 unreal)
    {
        int c = Math.Clamp((int)MathF.Round(unreal.X + 200), 0, 399), r = Math.Clamp((int)MathF.Round(unreal.Y + 200), 0, 399);
        return z.Heights[(r * 400) + c];
    }
}
