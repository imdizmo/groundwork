using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Objects: removing, moving and copying them as undo steps, and taking them away or back in the scene.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Removes things from the map (on the next save) as one undo step; those already gone are skipped.
    /// </summary>
    /// <param name="indices">Indices into the scene's things.</param>
    public void Delete(IReadOnlyCollection<int> indices)
    {
        int[] live = [.. indices.Where(i => !Scene.Things[i].Gone).Distinct()];
        if (live.Length == 0)
        {
            return;
        }
        _ = Commit(Lang.T("history.undo.deleted", live.Length), null, live, []);
    }

    /// <summary>
    /// Moves (and turns) things as one undo step: each is replaced by a copy at its new place that keeps its own data,
    /// like the web editor's moves. Rotations are Euler angles in degrees.
    /// </summary>
    /// <returns>The copies' indices.</returns>
    public List<int> Move(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation)> moves, string? label = null) =>
        Transform([.. moves.Select(m => (m.Index, m.Position, m.Rotation, Scene.Things[m.Index].Scale))], label);

    /// <summary>
    /// Moves, turns and sizes things as one undo step, as Move does, each with its own scale (zero: the asset's own).
    /// </summary>
    /// <returns>The copies' indices, in the order of the things not gone.</returns>
    public List<int> Transform(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> moves, string? label = null)
    {
        var live = moves.Where(m => !Scene.Things[m.Index].Gone).ToList();
        var adds = live.Select(m => (new NewObject(0, Scene.Things[m.Index].Prefab, m.Position, m.Rotation, m.Scale, Edits.FindAdded(Scene.Things[m.Index].Id)?.Data), Scene.Things[m.Index].Piece)).ToList();
        return Commit(label ?? Lang.T("history.undo.moved", live.Count), null, [.. live.Select(m => m.Index)], adds);
    }

    /// <summary>
    /// Takes things away or brings them back in the scene (a thing gone stays in the list, so it can come back).
    /// </summary>
    internal void SetGone(IEnumerable<(int Index, bool Gone)> things)
    {
        var list = Scene.Things;
        lock (list)
        {
            foreach (var (i, gone) in things)
            {
                list[i] = list[i] with { Gone = gone };
            }
        }
        Edits.ObjectsChanged();
    }

    /// <summary>
    /// Takes the things away and adds the new objects of a step, in the scene, under its things' lock.
    /// </summary>
    /// <param name="remove">Things to take away (bad or already gone ones are skipped).</param>
    /// <param name="add">New objects with whether each is a piece.</param>
    /// <returns>The indices of the things added, the things' changes (index, gone before, gone after) and the objects
    /// as they were given their ids.</returns>
    internal (List<int> Indices, List<(int, bool, bool)> Changes, List<NewObject> Added) ChangeThings(
        IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add)
    {
        var list = Scene.Things;
        var added = new List<NewObject>();
        var indices = new List<int>();
        var changes = new List<(int, bool, bool)>();
        lock (list)
        {
            // (Indices from elsewhere, a script's, are checked: a bad one changed the ground with no undo step.)
            foreach (int i in remove.Distinct())
            {
                if (i >= 0 && i < list.Count && !list[i].Gone)
                {
                    changes.Add((i, false, true));
                }
            }
            foreach (var (o, piece) in add)
            {
                var n = o with { Id = _nextId-- };
                added.Add(n);
                list.Add(new WorldScene.Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, piece));
                indices.Add(list.Count - 1);
                changes.Add((list.Count - 1, true, false));
            }
        }
        return (indices, changes, added);
    }

    /// <summary>A thing as extensions see it: its asset's name, its place, the data it holds, its id.</summary>
    internal ObjectView ViewOf(WorldScene.Thing t) =>
        new(AssetIds.NameOf(t.Prefab) ?? "", t.Position, t.Rotation, Edits.FindAdded(t.Id)?.Data, t.Scale) { Id = t.Id };
}
