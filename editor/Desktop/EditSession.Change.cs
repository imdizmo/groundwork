using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// A change that can be undone: the ground points it changed, its things, and what it says of itself.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A change that can be undone: the ground points it changed, before and after, and the things it took away or
    /// brought.
    /// </summary>
    /// <param name="Label">The name shown in the history and on Undo.</param>
    /// <param name="Points">The ground grid points it changed (indices, sorted).</param>
    /// <param name="Before">Those points' values before it, in the same order.</param>
    /// <param name="After">Those points' values after it.</param>
    public sealed record Change(string Label, int[] Points, Ground.State Before, Ground.State After)
    {
        /// <summary>
        /// The things it took away or brought: index into the scene's things, gone before and after.
        /// </summary>
        public (int Index, bool Before, bool After)[] Things { get; init; } = [];
        /// <summary>
        /// When it was made (local time).
        /// </summary>
        public DateTime Time { get; init; } = DateTime.Now;
        /// <summary>
        /// Taken out (History panel: Remove).
        /// </summary>
        public bool Removed { get; set; }
        /// <summary>
        /// For a removal, the change it takes out; null otherwise.
        /// </summary>
        public Change? RevertOf { get; init; }

        /// <summary>
        /// Something else the step changed (the map's rivers): called with true to do it again,
        /// false to take it back.
        /// </summary>
        public Action<bool>? Extra { get; init; }

        /// <summary>
        /// About how much memory it holds: 56 bytes a ground point (its index, and five values before and after), a
        /// few for each thing (<see cref="KeepBytes"/>).
        /// </summary>
        public long Bytes => 56L * Points.Length + 8L * Things.Length;

        /// <summary>
        /// Whether it is this very change: a change is itself, not its values, since Removed is set after it is made.
        /// </summary>
        public bool Equals(Change? other) => ReferenceEquals(this, other);

        /// <summary>The change's own hash, which setting Removed does not move.</summary>
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        /// <summary>
        /// A short line of what it did, like the web editor's history: "120 points · 2 new · 1 removed".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (Points.Length > 0)
            {
                parts.Add(Lang.T("history.change.points", Points.Length));
            }
            int added = Things.Count(t => t.Before && !t.After);
            int removed = Things.Count(t => !t.Before && t.After);
            if (added > 0)
            {
                parts.Add(Lang.T("history.change.new", added));
            }
            if (removed > 0)
            {
                parts.Add(Lang.T("history.change.removed", removed));
            }
            return string.Join(" · ", parts);
        }
    }
}
