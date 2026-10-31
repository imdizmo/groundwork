namespace Groundwork.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>Why Claude cannot change the open map now, or null.</summary>
    private string? CannotEdit() => _w.Session == null ? "No map is open: map_open first." : null;

    /// <summary>
    /// A history label of Claude's: the "Claude: " mark, which stays the same in every language since Take back
    /// Claude's changes finds Claude's steps by it (HistoryPanel.ByClaude), then what the step did.
    /// </summary>
    /// <param name="text">What the step did: in the user's language, or the words Claude gave.</param>
    internal static string ClaudesStep(string text) => "Claude: " + text;
}
