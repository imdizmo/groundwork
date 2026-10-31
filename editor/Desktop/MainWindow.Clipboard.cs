using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Ctrl+C: the Select tool's objects.</summary>
    internal void Copy()
    {
        if (CopyToClipboard() is { } clip)
        {
            Msg(Said.Of("paste.msg.copiedObjects", clip.Objects.Count));
        }
    }

    /// <summary>
    /// Ctrl+X: <see cref="Copy"/>, then the objects copied leave the map (one undo step), to be pasted elsewhere.
    /// </summary>
    internal void Cut()
    {
        if (Session is not { } s || CopyToClipboard() is not { } clip || clip.Taken.Count == 0)
        {
            return;
        }
        s.Delete(clip.Taken);
        View.Select([]);
        Msg(Said.Of("paste.msg.cutObjects", clip.Objects.Count));
        UpdateSaveBar();
    }

    /// <summary>Puts the Select tool's objects on the clipboard.</summary>
    /// <returns>The copy, or null (the message says what was missing).</returns>
    private CopyData? CopyToClipboard()
    {
        if (Session is not { } s)
        {
            return null;
        }
        View.SelectTool.Commit();
        if (CopyData.FromThings(s.Scene, [.. View.Selected], NameOfPrefab, View.GroundHeight) is not { } clip)
        {
            Msg("paste.msg.copyFirst");
            return null;
        }
        View.Paste.Clip = clip;
        View.Paste.Notify();
        return clip;
    }

    /// <summary>Ctrl+V: the Paste tool.</summary>
    internal void StartPaste()
    {
        if (View.Paste.Clip == null)
        {
            Msg("paste.msg.copyFirst");
            return;
        }
        View.SelectTool.Commit();
        Tools.ChooseMode(ToolMode.Paste);
        PastePanel.Refresh();
    }
}
