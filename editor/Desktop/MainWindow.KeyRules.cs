using Avalonia.Input;

namespace Groundwork.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The 3D editor's keys, in the order they are tried: the first rule that matches wins.
    /// </summary>
    private KeyRule[] KeyRules()
    {
        bool InMode(ToolMode mode) => Tools.Mode == mode;
        bool Pasting(KeyPress _) => InMode(ToolMode.Paste);
        return
        [
            Take([Key.Escape], KeyMods.NoCtrl, _ => View.PickObjectOnce != null, _ =>
            {
                View.PickObjectOnce = null;
                Msg("view.msg.pickCancelled");
            }),
            // The right-hand panels, as in the web editor: V the View panel, L the history, ? (or F3) the help. ? is Shift
            // and the / key (OemQuestion and Oem2 are that one key): / alone is not it.
            Take([Key.F3], KeyMods.NoCtrl, null, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.OemQuestion], KeyMods.NoCtrl, p => p.Shift, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.V], KeyMods.None, null, _ => ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard)),
            Take([Key.L], KeyMods.None, null, _ => ShowRight(History.Card.IsVisible ? null : History.Card)),
            Take([Key.Space], KeyMods.Ctrl, _ => InMap, _ => _drawer?.Toggle()),
            Take([Key.Z], KeyMods.Ctrl, null, p => _ = p.Shift ? Redo() : Undo()),
            Take([Key.Y], KeyMods.Ctrl, null, p => _ = Redo()),
            Take([Key.C], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Select, _ => Copy()),
            Take([Key.X], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Select, _ => Cut()),
            Take([Key.V], KeyMods.Ctrl, null, _ => StartPaste()),
            // Pasting: R turns a quarter, F mirrors, , and . turn by 1° (Shift: 15°), Esc ends it.
            Take([Key.R], KeyMods.NoCtrl, Pasting, _ => View.Paste.TurnBy(90)),
            Take([Key.F], KeyMods.NoCtrl, Pasting, _ =>
            {
                View.Paste.Mirror = !View.Paste.Mirror;
                View.Paste.Notify();
            }),
            Take([Key.OemComma, Key.OemPeriod], KeyMods.NoCtrl, Pasting, p => TurnPaste(p.Key, p.Shift)),
            Take([Key.Escape], KeyMods.NoCtrl, Pasting, _ => Tools.ChooseSelect()),
            Take([Key.S], KeyMods.Ctrl, null, p => _ = Save()),
            Take([Key.OemOpenBrackets, Key.OemCloseBrackets], KeyMods.NoCtrl, null, p =>
            {
                Tools.ResizeBrush(p.Key == Key.OemOpenBrackets ? -1 : 1);
                if (InMap && Session is { } s)
                {
                    Msg(new Groundwork.App.Said($"Brush {s.Brush.Radius:0} m · strength {s.Brush.Strength:0.00}", ""));
                }
            }),
            .. SelectKeyRules(),
            // A mod's own tool's keys (any modifiers): what it turns down goes on.
            Offer(null, KeyMods.Any, _ => InMode(ToolMode.Mod), p => ModToolKey(p.Key, p.Mods)),
            // The Select tool's own keys (any modifiers): what it turns down goes on.
            Offer(null, KeyMods.Any, _ => Tools.SelectMode, p => View.SelectTool.Key(p.Key, p.Shift)),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Measure) && View.Tape.A != null, _ => View.Tape.Clear()),
            // The rail's keys: the tool panel acts on them, and they stay unhandled.
            Pass([Key.Escape], KeyMods.NoCtrl, null, _ => Tools.Key("Escape")),
            Pass([Key.E, Key.M, Key.O, Key.H], KeyMods.NoCtrl, null, p => Tools.Key(p.Key.ToString())),
            Pass(Digits(Key.D0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.D0).ToString())),
            Pass(Digits(Key.NumPad0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.NumPad0).ToString())),
        ];
    }

    /// <summary>
    /// The Select tool's selection keys, as Blender's, tried where <see cref="KeyRules"/> lists them: Alt + Z Select
    /// through, Ctrl + A all, Alt + A none, Ctrl + I invert, Ctrl + H hide, Alt + H show again, Shift + F or Numpad .
    /// frame (F alone turns the eye).
    /// </summary>
    private KeyRule[] SelectKeyRules() =>
    [
        Take([Key.Z], KeyMods.NoCtrl, p => Tools.SelectMode && p.Alt, _ => SelectPanel.ThroughBox.IsChecked = SelectPanel.ThroughBox.IsChecked != true),
        Take([Key.A], KeyMods.Ctrl, _ => Tools.SelectMode, _ => View.SelectTool.SelectAll()),
        Take([Key.A], KeyMods.NoCtrl, p => Tools.SelectMode && p.Alt, _ => View.SelectTool.SelectNone()),
        Take([Key.I], KeyMods.Ctrl, _ => Tools.SelectMode, _ => View.SelectTool.Invert()),
        Take([Key.H], KeyMods.Ctrl, _ => Tools.SelectMode, _ => View.SelectTool.HideSelected()),
        Take([Key.H], KeyMods.NoCtrl, p => Tools.SelectMode && p.Alt, _ => View.SelectTool.ShowHidden()),
        Take([Key.F, Key.Decimal], KeyMods.NoCtrl, p => Tools.SelectMode && (p.Shift || p.Key == Key.Decimal), _ => View.SelectTool.Frame()),
    ];
}
