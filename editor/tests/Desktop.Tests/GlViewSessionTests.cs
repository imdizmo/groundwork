using Avalonia.Headless.XUnit;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The 3D view following its area's session: an area read again from the game (after zone resets were applied) drops
/// the selection at once, as its objects have new indices; another area shown, the last one's session is not followed.
/// </summary>
[Collection("World files")]
public class GlViewSessionTests
{

    // Another area shown: the last one's session is not followed any more. Its change to a thing the new scene does not
    // have (index 2 of 3) threw from the old session's step, and it touched the new scene's selection.
    [AvaloniaFact]
    public void TheLastAreasSessionIsNotFollowedAnyMore()
    {
        static WorldScene.Thing Rock(int id) => new(id, AssetIds.Of("Rock_4"), new System.Numerics.Vector3(id, 30, 0), System.Numerics.Vector3.Zero, 1, false);
        var before = TestScenes.Flat(1, Rock(1), Rock(2), Rock(3));
        var now = TestScenes.Flat(1, Rock(4));
        var w = new MainWindow(load: false);
        w.Show();
        w.View.Show(before.Scene, null);
        w.View.Show(now.Scene, null);
        w.View.Select([0]);
        before.Delete([2]);
        Assert.Equal([0], w.View.Selected);
    }
}
