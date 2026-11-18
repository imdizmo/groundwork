
namespace Groundwork.Desktop;

/// <summary>
/// One undo step that changes the ground and the things together.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// One undo step that may change the ground, take things away and add new objects. Nothing goes on the history when
    /// it changes nothing.
    /// </summary>
    /// <param name="label">The history's name for the step.</param>
    /// <param name="ground">As EditGround's change; null leaves the ground alone.</param>
    /// <param name="remove">Things to take away, indices into the scene's things (bad or already gone ones are
    /// skipped).</param>
    /// <param name="add">New objects (their ids are given here; Piece: a building part; read after the ground step,
    /// which may fill it).</param>
    /// <param name="extra">Something of the step's own, already done: extra(true) does it again, extra(false) takes it
    /// back (a kept road's line); the step is kept with it even when the ground and things did not change.</param>
    /// <returns>The indices of the things added.</returns>
    public List<int> Commit(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground,
        IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add, Action<bool>? extra = null)
    {
        var lifted = new List<(int Point, float Before, float After)>();
        var (points, before, after) = ChangeGround(WithBeds(ground, remove, add, lifted));
        var (indices, changes, added) = ChangeThings(remove, add);
        if (added.Count > 0)
        {
            Edits.AddObjects(added);
        }
        SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
        if (points.Length == 0 && changes.Count == 0 && extra == null)
        {
            return indices;
        }
        AddChange(new Change(label, points, before, after) { Things = [.. changes], Lifted = LiftedOf(lifted), Extra = extra });
        if (changes.Count > 0)
        {
            Notify(changes.Select(c => c.Item1));
        }
        if (indices.Count > 0)
        {
            ThingsAdded?.Invoke(indices);
        }
        Edited();
        return indices;
    }
}
