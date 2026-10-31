namespace Groundwork.Desktop;

/// <summary>
/// What the editor shows, and why a map cannot be left yet, as Claude reads it (editor_state and the other answers).
/// </summary>
public sealed partial class ClaudeTools
{

    /// <summary>
    /// What the window shows, for editor_state and other answers: the page (start or map), the map's
    /// bounds in metres, whether it is saved, whether undo is possible, and the last message.
    /// </summary>
    internal static object StateOf(MainWindow w)
    {
        var s = w.Session;
        var scene = s?.Scene;
        string page = s != null ? "map" : "start";
        return new
        {
            page,
            area = scene == null ? null : new
            {
                name = scene.Name,
                minX = scene.OriginX,
                minZ = scene.OriginZ,
                maxX = scene.OriginX + scene.ZonesX * 64f,
                maxZ = scene.OriginZ + scene.ZonesZ * 64f,
                water = scene.Water,
                objects = scene.Things.Count(t => !t.Gone),
            },
            unsaved = w.MapDirty,
            canUndo = s?.CanUndo ?? false,
            message = w.LastMessageEn,
        };
    }
}
