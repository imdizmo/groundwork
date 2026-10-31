using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A stroke ended: its message, Flatten's target when it came from the click, and the save bar.
    /// </summary>
    /// <param name="msg">What the stroke did.</param>
    private void OnStrokeEnded(Said msg)
    {
        Msg(msg);
        if (Tools.Tool == BrushTool.Flatten && Tools.Brush.TargetFromClick)
        {
            Tools.ShowTarget(Tools.Brush.Target);
        }
        UpdateSaveBar();
    }

    /// <summary>
    /// The editor's keys, and Shift held shown by the Place brush (a drag removes).
    /// </summary>
    private void WireKeys()
    {
        KeyDown += OnKey;
    }
}
