using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The preview's cache of the ground after each step: what it gives is what making it afresh gives.</summary>
public class ShapeCacheTests
{
    private static readonly ZoneGenerator.Planting None = new([], [], [], []);
    private static readonly ZoneGenerator.Spec Start = new(Low: 10, High: 160, RimHeight: 80, LakeHectares: [1], Seed: 6, Valleys: 1, Strata: 6, Erosion: 0.5f, Talus: 38, Crags: 3, Woods: 0.3f);

    private static ZoneGenerator.Zone Make(ZoneGenerator.Spec spec, ShapeCache? cache) => ZoneGenerator.Generate(spec, 400, 400, -200, -200, None, null, cache);

    [Fact]
    public void EachChangeMadeFromTheCacheIsTheGroundMadeAfresh()
    {
        var cache = new ShapeCache();
        var changes = new[]
        {
            Start,
            Start with { Woods = 0.6f },   // laid on the ground: all of the shaping reused
            Start with { Woods = 0.6f, Crags = 6 },   // the last step's slider
            Start with { Woods = 0.6f, Crags = 6, Erosion = 1 },   // a middle step's
            Start with { Steps = [new("erosion"), new("valleys", Times: 0.5f), new("crags", On: false)] },
            Start with { Seed = 7 },   // everything again
        };
        foreach (var spec in changes)
        {
            var cached = Make(spec, cache);
            var fresh = Make(spec, null);
            Assert.Equal(fresh.Heights, cached.Heights);
            Assert.Equal(fresh.Mask, cached.Mask);
        }
        Assert.True(cache.Count > 5);
    }
}
