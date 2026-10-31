using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

/// <summary>
/// Claude's undo and redo of the open area's changes (undo, redo), as Ctrl+Z and Ctrl+Y, and the changes pending in words.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>undo: the last change of the area, anyone's, as Ctrl+Z.</summary>
    [McpServerTool(Name = "undo", Title = "Undo")]
    [Description("Takes back the last change of the map (anyone's: Claude's or the user's), as Ctrl+Z.")]
    public Task<string> Undo() => OnUi(async () =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.Session is not { CanUndo: true })
        {
            return "Nothing to undo.";
        }
        await _w.Undo();
        return "Undone. Now pending: " + Pending();
    });

    /// <summary>redo: the last change undone, as Ctrl+Y.</summary>
    [McpServerTool(Name = "redo", Title = "Redo")]
    [Description("Puts back the last change undone, as Ctrl+Y.")]
    public Task<string> Redo() => OnUi(async () =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.Session is not { CanRedo: true })
        {
            return "Nothing to redo.";
        }
        await _w.Redo();
        return "Redone. Now pending: " + Pending();
    });

    /// <summary>The changes not saved yet, in words.</summary>
    private string Pending() => _w.MapDirty ? "changes not saved yet (map_save writes them)." : "nothing.";
}
