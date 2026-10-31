using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The tool rail on its own: Alt + click's flatten height.
/// </summary>
public class ToolPanelBranchesTests
{
    [AvaloniaFact]
    public void FlattenToLevelsToTheHeightGiven()
    {
        var t = new ToolPanel();
        t.FlattenTo(33.27f);
        Assert.Equal(BrushTool.Flatten, t.Tool);
        Assert.False(t.TargetFromClickBox.IsChecked);
        Assert.Equal(33.3f, t.Brush.Target, 3);
        Assert.Equal(33.3m, t.TargetBox.Value);
    }
}
