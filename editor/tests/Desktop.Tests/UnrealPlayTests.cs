using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Play here: where the game starts in Unreal, from the editor's ground point and camera turn.</summary>
public class UnrealPlayTests
{
    [Fact]
    public void TheStartIsInUnrealCentimetresAboveTheGround()
    {
        var (x, y, z, _) = UnrealLive.PlayStart(new Vector3(10, 5, 20), 0);
        Assert.Equal(1000, x, 0.01f);
        Assert.Equal(-2000, y, 0.01f);
        Assert.Equal(620, z, 0.01f);   // 1.2 m up: the player's capsule stands on the ground
    }

    [Theory]
    [InlineData(0, -90)]                  // the camera looks north (+z): Unreal's -Y
    [InlineData(MathF.PI / 2, 180)]       // west (-x)
    [InlineData(MathF.PI, 90)]            // south
    [InlineData(-MathF.PI / 2, 0)]        // east (+x): Unreal's +X
    public void ItFacesTheWayTheCameraLooks(float cameraYaw, float unrealYaw)
    {
        var (_, _, _, yaw) = UnrealLive.PlayStart(Vector3.Zero, cameraYaw);
        Assert.Equal(0, Math.Abs(Math.IEEERemainder(yaw - unrealYaw, 360)), 0.01);
    }
}
