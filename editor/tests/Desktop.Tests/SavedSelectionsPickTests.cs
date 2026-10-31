using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

using static Groundwork.Desktop.Tests.SavedSelectionsData;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Picking a kind for the Replace lists (Area and Select) with the eyedropper: a click in the 3D view gives the kind
/// clicked, and Esc or a click on nothing cancels.
/// </summary>
[Collection("Saved selections")]
public sealed class SavedSelectionsPickTests : SavedSelectionsFolder
{

    /// <summary>
    /// Where a point in view space is on screen.
    /// </summary>
    private static Avalonia.Point ScreenOf(MainWindow w, Vector3 viewPos)
    {
        var q = Vector4.Transform(new Vector4(viewPos, 1), w.View.ViewProj);
        var size = w.View.Bounds.Size;
        return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }

    /// <summary>
    /// A click in the window, once its layout is done (a tool chosen changes it).
    /// </summary>
    private static void Press(MainWindow w, Avalonia.Point at, MouseButton button)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        w.MouseMove(at);
        w.MouseDown(at, button);
        w.MouseUp(at, button);
    }

    /// <summary>
    /// A shown main window with one half wall (given its box, as there are no models here) and the view looking at it:
    /// with a screen point on the wall and one on empty ground.
    /// </summary>
    private static (MainWindow W, EditSession S, Avalonia.Point Wall, Avalonia.Point Empty) PickWindow()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), Vector3.Zero, 0, true));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
        // The wall's box, as drawing would know it (no models here): 2 m wide, 2 m high.
        w.View.SetBoxes([(0, c + new Vector3(-1, 0, -0.2f), c + new Vector3(1, 2, 0.2f))]);
        w.View.SetCamera(c + new Vector3(0, 6, 8), c, 1);
        // Laid out and drawn once: hit tests use the last frame (with one app for the whole run, the
        // window is not always drawn yet here).
        w.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (w, s, ScreenOf(w, c + new Vector3(0, 1, 0.2f)), ScreenOf(w, c + new Vector3(0, 0, 3)));
    }

    [AvaloniaFact]
    public void TheEyedropperGivesTheClickedKindAndSwallowsTheClick()
    {
        var (w, _, wall, _) = PickWindow();
        w.Tools.ChooseSelect();
        int? picked = null;
        w.PickKind("Replace", p => picked = p);
        Assert.Equal("Click an object to pick its kind for Replace. Esc cancels.", w.MessageText.Text);
        Press(w, wall, MouseButton.Left);
        Assert.Equal(Wall, picked);
        // The click was the eyedropper's: the Select tool did not select the wall.
        Assert.Empty(w.View.Selected);
        Assert.Null(w.View.PickObjectOnce);
        // The next click is the tool's again.
        Press(w, wall, MouseButton.Left);
        Assert.Equal([0], w.View.Selected);
    }
}
