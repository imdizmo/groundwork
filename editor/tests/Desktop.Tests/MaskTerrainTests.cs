using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Mask limited by a terrain map: hollows by convexity, and which way the ground faces, north up the grid.</summary>
public class MaskTerrainTests
{
    [Fact]
    public void AConvexityLimitPassesTheHollowAndNotTheFlat()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        _ = s.Shape(32, 32, Formula.Compile("-h * smooth(1 - d / r)", ["x", "z", "d", "r", "h"]), 10, 6, "pit");
        var test = new Mask { On = true, Terrain = "convexity", TerrainMax = 0.4f }.For(g, _ => 0)!;
        Assert.Equal(1, test((32 * g.W) + 32));
        Assert.Equal(0, test((32 * g.W) + 55));
    }

    [Fact]
    public void GroundRisingNorthFacesSouth()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        // rising 0.5 m a metre north (up the grid): its slope runs down to the south
        for (int gz = 0; gz < g.H; gz++)
        {
            for (int gx = 0; gx < g.W; gx++)
            {
                g.SetHeight((gz * g.W) + gx, gz * 0.5f);
            }
        }
        int mid = (g.H / 2 * g.W) + (g.W / 2);
        Assert.Equal(1, new Mask { On = true, Terrain = "facing_north", TerrainMax = 0.2f }.For(g, _ => 0)!(mid));
        Assert.Equal(0, new Mask { On = true, Terrain = "facing_north", TerrainMin = 0.8f }.For(g, _ => 0)!(mid));
        Assert.Equal(1, new Mask { On = true, Terrain = "facing_east", TerrainMin = 0.4f, TerrainMax = 0.6f }.For(g, _ => 0)!(mid));
    }

    [Fact]
    public void AnUpsideDownRangeIsWarnedAbout()
    {
        var m = new Mask { On = true, Terrain = "wetness", TerrainMin = 0.8f, TerrainMax = 0.2f };
        Assert.NotEqual("", m.Warning(english: true));
    }
}
