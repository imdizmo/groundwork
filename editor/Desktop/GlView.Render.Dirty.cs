using Groundwork.App;

namespace Groundwork.Desktop;

// What changed since the last frame, built again before it is drawn: the groups and the shapes.
public sealed partial class GlView
{
    /// <summary>
    /// Builds again the groups that changed (once their models are ready) and the shapes.
    /// </summary>
    /// <param name="s">The area shown.</param>
    private void RebuildDirty(WorldScene s)
    {
        Group[] dirty;
        lock (_dirtyGroups)
        {
            dirty = [.. _dirtyGroups.Where(g => g.Ready)];
            _dirtyGroups.ExceptWith(dirty);
        }
        foreach (var g in dirty)
        {
            Rebuild(s, g);
        }
        if (_shapesDirty || _shapesGeneration != ModExtensions.Generation)
        {
            RebuildShapes(s);
        }
    }
}
