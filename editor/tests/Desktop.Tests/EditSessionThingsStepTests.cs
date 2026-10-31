using System.Numerics;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The things parts of a step: the removals a step makes, the natural objects a mountain buries, and the things the
/// game changed taken into the scene.
/// </summary>
public class EditSessionThingsStepTests
{
    private static WorldScene.Thing Rock() => new(3, AssetIds.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);

    [Fact]
    public void ChangeThingsRemovesTheThingsGivenAndSkipsBadOnesAndGoneOnes()
    {
        var s = TestScenes.Flat(1, Rock());
        var (indices, changes, added) = s.ChangeThings([0, 0, 7], []);
        Assert.Empty(indices);
        Assert.Empty(added);
        Assert.Equal(new[] { (0, false, true) }, changes.ToArray());
    }

    [Fact]
    public void ChangeThingsSkipsAThingAlreadyGone()
    {
        var s = TestScenes.Flat(1, Rock() with { Gone = true });
        var (_, changes, _) = s.ChangeThings([0], []);
        Assert.Empty(changes);
    }
}
