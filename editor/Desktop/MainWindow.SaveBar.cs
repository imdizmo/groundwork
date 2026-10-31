using Avalonia.Controls;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>The save bar's pill: amber while something waits, grey when all is saved.</summary>
    private void SetPendingPill(bool waiting)
    {
        _pendingPill.Background = waiting ? PillWaiting : Brushes.Transparent;
        _pendingPill.BorderBrush = waiting ? PillWaitingBorder : Ui.Line;
        PendingText.Foreground = waiting ? PillWaitingText : Ui.Muted;
    }

    /// <summary>The save bar as the map now stands: what waits to be saved, Save, undo and redo.</summary>
    private void UpdateSaveBar()
    {
        if (Session is not { } s)
        {
            return;
        }
        MapSaveBar();
        UndoButton.IsEnabled = s.CanUndo;
        RedoButton.IsEnabled = s.CanRedo;
        ToolTip.SetTip(UndoButton, s.UndoLabel is string u ? Lang.T("top.undoNamed", u) : Lang.T("top.undoNone"));
        ToolTip.SetTip(RedoButton, s.RedoLabel is string rl ? Lang.T("top.redoNamed", rl) : Lang.T("top.redoNone"));
    }

    /// <summary>Ctrl+Z: takes the last step back.</summary>
    internal Task Undo()
    {
        View.SelectTool.Commit();
        _ = Session?.Undo();
        UpdateSaveBar();
        return Task.CompletedTask;
    }
}
