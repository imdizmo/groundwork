using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

// Claude's tools for the map's names (MapNames): a name on a place, a named region, removing one. map_catalog lists them.
public sealed partial class ClaudeTools
{
    /// <summary>One undo step for a change of the names.</summary>
    private void NamesStep(string label, string before)
    {
        MapNames.Touch();
        string after = MapNames.Snapshot();
        _ = _w.Session!.RecordExtra(label + " (Claude)", again => MapNames.Restore(again ? after : before));
    }

    /// <inheritdoc/>
    [McpServerTool(Name = "map_name", Title = "Name a place on the map")]
    [Description("Puts a name on the map at x, z: size region (big, seen from anywhere), place (a town, a bay, a peak) or small (shown up close only). A name already there with the same text is moved. One undo step. Names are text the user reads; map_catalog lists them.")]
    public Task<string> MapName(string text, float x, float z, [Description("region, place or small")] string size = "place") => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        string clipped = MapNames.Clip(text);
        if (clipped.Length == 0)
        {
            return "A name needs some text.";
        }
        if (!Enum.TryParse<MapNames.Size>(size, ignoreCase: true, out var sz) || !Enum.IsDefined(sz))
        {
            return "size is region, place or small.";
        }
        string before = MapNames.Snapshot();
        var label = MapNames.Labels.FirstOrDefault(l => string.Equals(l.Text, clipped, StringComparison.OrdinalIgnoreCase));
        if (label == null)
        {
            label = new MapNames.Label();
            MapNames.Labels.Add(label);
        }
        (label.Text, label.X, label.Z, label.Size) = (clipped, x, z, sz);
        NamesStep("Name " + clipped, before);
        return $"Named {clipped} at {x:0}, {z:0} ({size}).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_region", Title = "Draw a named region on the map")]
    [Description("A named region: its outline as points [x, z] going round it (three or more); its name shows over its middle. A region of the same name is replaced. One undo step.")]
    public Task<string> MapRegion(string name, float[][] points) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        string clipped = MapNames.Clip(name);
        if (clipped.Length == 0 || points.Length < 3 || points.Length > 2000 || points.Any(p => p.Length < 2 || !float.IsFinite(p[0]) || !float.IsFinite(p[1])))
        {
            return "A region needs a name and 3 to 2000 points [x, z].";
        }
        string before = MapNames.Snapshot();
        _ = MapNames.Regions.RemoveAll(r => string.Equals(r.Name, clipped, StringComparison.OrdinalIgnoreCase));
        var region = new MapNames.Region { Name = clipped };
        region.Points.AddRange(points.Select(p => new Vector2(p[0], p[1])));
        MapNames.Regions.Add(region);
        NamesStep("Region " + clipped, before);
        return $"Region {clipped}: {MathF.Abs(MapNames.Area(region)) / 1e4f:0.#} ha.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_name_remove", Title = "Remove a name or region from the map")]
    [Description("Removes the name or the region with that text. One undo step.")]
    public Task<string> MapNameRemove(string text) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        string before = MapNames.Snapshot();
        int n = MapNames.Labels.RemoveAll(l => string.Equals(l.Text, text, StringComparison.OrdinalIgnoreCase))
            + MapNames.Regions.RemoveAll(r => string.Equals(r.Name, text, StringComparison.OrdinalIgnoreCase));
        if (n == 0)
        {
            return $"No name or region \"{text}\".";
        }
        NamesStep("Remove " + text, before);
        return $"{text} removed.";
    });
}
