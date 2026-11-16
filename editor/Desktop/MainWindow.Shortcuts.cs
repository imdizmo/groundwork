using Avalonia.Controls;
using Avalonia.Threading;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Ctrl + wheel: pasting, the paste is lowered (into a mountain: Clear the site digs it out) or raised; building
    /// (the height cut), the object is lifted; otherwise (false) the wheel is not taken.
    /// </summary>
    /// <param name="dir">The wheel's direction: 1 up, -1 down.</param>
    /// <param name="shift">Shift held: finer steps.</param>
    /// <returns>Whether a tool took it.</returns>
    private bool CtrlWheel(int dir, bool shift)
    {
        // The map: Ctrl+wheel sizes the brush, Ctrl+Shift+wheel its strength (Brush and Biome: radius, density).
        if (InMap && Session is { } session && (Tools.Mode == ToolMode.Brush || Tools.ModTool?.Info.Id is "brush" or "biome"))
        {
            if (Tools.Mode == ToolMode.Brush)
            {
                if (shift)
                {
                    Tools.StrengthSlider.Value = Math.Clamp(Tools.StrengthSlider.Value + (dir * 0.05), Tools.StrengthSlider.Minimum, Tools.StrengthSlider.Maximum);
                }
                else
                {
                    Tools.ResizeBrush(dir);
                }
                Msg(new Said($"Brush {session.Brush.Radius:0} m · strength {session.Brush.Strength:0.00}", ""));
            }
            else
            {
                ModToolPanel.Nudge(shift ? "density" : "radius", dir * (shift ? 0.1f : 1f));
                Msg(new Said($"Radius {ModToolPanel.Number("radius"):0} m · density × {ModToolPanel.Number("density"):0.0#}", ""));
            }
            return true;
        }
        // Pasting: the paste lowered (into a mountain: Clear the site digs it out) or raised.
        if (Tools.Mode == ToolMode.Paste)
        {
            PastePanel.OffsetBox.Value = Math.Round((PastePanel.OffsetBox.Value ?? 0) + dir * (shift ? 0.1m : 0.5m), 2);
            Msg("view.msg.pasteHeight", PastePanel.OffsetBox.Value);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The status bar's cursor readout follows the mouse over the 3D view (and is read again after a stroke).
    /// </summary>
    /// <param name="surface">The control that takes the mouse for the 3D view.</param>
    private void TrackCursor(Control surface)
    {
        surface.PointerMoved += (_, e) => ShowCursor(e.GetPosition(surface));
        surface.PointerExited += (_, _) => ShowCursor(null);
        View.StrokeEnded += _ => ShowCursor(_cursorAt);
    }

    /// <summary>
    /// Shows the 3D view's report in the help card's frame rate line, in the language in use.
    /// </summary>
    /// <param name="s">The view's report: frames, work, objects, draws and the kinds still loading.</param>
    internal void ShowStats(GlView.Stats s)
    {
        if (InMap && _mapFps != (_mapFps = $"{s.WorkMs:0.0} ms a frame · {s.Objects:N0} objects"))
        {
            MapHints();
        }
        ShowFps(s);
    }

    private void ShowFps(GlView.Stats s) => _fps.Text = Lang.T("help.fps", s.Fps, s.WorkMs, s.Objects, s.Instances, s.Batches)
        + (s.PendingModels > 0 ? " " + Lang.T("help.fpsLoading", s.PendingModels) : "");

    /// <summary>
    /// What the 3D view reports, shown: frame rates, the selection (the Select panel, the Inspector and the status
    /// line follow it), walking or flying, and its status lines.
    /// </summary>
    private void WireViewReports()
    {
        View.StatsChanged += ShowStats;
        View.SelectionChanged += _ => FollowSelection();
        // The map says it once, on the selection bar.
        View.SelectionChanged += things => _selection.Text = things.Count == 0 || InMap ? "" : things.Count == 1
            ? Lang.T("view.selected.one", ThingName(things[0]), things[0].Position.X, things[0].Position.Z, things[0].Position.Y)
            : Lang.T("view.selected.many", things.Count, string.Join(", ", things.GroupBy(ThingName).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}")));
        View.EyeChanged += e => _eye.Text = e switch
        {
            GlView.EyeMode.Walk => Lang.T("view.eye.walk"),
            GlView.EyeMode.Fly => Lang.T("view.eye.fly"),
            _ => "",
        };
        View.Boxed += (n, why) => Msg(new Said($"{n} {(n == 1 ? "model" : "models")} could not be read and {(n == 1 ? "is" : "are")} drawn as boxes ({why}). The log has each one (Open log, on the start page).", ""));
        View.Status += t => { Options.Say(t.English); Dispatcher.UIThread.Post(() => LoadedInfo.Text = t.Shown + "\n" + LoadedInfo.Text); };
    }

    /// <summary>
    /// The selection changed: the Select panel shows it, and an open Inspector follows a single selected object.
    /// </summary>
    private void FollowSelection()
    {
        SelectPanel.Refresh();
    }
}
