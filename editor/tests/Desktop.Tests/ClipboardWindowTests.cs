using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static Groundwork.Desktop.Tests.ClipboardSetup;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Copy and paste in the window: the keys, the Select tool's copy, clearing a site and a paste past the open area.
/// </summary>
public class ClipboardWindowTests
{

    [AvaloniaFact]
    public void TheSelectToolCopiesObjectsThatFollowTheGround()
    {
        var s = Area();
        var w = TestWindows.Editing(s);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        var o = Assert.Single(w.View.Paste.Clip!.Objects);
        Assert.True(o.Follow);
        // On a flat place it lands at the ground plus what it stood above the mound.
        w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        w.PasteAt(new Vector2(100, 100));
        Assert.Equal(30 + (33 - H(s, 40, 40)), s.Scene.Things[^1].Position.Y, 2);
        // Objects only: the ground is not touched.
        Assert.Equal(30, H(s, 100, 100), 3);
    }
}
