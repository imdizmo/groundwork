using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A click in the Paste tool: one paste (with its repeats) is one undo step.
    /// </summary>
    /// <param name="at">The grid point clicked.</param>
    internal void PasteAt(System.Numerics.Vector2 at)
    {
        if (Session is not { } s || View.Paste.Clip == null)
        {
            return;
        }
        // Past the open map the ground could not be shaped.
        if (!View.Paste.Fits(at, s.Ground.W, s.Ground.H))
        {
            Msg("paste.msg.tooFar");
            return;
        }
        // The objects' heights come from the ground as the paste leaves it: the ground step fills the list,
        // which Commit reads after it (one undo step for both).
        var add = new List<(NewObject, bool)>();
        int touched = 0;
        var paste = View.Paste;
        string label = paste.Count > 1 ? Lang.T("paste.undo.copies", paste.Count) : Lang.T("paste.label.paste");
        s.Commit(label, g =>
        {
            var (t, rect, a) = paste.Apply(g, at, s.MaskNow());
            add.AddRange(a);
            touched = t.Count;
            return (t, rect);
        }, [], add);
        Msg(PastedSaid(paste.Count, paste.Ground, touched, add.Count, 0) + Said.Of("paste.msg.pasteMore"));
        UpdateSaveBar();
    }

    /// <summary>
    /// What a paste did, for the status bar: the copies, the ground (the copy's own, or with its ground left out only
    /// its site dug out), the objects placed and the nature cleared from the site.
    /// </summary>
    /// <param name="copies">How many copies were pasted.</param>
    /// <param name="ground">Whether the copy's ground was pasted (Ground shape and paint).</param>
    /// <param name="touched">How many ground points changed.</param>
    /// <param name="added">How many objects were placed.</param>
    /// <param name="cleared">How many trees, rocks and the like were cleared from the site.</param>
    internal static Said PastedSaid(int copies, bool ground, int touched, int added, int cleared) =>
        Said.Of("paste.msg.pasted",
            copies > 1 ? Said.Of("paste.msg.pastedCopies", copies) : Said.Empty,
            touched == 0 ? Said.Empty : ground ? Said.Of("paste.msg.pastedGround") : Said.Of("paste.msg.pastedSiteDug"),
            added > 0 ? Said.Of("paste.msg.pastedObjects", added) : Said.Empty,
            cleared > 0 ? Said.Of("paste.msg.pastedCleared", cleared) : Said.Empty);
}
