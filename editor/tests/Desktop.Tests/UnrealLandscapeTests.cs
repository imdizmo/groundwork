using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The map's heights as an Unreal landscape's 16-bit values, at a height scale that holds them.</summary>
public class UnrealLandscapeTests
{
    private static float Back(byte[] d, int i, float z) => (BitConverter.ToUInt16(d, i * 2) - 32768) / 128f * z / 100;

    [Fact]
    public void LowMapsKeepUnrealsUsualScale()
    {
        var (z, d) = UnrealLive.LandscapeHeights([0, 10.5f, -3, 200]);
        Assert.Equal(100, z);
        Assert.Equal(32768, BitConverter.ToUInt16(d, 0));
        Assert.Equal(10.5f, Back(d, 1, z), 0.01f);
        Assert.Equal(-3, Back(d, 2, z), 0.01f);
    }

    [Fact]
    public void HighMapsGetAScaleThatHoldsThem()
    {
        var (z, d) = UnrealLive.LandscapeHeights([0, 402, -40]);
        Assert.True(z > 157, $"{z}");
        Assert.Equal(402, Back(d, 1, z), 0.05f);
        Assert.Equal(-40, Back(d, 2, z), 0.05f);
    }
}
