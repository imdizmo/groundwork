using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The 3D editor's keys in the window: V and L for the right-hand panels, the Paste tool's F, Esc and Ctrl + wheel, the
/// Area tool's Enter and Backspace, the Dungeon tool's R and Delete, Esc for the Measure tool's tape, I and Esc for the
/// inspector; the walking and flying eyes; Inspect on a runestone or on more than one object; and the key table's
/// order and which keys it marks handled.
/// </summary>
[Collection("World files")]
public class MainWindowKeysTests
{
    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area (ground at 30 m) with these objects.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
        => TestWindows.EditingFlat(things);

    /// <summary>
    /// Presses a key (with these modifiers) on the window.
    /// </summary>
    private static void Press(MainWindow w, Key key, PhysicalKey physical, string? text, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPress(key, mods, physical, text);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void VAndLOpenAndCloseTheRightHandPanels()
    {
        var (w, _) = Open();
        Assert.True(w.ViewPanelCard.IsVisible);
        Press(w, Key.V, PhysicalKey.V, "v");
        Assert.False(w.ViewPanelCard.IsVisible);
        Press(w, Key.V, PhysicalKey.V, "v");
        Assert.True(w.ViewPanelCard.IsVisible);
        Press(w, Key.L, PhysicalKey.L, "l");
        Assert.True(w.History.Card.IsVisible);
        Assert.False(w.ViewPanelCard.IsVisible);
        Press(w, Key.L, PhysicalKey.L, "l");
        Assert.False(w.History.Card.IsVisible);
    }

    [AvaloniaFact]
    public void CtrlXCutsTheSelectionToTheClipboardAndCtrlZBringsItBack()
    {
        var stone = new WorldScene.Thing(4, AssetIds.Of("Runestone_Meadows"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        var beech = new WorldScene.Thing(5, AssetIds.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 1, false);
        var (w, s) = Open(stone, beech);
        w.Tools.ChooseSelect();
        w.View.Select([1]);
        Press(w, Key.X, PhysicalKey.X, "x", RawInputModifiers.Control);
        Assert.True(s.Scene.Things[1].Gone);
        Assert.False(s.Scene.Things[0].Gone);
        Assert.Empty(w.View.Selected);
        Assert.Equal([1], w.View.Paste.Clip!.Taken);
        Assert.Equal(AssetIds.Of("Beech1"), w.View.Paste.Clip.Objects.Single().Prefab);
        Assert.StartsWith("Cut 1 object(s): gone from here", w.MessageText.Text);
        Assert.True(s.CanUndo);
        Press(w, Key.Z, PhysicalKey.Z, "z", RawInputModifiers.Control);
        Assert.False(s.Scene.Things[1].Gone);
        // The copy stays on the clipboard, to be pasted after the undo too.
        Assert.Single(w.View.Paste.Clip.Objects);
    }
}
