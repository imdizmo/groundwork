using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;
using static Groundwork.Desktop.Tests.KeysAndMouseSetup;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The web editor's keys and mouse in the native app, the rest: the paste and the Place preview as , and . do (zooming when there is
/// nothing to turn), the help listing them, and Naturalize's New pattern.
/// </summary>
[Collection("World files")]
public class KeysAndMouseListingTests
{
    [AvaloniaFact]
    public void SpaceStillTypesInTextBoxes()
    {
        var w = new Window { Width = 400, Height = 200 };
        var box = new TextBox();
        w.Content = box;
        w.Show();
        new GlView().Attach(new Border(), w);
        box.Focus();
        // The box gets its Space (the view only takes it from other controls).
        int spaces = 0;
        box.KeyDown += (_, e) => spaces += e.Key == Key.Space ? 1 : 0;
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.Equal(1, spaces);
    }

    // ---- Alt + wheel.

    [AvaloniaFact]
    public void AltWheelTurnsTheSelectionLikeTheKeys()
    {
        var (a, sa, screenA) = Open(Wall());
        a.Tools.ChooseSelect();
        a.View.Select([0]);
        float distance = a.View.Camera.Distance;
        // Towards you (down) as ".", 1°.
        a.MouseWheel(screenA(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt);
        a.View.SelectTool.Commit();
        Assert.Equal(distance, a.View.Camera.Distance);
        var (b, sb, _) = Open(Wall());
        b.Tools.ChooseSelect();
        b.View.Select([0]);
        b.KeyPress(Key.OemPeriod, RawInputModifiers.None, PhysicalKey.Period, ".");
        b.View.SelectTool.Commit();
        Assert.Equal(sb.Scene.Things[^1].Rotation, sa.Scene.Things[^1].Rotation);
        Assert.NotEqual(Vector3.Zero, sa.Scene.Things[^1].Rotation);
    }

    [AvaloniaFact]
    public void AltWheelAwayAndWithShiftTurnTheOtherWayAndFurther()
    {
        var (a, sa, screen) = Open(Wall());
        a.Tools.ChooseSelect();
        a.View.Select([0]);
        a.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt | RawInputModifiers.Shift);
        a.View.SelectTool.Commit();
        var (b, sb, _) = Open(Wall());
        b.Tools.ChooseSelect();
        b.View.Select([0]);
        b.KeyPress(Key.OemComma, RawInputModifiers.Shift, PhysicalKey.Comma, ",");
        b.View.SelectTool.Commit();
        Assert.Equal(sb.Scene.Things[^1].Rotation, sa.Scene.Things[^1].Rotation);
    }

    [AvaloniaFact]
    public void AltWheelWithNothingToTurnZooms()
    {
        var (w, _, screen) = Open(Wall());
        w.Tools.ChooseSelect();
        float d = w.View.Camera.Distance;
        w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1), RawInputModifiers.Alt);
        Assert.True(w.View.Camera.Distance < d);
        // In the View tool too, and the plain wheel always zooms.
        w.Tools.Choose(null);
        d = w.View.Camera.Distance;
        w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, -1), RawInputModifiers.Alt);
        Assert.True(w.View.Camera.Distance > d);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        d = w.View.Camera.Distance;
        w.MouseWheel(screen(64, 64), new Avalonia.Vector(0, 1));
        Assert.True(w.View.Camera.Distance < d);
        Assert.True(w.AltWheel(Key.OemComma, false));
        w.Tools.Choose(BrushTool.Raise);
        Assert.False(w.AltWheel(Key.OemComma, false));
    }

    // ---- Help and Naturalize.

    [AvaloniaFact]
    public void TheHelpListsEveryKey()
    {
        var w = new MainWindow(load: false);
        var texts = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w.HelpCard).OfType<TextBlock>().Select(t => t.Text).ToList();
        foreach (string k in new[] { "[  ]", "Alt + wheel", "E / M / H", "Middle drag", "Place: R", "PgUp / PgDn / End", "Ctrl+S", "V / L / ?", "Alt + click", "I", "Del" })
        {
            Assert.Contains(k, texts);
        }
        Assert.Contains(texts, t => t?.Contains("Space + left drag, Shift + right drag") == true);
    }

    [AvaloniaFact]
    public void NewPatternGivesNaturalizeAnotherNoise()
    {
        var t = new ToolPanel();
        string? said = null;
        t.Message += m => said = m.Shown;
        t.NewPattern(1234);
        Assert.Equal(1234, t.Brush.Noise.Seed);
        Assert.Equal("New natural pattern. The next Naturalize stroke or natural path uses it.", said);
        float a = t.Brush.Noise.Fbm(3.3f, 7.1f);
        t.NewPattern(99);
        Assert.NotEqual(a, t.Brush.Noise.Fbm(3.3f, 7.1f));
        // The same seed, the same pattern.
        t.NewPattern(1234);
        Assert.Equal(a, t.Brush.Noise.Fbm(3.3f, 7.1f));
        // The button: a new random one, shown with the Naturalize tool.
        int before = t.Brush.Noise.Seed;
        t.NewPatternButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotEqual(before, t.Brush.Noise.Seed);
        t.Choose(BrushTool.Natural);
        Assert.True(((Control)t.NewPatternButton.Parent!).IsVisible);
        t.Choose(BrushTool.Raise);
        Assert.False(((Control)t.NewPatternButton.Parent!).IsVisible);
    }

    [AvaloniaFact]
    public void TheEditorsNaturalizeUsesTheNewPattern()
    {
        var (w, s, _) = Open();
        w.Tools.NewPattern(77);
        Assert.Equal(77, s.Brush.Noise.Seed);
    }
}
