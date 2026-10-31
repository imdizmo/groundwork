using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// An extension's tool's edit (ToolEdit) made one step of a map's history: its added objects made with their data, its
/// changed ones replaced by a copy with the tool's changes (same asset, place, turn and size), its removed ones taken
/// away.
/// </summary>
internal static class ToolCommit
{
    /// <summary>What an edit comes to: things to take away, new ones (with whether each is a building part), and what was skipped.</summary>
    internal sealed record Plan(List<int> RemoveThings, List<(NewObject, bool)> AddThings, List<string> Skipped);

    /// <summary>Makes an edit one step of the session's history; what it could not do is listed (an object not from the map).</summary>
    /// <param name="s">The map's session.</param>
    /// <param name="label">The step's name.</param>
    /// <param name="edit">The edit.</param>
    public static Plan Apply(EditSession s, string label, ToolEdit edit)
    {
        var plan = PlanOf(s, edit);
        _ = s.Commit(label, null, plan.RemoveThings, plan.AddThings);
        return plan;
    }

    /// <summary>What an edit comes to in a session.</summary>
    internal static Plan PlanOf(EditSession s, ToolEdit edit)
    {
        var plan = new Plan([], [], []);
        foreach (var (asset, position, rotation, data) in edit.Added)
        {
            Add(plan, new NewObject(0, AssetIds.Of(asset), position, rotation, 1, data is { Count: > 0 } ? new Dictionary<string, string>(data) : null));
        }
        foreach (var (obj, change) in edit.Changed)
        {
            if (Remove(s, plan, obj) is not { } view)
            {
                continue;
            }
            var data = new Dictionary<string, string>(view.Data);
            foreach (var (key, value) in change)
            {
                if (value == null)
                {
                    _ = data.Remove(key);
                }
                else
                {
                    data[key] = value;
                }
            }
            Add(plan, new NewObject(0, AssetIds.Of(view.Asset), view.Position, view.Rotation, view.Scale, data));
        }
        foreach (var obj in edit.Removed)
        {
            _ = Remove(s, plan, obj);
        }
        return plan;
    }

    /// <summary>A new object: a building part when its catalog's kind is Buildings.</summary>
    private static void Add(Plan plan, NewObject n) => plan.AddThings.Add((n, ObjectKinds.Of(AssetIds.NameOf(n.Prefab)) == ObjectKind.Buildings));

    /// <summary>
    /// Takes an object of the map away, by its id. Null (listed as skipped) for one the editor did not hand over (no
    /// id) or no longer has.
    /// </summary>
    private static ObjectView? Remove(EditSession s, Plan plan, IObjectView obj)
    {
        if (obj is not ObjectView { Id: int id } view)
        {
            plan.Skipped.Add(obj?.Asset ?? "");
            return null;
        }
        int index;
        lock (s.Scene.Things)
        {
            index = s.Scene.Things.FindLastIndex(t => t.Id == id && !t.Gone);
        }
        if (index < 0)
        {
            plan.Skipped.Add(view.Asset);
            return null;
        }
        plan.RemoveThings.Add(index);
        return view;
    }
}
