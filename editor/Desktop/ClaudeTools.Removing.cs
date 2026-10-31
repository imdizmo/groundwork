using System.ComponentModel;
using ModelContextProtocol.Server;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Claude's removal of objects (remove_objects), as one pending step.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>remove_objects: takes objects away by id, as one pending step.</summary>
    [McpServerTool(Name = "remove_objects", Title = "Take objects away")]
    [Description("Takes objects of the open map away, by their ids (list_objects), as one step of the history (pending).")]
    public Task<string> RemoveObjects([Description("Their ids.")] int[] ids) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var session = _w.Session!;
        if (StaleIds(session.Scene) is string stale)
        {
            return stale;
        }
        var things = session.Scene.Things;
        int[] asked = [.. ids.Distinct()];
        List<int> given;
        lock (things)
        {
            given = [.. asked.Where(i => i >= 0 && i < things.Count && !things[i].Gone)];
        }
        if (given.Count == 0)
        {
            return "None of those ids is an object of the map.";
        }
        var ok = given;
        session.Commit(ClaudesStep(Lang.T("claude.undo.tookAway", ok.Count)), null, ok, []);
        _w.AfterClaudeEdit(Said.Of("claude.msg.tookAway", ok.Count));
        return $"Took {ok.Count} away{(given.Count < asked.Length ? $" ({asked.Length - given.Count} id(s) were not objects of the area)" : "")}"
            + $". Now pending: {Pending()}";
    });
}
