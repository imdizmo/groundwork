using System.Text.Json;

namespace Groundwork.Desktop;

/// <summary>
/// What the driver answers with after each command: the state of the window, as JSON.
/// </summary>
public static partial class Driver
{
    /// <summary>What is shown, as JSON: page, objects, pending, frames drawn, the camera, the map, models and more.</summary>
    private static string State(MainWindow w)
    {
        var s = w.Session;
        var fields = new Dictionary<string, object?>
        {
            ["page"] = s != null ? "editor" : "start",
            ["objects"] = s?.Scene.Things.Count(t => !t.Gone) ?? 0,
            ["unsaved"] = w.MapDirty,
            ["frames"] = w.View.FramesDrawn,
            ["glErrors"] = w.View.GlErrors,
            ["selected"] = w.View.Selected.Count,
            ["mode"] = w.Tools.Mode.ToString(),
            ["message"] = w.MessageText.Text ?? "",
            ["yaw"] = w.View.Camera.Yaw,
            ["distance"] = w.View.Camera.Distance,
            ["targetX"] = w.View.Camera.Target.X,
            // The open area: its south-west zone and its size in zones.
            ["area"] = w.View.Scene is { } sc ? new[] { sc.X0, sc.Z0, sc.ZonesX, sc.ZonesZ } : null,
        };
        AddMapFields(w, fields);
        return JsonSerializer.Serialize(fields);
    }
}
