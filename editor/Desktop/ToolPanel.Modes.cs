using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>A brush, or (null) the mode's tool.</summary>
    public void Choose(BrushTool? t, ToolMode mode = ToolMode.View)
    {
        Tool = t;
        Mode = t != null ? ToolMode.Brush : mode;
        _viewButton.Classes.Set("on", Mode == ToolMode.View);
        SelectButton.Classes.Set("on", Mode == ToolMode.Select);
        _measureButton.Classes.Set("on", Mode == ToolMode.Measure);
        if (Mode != ToolMode.Mod)
        {
            ModTool = null;
        }
        MarkModTool();
        foreach (var (k, b) in _buttons)
        {
            b.Classes.Set("on", k == t);
        }
        Options.IsVisible = t != null;
        MarkMapRail(t);
        if (t is BrushTool tool)
        {
            _title.Text = Brush.Label(tool);
            _help.Text = Lang.T("brush.helpHold", Brush.Help(tool));
            _flattenRows.IsVisible = tool == BrushTool.Flatten;
            _naturalRows.IsVisible = tool == BrushTool.Natural;
            _erodeRows.IsVisible = tool == BrushTool.Erode;
            _terraceRows.IsVisible = tool == BrushTool.Terrace;
            WholeMapButton.IsVisible = IsPaint(tool) || tool == BrushTool.Erode;
            WholeMapButton.Content = Lang.T(tool == BrushTool.Erode ? "brush.label.erodeWholeMap" : "brush.label.wholeMap");
            SyncStamp();
        }
        ToolChanged?.Invoke(t);
    }
}
