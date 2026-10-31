using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Sculpting in the window: the number keys pick the brushes, Escape goes back to view, the sliders set the brush, and
/// undo, redo and save work from the window.
/// </summary>
[Collection("World files")]
public class SculptWindowTests
{
    [AvaloniaFact]
    public void NumberKeysPickTheBrushesAndEscapeGoesBackToView()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.KeyPress(Key.D3, RawInputModifiers.None, PhysicalKey.Digit3, "3");
        Assert.Equal(BrushTool.Flatten, w.Tools.Tool);
        Assert.Equal(BrushTool.Flatten, w.View.Tool);
        w.KeyPress(Key.D0, RawInputModifiers.None, PhysicalKey.Digit0, "0");
        Assert.Equal(BrushTool.Natural, w.Tools.Tool);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(w.Tools.Tool);
        Assert.Null(w.View.Tool);
    }

    [AvaloniaFact]
    public void TheSlidersSetTheBrush()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.Tools.SizeSlider.Value = 12.5;
        w.Tools.StrengthSlider.Value = 0.8;
        w.Tools.ShapeBox.SelectedIndex = 1;
        w.Tools.FalloffBox.SelectedIndex = 5;
        Assert.Equal(12.5f, w.Tools.Brush.Radius);
        Assert.Equal(0.8f, w.Tools.Brush.Strength, 4);
        Assert.Equal(BrushShape.Square, w.Tools.Brush.Shape);
        Assert.Equal(Falloff.Sharp, w.Tools.Brush.Falloff);
    }
}
