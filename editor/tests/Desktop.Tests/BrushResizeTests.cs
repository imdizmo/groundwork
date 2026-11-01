using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>[ and ] resize the brush by a tenth, at least half a metre, up to the largest size.</summary>
public class BrushResizeTests
{
    [AvaloniaFact]
    public void ResizingIsFineWhenSmallAndQuickWhenLarge()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.Tools.SizeSlider.Value = 2;
        w.Tools.ResizeBrush(1);
        Assert.Equal(2.5, w.Tools.SizeSlider.Value);   // half a metre: a tenth would not move it
        w.Tools.SizeSlider.Value = 200;
        w.Tools.ResizeBrush(1);
        Assert.Equal(220, w.Tools.SizeSlider.Value);
        w.Tools.ResizeBrush(-1);
        Assert.Equal(200, w.Tools.SizeSlider.Value);
        w.Tools.SizeSlider.Value = 295;
        w.Tools.ResizeBrush(1);
        Assert.Equal(300, w.Tools.SizeSlider.Value);   // no further than the largest
    }
}
