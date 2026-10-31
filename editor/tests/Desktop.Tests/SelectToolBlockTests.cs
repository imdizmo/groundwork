using Avalonia.Headless.XUnit;
using Xunit;
using static Groundwork.Desktop.Tests.SelectToolRun;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A move not put on the ground: the whole selection goes as one block that keeps its shape, up or down with the
/// ground under its middle.
/// </summary>
[Collection("World files")]
public class SelectToolBlockTests
{
    // How far the block follows the ground: as much as the ground under its middle goes up or down; nothing when the
    // middle is, or was, past the area's edge, where there is no ground.
    [AvaloniaFact]
    public void TheBlockFollowsNothingPastTheAreasEdge()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        var s = v.S.Scene;
        for (int i = 0; i < s.Heights.Length; i++)
        {
            s.Heights[i] = i % s.W - (s.W - 1) / 2f + s.Cx > 60.5f ? 34 : 30;
        }
        float Follow(float dx) =>
            (float)PrivateMethods.Call(typeof(SelectTool), "FollowGround", v.Tool, new SelectTool.Move { Items = [], Cx = 40, Cz = 40, Dx = dx })!;
        Assert.Equal(4, Follow(21), 2);
        Assert.Equal(0, Follow(10_000));
        Assert.Equal(0, (float)PrivateMethods.Call(typeof(SelectTool), "FollowGround", v.Tool, new SelectTool.Move { Items = [], Cx = 10_000, Cz = 40, Dx = -9_960 })!);
    }
}
