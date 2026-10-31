using System.Numerics;

namespace Groundwork.Desktop;

// Showing an area: the scene, the camera over its middle, the session's changes followed, and the game look made for it.
public sealed partial class GlView
{
    /// <summary>
    /// Shows a scene: the camera over its middle, the selection cleared, the game look read again for it
    /// (on a worker thread) and its session's changes followed.
    /// </summary>
    /// <param name="scene">The area to show.</param>
    /// <param name="models">Its objects' models, null to draw them as boxes.</param>
    public void Show(WorldScene scene, ModelStore? models)
    {
        Scene = scene;
        Models = models;
        int g = (scene.H / 2) * scene.W + scene.W / 2;
        lock (_camLock)
        {
            _target = new Vector3(0, scene.Heights[g], 0);
        }
        // Another area: the selection goes now (its indices were the last one's), before anything is
        // selected in this one.
        lock (_selection)
        {
            _selection.Clear();
        }
        SelectionDone();
        _hidden = [];
        _sceneDirty = true;
        _marksDirty = true;
        // The last area's session is not followed any more: its late events hold its indices, not this scene's.
        if (_followed is { } old)
        {
            old.Changed -= Wake;
            old.ThingsChanged -= OnThingsChanged;
        }
        _followed = scene.Session;
        if (scene.Session != null)
        {
            scene.Session.Changed += Wake;
            scene.Session.ThingsChanged += OnThingsChanged;
        }
        Wake();
    }
    /// <summary>The session whose changes are followed (the shown scene's), null when none.</summary>
    private EditSession? _followed;

}
