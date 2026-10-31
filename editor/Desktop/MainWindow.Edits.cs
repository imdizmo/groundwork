using Avalonia.Media;
using Avalonia.Threading;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The test driver's quit: what is pending is dropped without asking.
    /// </summary>
    internal void CloseWithoutAsking()
    {
        _closeAnyway = true;
        Close();
    }

    /// <summary>
    /// Starts editing a loaded scene (also used by tests with a scene of their own).
    /// </summary>
    internal void Edit(EditSession session)
    {
        Session = session;
        session.Brush = Tools.Brush;
        session.Mask = Mask;
        // What is placed stays visible: its kinds switched off in View are switched on.
        session.ThingsAdded += indices => Dispatcher.UIThread.Post(() => ShowKindsOf(session.Scene, indices));
        session.Changed += () => Dispatcher.UIThread.Post(() => AfterChange(session));
        SelectPanel.FillSaved();
        UpdateSaveBar();
    }

    /// <summary>
    /// After a change of the session edited (an edit, an undo, a redo): the hidden groups, Live, the save bar, the
    /// object count under the map's name, the history and the tool's own readouts follow.
    /// </summary>
    /// <param name="session">The session that changed.</param>
    private void AfterChange(EditSession session)
    {
        if (InMap && MapTools.Hidden.Count > 0)
        {
            MapTools.ApplyHidden(this);
        }
        if (InMap)
        {
            _ = LiveChanged();
        }
        UpdateSaveBar();
        // The object count under the map's name follows what was placed, deleted or undone.
        if (Session == session)
        {
            ShowAreaInfo(session.Scene);
        }
        History.Refresh();
        RefreshToolAfterChange();
    }

    /// <summary>
    /// The tool in use's readouts after a change: the tape's lowest and highest ground.
    /// </summary>
    private void RefreshToolAfterChange()
    {
        switch (Tools.Mode)
        {
            case ToolMode.Measure:
                MeasurePanel.Refresh();
                break;
            default:
                // The other tools show nothing that a change makes stale.
                break;
        }
    }

    /// <summary>The save bar's pill while something waits: amber background, border and text.</summary>
    private static readonly IImmutableSolidColorBrush PillWaiting = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#2c2416")),
        PillWaitingBorder = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7a5a2a")), PillWaitingText = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#f3d29b"));
}
