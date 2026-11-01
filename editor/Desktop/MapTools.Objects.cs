using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// Objects (a right-hand panel): every kind standing on the map with how many, found by what is typed; the kind
    /// picked can be selected whole, gone to, locked (drawn, not picked) or hidden.
    /// </summary>
    public sealed class ObjectsTool(MainWindow w) : Base(w)
    {
        private List<(int Prefab, string Model, string Label, string Group, int Count)> _rows = [];

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("objects", "Objects", "Every kind of object on the map and how many; type to find one. Pick a kind, then select them all, go to the nearest, lock them (they stay drawn but cannot be picked) or hide them.");

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new TextBoxControl("search", "Find", "name or group"),
            new ListControl("kind", "On the map"),
            new ButtonControl("select", "Select them all"),
            new ButtonControl("goto", "Go to the nearest"),
            new ButtonControl("lock", "Lock or unlock them"),
            new ButtonControl("hide", "Hide or show them"),
            new TextControl("status", ""),
        ];

        /// <summary>The kinds standing on the map (not gone) whose name, label or group holds what is typed, by group then label.</summary>
        internal static List<(int Prefab, string Model, string Label, string Group, int Count)> KindsOnMap(WorldScene s, string search)
        {
            var counts = new Dictionary<int, int>();
            lock (s.Things)
            {
                foreach (var t in s.Things.Where(t => !t.Gone))
                {
                    counts[t.Prefab] = counts.GetValueOrDefault(t.Prefab) + 1;
                }
            }
            return [.. counts.Select(kv =>
                {
                    string model = AssetIds.NameOf(kv.Key) ?? ModelStore.ModelName(kv.Key) ?? "?";
                    var (group, label) = GroupOf(kv.Key);
                    return (kv.Key, model, label.Length > 0 ? label : model, group, kv.Value);
                })
                .Where(r => search.Length == 0 || $"{r.model} {r.Item3} {r.group}".Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.group, StringComparer.Ordinal).ThenBy(r => r.Item3, StringComparer.Ordinal)];
        }

        /// <summary>The standing objects of a kind (indices).</summary>
        internal static List<int> ThingsOf(WorldScene s, int prefab)
        {
            lock (s.Things)
            {
                return [.. Enumerable.Range(0, s.Things.Count).Where(i => !s.Things[i].Gone && s.Things[i].Prefab == prefab)];
            }
        }

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => Refresh(ctx);

        private void Refresh(IToolContext ctx)
        {
            if (Session is not { } s)
            {
                return;
            }
            _rows = KindsOnMap(s.Scene, ctx.Typed("search"));
            var view = W.View;
            ctx.SetList("kind", [.. _rows.Select(r =>
            {
                var things = ThingsOf(s.Scene, r.Prefab);
                string tag = things.Count > 0 && things.All(view.IsLocked) ? "locked" : things.Count > 0 && things.All(view.IsHidden) ? "hidden" : "";
                return (r.Label, $"{r.Group} · {r.Count:N0}", tag);
            })], Math.Clamp(ctx.Choice("kind"), 0, Math.Max(0, _rows.Count - 1)));
            ctx.SetText("status", _rows.Count == 0 ? "Nothing on the map is called so." : $"{_rows.Count} kinds, {_rows.Sum(r => r.Count):N0} objects.");
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            int k = ctx.Choice("kind");
            if (id == "search" || Session is not { } s || k < 0 || k >= _rows.Count)
            {
                Refresh(ctx);
                return;
            }
            var row = _rows[k];
            var things = ThingsOf(s.Scene, row.Prefab);
            var view = W.View;
            switch (id)
            {
                case "select":
                    view.Select(things.Where(i => !view.IsLocked(i) && !view.IsHidden(i)));
                    ctx.Status($"Selected {things.Count:N0} {row.Label} (Select, E, moves them).");
                    break;
                case "goto":
                    var (_, _, _, target) = view.Camera;
                    var at = new System.Numerics.Vector3(target.X + s.Scene.Cx, target.Y, -target.Z + s.Scene.Cz);
                    if (things.Count > 0)
                    {
                        int nearest = things.MinBy(i => System.Numerics.Vector3.Distance(s.Scene.Things[i].Position, at));
                        view.Unhide([nearest]);
                        view.Select([nearest]);
                        _ = view.FrameSelection();
                    }
                    break;
                case "lock":
                    bool lockThem = !things.All(view.IsLocked);
                    view.Lock(things, lockThem);
                    ctx.Status(lockThem ? $"{row.Label} locked: drawn, but not picked." : $"{row.Label} unlocked.");
                    break;
                case "hide":
                    if (things.All(view.IsHidden))
                    {
                        view.Unhide(things);
                    }
                    else
                    {
                        view.Hide(things);
                    }
                    break;
                default:
                    break;
            }
            Refresh(ctx);
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
        }
    }
}
