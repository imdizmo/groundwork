using System.ComponentModel;
using ModelContextProtocol.Server;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Claude's tools for looking at the editor: its state, and what the open map holds (describe_map, and the kind of each object).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>editor_state: the page shown, the world, the area's bounds, what is pending (see <see cref="StateOf"/>).</summary>
    [McpServerTool(Name = "editor_state", ReadOnly = true, Title = "What the editor shows")]
    [Description("What the editor shows now: the start page or a map in 3D; the map's bounds in editor coordinates, whether it has changes not saved yet, and the editor's last message. Call it first.")]
    public Task<string> EditorState() => OnUi(() => ToJson(StateOf(_w)));

    /// <summary>describe_map: the open map's bounds, heights and objects counted by kind and by name (the top 40).</summary>
    [McpServerTool(Name = "describe_map", ReadOnly = true, Title = "Describe the open map")]
    [Description("The open map: its bounds, ground heights (lowest, highest, average; the water level), and its objects counted by kind and by name.")]
    public Task<string> DescribeMap() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No map is open (map_open).";
        }
        float[] h = scene.Heights;
        List<(string Name, string Kind)> things;
        lock (scene.Things)
        {
            things = [.. scene.Things.Where(t => !t.Gone).Select(t => (Name: _w.NameOfPrefab(t.Prefab) ?? t.Prefab.ToString(), Kind: KindOf(t)))];
        }
        return ToJson(new
        {
            area = StateOf(_w),
            ground = new { lowest = h.Min(), highest = h.Max(), average = MathF.Round(h.Average(), 2), water = scene.Water },
            objectsByKind = things.GroupBy(t => t.Kind).OrderByDescending(g => g.Count()).Select(g => new { kind = g.Key, count = g.Count() }),
            objectsByName = things.GroupBy(t => t.Name).OrderByDescending(g => g.Count()).Take(40).Select(g => new { name = g.Key, count = g.Count() }),
        });
    });

    /// <summary>
    /// The refusal when only one of a point's two coordinates is given (null: both or neither): the other was dropped
    /// or mixed with a default without a word.
    /// </summary>
    /// <param name="x">The point's x, if given.</param>
    /// <param name="z">The point's z, if given.</param>
    /// <param name="xName">The x argument's name.</param>
    /// <param name="zName">The z argument's name.</param>
    internal static string? LoneCoordinate(float? x, float? z, string xName = "x", string zName = "z") =>
        x.HasValue == z.HasValue ? null : $"Give {xName} and {zName} together, or neither: only {(x.HasValue ? xName : zName)} was given.";

    /// <summary>A thing's object kind, as describe_map and list_objects name it.</summary>
    private string KindOf(WorldScene.Thing t) => ObjectKinds.Of(_w.NameOfPrefab(t.Prefab), t.Piece).ToString();
}
