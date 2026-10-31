using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A control as Claude sees it.</summary>
    /// <param name="Id">How Claude names it: its tip key (view.models, select.all…), else ~ and its English label; #2… when shared.</param>
    /// <param name="Kind">switch, toggle, radio, dropdown, slider, number, text or button.</param>
    /// <param name="Label">Its words: its content, placeholder, or the label beside it.</param>
    /// <param name="Control">The control.</param>
    internal sealed record Ctl(string Id, string Kind, string Label, Control Control);

    /// <summary>Tip keys of controls Claude never sets: saving and forgetting.</summary>
    internal static readonly HashSet<string> BlockedKeys =
    [
        "top.save", "map.save", "select.delete", "select.forget", "brush.forgetStamp",
    ];

    /// <summary>
    /// English labels of controls without a tip key that Claude never sets (the same kinds of actions); a label in
    /// another language is matched by its English (Lang.EnglishOf).
    /// </summary>
    [GeneratedRegex(@"^(save|live|send|discard|reload|delete|import|forget)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedLabel();

    /// <summary>Why Claude may not set this control, or null when it may.</summary>
    internal string? Blocked(Ctl c)
    {
        if (ReferenceEquals(c.Control, _w.SaveButton))
        {
            return "saving is the user's (or map_save)";
        }
        if (Tips.KeyOf(c.Control) is string k && (BlockedKeys.Contains(k) || k.StartsWith("settings.", StringComparison.Ordinal)))
        {
            return "it saves, goes live or sends to Unreal, or writes the user's files: the user's to do";
        }
        if (Tips.KeyOf(c.Control) == null && c.Kind == "button" && BlockedLabel().IsMatch(Lang.EnglishOf(c.Label)))
        {
            return "it saves, discards or writes or deletes the user's files: the user's to do";
        }
        return null;
    }

    /// <summary>
    /// Presses a button as a pointer would, through its automation peer: a toggle changes first (so its handlers see
    /// the new value), then its Click handlers and its Command run. Raising the Click event alone would skip the
    /// toggle and the Command.
    /// </summary>
    private static void Press(Button b) => new ButtonAutomationPeer(b).Invoke();

    /// <summary>
    /// True while Claude is setting a control (Set): the steps the control's handlers make, at once or after an
    /// await, are labelled "Claude: …" by EditSession, as every other tool's are, so History and Take back Claude's
    /// changes know them. It flows with the execution context, so a handler's continuations inherit it and the
    /// user's own actions meanwhile do not.
    /// </summary>
    internal static readonly AsyncLocal<bool> Acting = new();
}
