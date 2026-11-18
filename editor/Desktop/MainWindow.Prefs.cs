using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// What is remembered between runs (Prefs, as the web editor kept it in the browser): each control set from it now
    /// and remembered as it changes.
    /// </summary>
    private void RememberPrefs()
    {
        var p = Prefs;
        // View: the kinds drawn, water, overlays, the look's slope colours and height lines.
        foreach (var (k, box) in _kindBoxes)
        {
            p.Bind(box, $"view.show.{k}");
        }
        p.Bind(WaterBox, "view.water");
        p.Bind(UnsavedBox, "view.unsaved");
        // The look: the game's or plain, buildings see-through, the 3D view's resolution.
        p.Bind(ModelsBox, "view.models");
        p.Bind(SeeThroughBox, "view.seeThrough");
        p.Bind(NamesBox, "view.names");
        p.Bind(BlockBox, "view.block");
        p.Bind(ResolutionBox, "view.res3d");
        // The brush's shape and falloff, Select's options.
        p.Bind(Tools.ShapeBox, "brush.shape");
        p.Bind(Tools.FalloffBox, "brush.falloff");
        p.Bind(SelectPanel.GroundBox, "select.onGround");
        p.Bind(SelectPanel.ZoneBox, "select.zone");
        p.Bind(SelectPanel.ThroughBox, "select.through");
        p.Bind(SelectPanel.PickBox, "select.picks");
        // The right-hand panel open.
        ShowRight(ChosenRight());
        // The clipboard (its objects' ids only meant something in the world it came from).
        if (p.TryGet("clipboard", out System.Text.Json.Nodes.JsonObject? clip) && clip != null)
        {
            try
            {
                View.Paste.Clip = CopyFormat.FromJson(clip);
                _keptClip = View.Paste.Clip;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException or ArgumentException or System.Text.Json.JsonException)
            {
                Log.Caught(ex, "restoring the clipboard kept in the preferences (it is dropped)");
                p.Remove("clipboard");
            }
        }
        View.Paste.Changed += () =>
        {
            if (View.Paste.Clip is { } c && !ReferenceEquals(c, _keptClip))
            {
                _keptClip = c;
                p.Set("clipboard", CopyFormat.ToJson(c));
            }
        };
    }
}
