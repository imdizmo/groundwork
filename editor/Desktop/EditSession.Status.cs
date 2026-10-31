
namespace Groundwork.Desktop;

/// <summary>
/// The status the window shows: what can be undone and redone, whether a stroke goes on.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Whether there is a step to undo.
    /// </summary>
    public bool CanUndo { get { lock (_lock) { return _undo.Count > 0; } } }

    /// <summary>
    /// Whether there is a step to redo.
    /// </summary>
    public bool CanRedo { get { lock (_lock) { return _redo.Count > 0; } } }

    /// <summary>
    /// The label of the next undo; null when there is none.
    /// </summary>
    public string? UndoLabel { get { lock (_lock) { return _undo.Count > 0 ? _undo[^1].Label : null; } } }

    /// <summary>
    /// The label of the next redo; null when there is none.
    /// </summary>
    public string? RedoLabel { get { lock (_lock) { return _redo.Count > 0 ? _redo[^1].Label : null; } } }

    /// <summary>
    /// Whether a brush stroke is going on.
    /// </summary>
    public bool Stroking { get { lock (_lock) { return _stroke != null; } } }
}
