using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The editor's camera (looking at a point from a compass yaw, a pitch down and a distance) as Unreal's.</summary>
public class UnrealCameraTests
{
    [Fact]
    public void LookingNorthFromTheSouth()
    {
        var (at, yaw, pitch) = UnrealLive.CameraFor(new Vector3(0, 5, 0), 0, 0, 10);
        Assert.Equal(new Vector3(0, 1000, 500), at);   // 10 m south (editor z -10 = Unreal Y +1000), 5 m up
        Assert.Equal(-90, yaw, 0.01f);                  // north is Unreal's -Y
        Assert.Equal(0, pitch, 0.01f);
    }

    [Fact]
    public void LookingEastAndDown()
    {
        var (at, yaw, pitch) = UnrealLive.CameraFor(new Vector3(100, 0, 0), 90, 30, 20);
        Assert.Equal(0, yaw, 0.01f);      // east is Unreal's +X
        Assert.Equal(-30, pitch, 0.01f);  // looking down
        Assert.Equal(100 * 100 - (20 * MathF.Cos(MathF.PI / 6) * 100), at.X, 0.5f);
        Assert.Equal(20 * 0.5f * 100, at.Z, 0.5f);
    }
}
