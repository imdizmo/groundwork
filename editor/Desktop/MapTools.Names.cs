using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// Names: names on places (a town, a bay, a peak) and named regions drawn round on the ground. A click names a
    /// place (or picks the name there, to drag or rename it); in Region, clicks go round the outline and Enter or a click
    /// on its first point closes it. Shift+click deletes. Kept in the map's names file (MapNames).
    /// </summary>
    public sealed class NamesTool(MainWindow w) : Base(w)
    {
        private static readonly string[] Sizes = ["Region", "Place", "Small place"];
        private readonly List<Vector3> _draft = [];
        private string? _before;
        private int _dragging = -1;
        private Action? _refresh;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("names", "Names", "Names on the map: click to name a place (type its name below), drag a name to move it, Shift+click to delete it. In Region, click round an area and press Enter (or click its first point) to make a named region. Names show over the view, the small ones only up close.") { Icon = "M4 20h16M7 16l5-12 5 12M9 12h6" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ChoiceControl("what", "Make", ["A place's name", "A region"]),
            new ChoiceControl("size", "Size", Sizes),
            new ListControl("names", "Names"),
            new TextBoxControl("text", "Name", "the picked name"),
            new ButtonControl("delete", "Delete this one"),
            new TextControl("status", ""),
        ];

        private static bool RegionMode(IToolContext ctx) => ctx.Choice("what") == 1;

        /// <summary>The picked entry of the list: a label (its index), or a region (its index past the labels).</summary>
        private static (MapNames.Label? Label, MapNames.Region? Region) Picked(IToolContext ctx)
        {
            int i = ctx.Choice("names");
            return i >= 0 && i < MapNames.Labels.Count ? (MapNames.Labels[i], null)
                : i >= MapNames.Labels.Count && i < MapNames.Labels.Count + MapNames.Regions.Count ? (null, MapNames.Regions[i - MapNames.Labels.Count]) : (null, null);
        }

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            MapNames.Changed -= _refresh;
            _refresh = () =>
            {
                List(ctx);
                Show(ctx);
            };
            MapNames.Changed += _refresh;
            List(ctx);
            Fields(ctx);
            Show(ctx);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx)
        {
            MapNames.Changed -= _refresh;
            Record("Rename");
            _draft.Clear();
        }

        private static void List(IToolContext ctx, int? pick = null)
        {
            var items = MapNames.Labels.Select(l => (l.Text, $"at {l.X:0}, {l.Z:0}", Sizes[(int)l.Size]))
                .Concat(MapNames.Regions.Select(r => (r.Name, $"{MathF.Abs(MapNames.Area(r)) / 1e4f:0.#} ha, {r.Points.Count} points", "Region outline"))).ToList();
            ctx.SetList("names", items, Math.Clamp(pick ?? ctx.Choice("names"), 0, Math.Max(0, items.Count - 1)));
        }

        /// <summary>The name box and size show the picked one.</summary>
        private void Fields(IToolContext ctx)
        {
            var (l, r) = Picked(ctx);
            ctx.SetText("text", l?.Text ?? r?.Name ?? "");
            if (l != null)
            {
                ctx.SetNumber("size", (int)l.Size);
            }
            ctx.SetText("status", RegionMode(ctx)
                ? _draft.Count == 0 ? "Click round the region; Enter or its first point closes it." : $"{_draft.Count} points: Enter closes it, Backspace takes the last off, Esc clears."
                : MapNames.None ? "Click on the map to name a place." : "Click: a new name · drag a name: move · Shift+click: delete");
        }

        private void Start() => _before ??= MapNames.Snapshot();

        private void Record(string label)
        {
            if (_before is not string before || Session is not { } s)
            {
                _before = null;
                return;
            }
            _before = null;
            string after = MapNames.Snapshot();
            if (after != before)
            {
                _ = s.RecordExtra(label, again => MapNames.Restore(again ? after : before));
            }
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            var (l, r) = Picked(ctx);
            switch (id)
            {
                case "names":
                    Record("Rename");
                    Fields(ctx);
                    break;
                case "what":
                    _draft.Clear();
                    Fields(ctx);
                    break;
                case "text" when l != null || r != null:
                    // a name typed is one step, however many keys it took
                    string text = MapNames.Clip(ctx.Typed("text"));
                    if (text.Length == 0)
                    {
                        return;
                    }
                    Start();
                    if (l != null)
                    {
                        l.Text = text;
                    }
                    else
                    {
                        r!.Name = text;
                    }
                    MapNames.Touch();
                    break;
                case "size" when l != null:
                    Record("Rename");
                    Start();
                    l.Size = (MapNames.Size)Math.Clamp(ctx.Choice("size"), 0, 2);
                    MapNames.Touch();
                    Record("Name size");
                    break;
                case "delete" when l != null || r != null:
                    Record("Rename");
                    Start();
                    _ = l != null ? MapNames.Labels.Remove(l) : MapNames.Regions.Remove(r!);
                    MapNames.Touch();
                    Record("Delete name");
                    Fields(ctx);
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        /// <inheritdoc/>
        public override bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
        {
            if (!RegionMode(ctx) || _draft.Count == 0)
            {
                return false;
            }
            switch (key)
            {
                case "Enter" or "Return":
                    Close(ctx);
                    break;
                case "Escape":
                    _draft.Clear();
                    break;
                case "Back":
                    _draft.RemoveAt(_draft.Count - 1);
                    break;
                default:
                    return false;
            }
            Fields(ctx);
            Show(ctx);
            return true;
        }

        /// <summary>The label whose place is nearest a point, within a reach that grows with the view's distance; -1: none.</summary>
        private static int LabelNear(Vector3 p, float reach)
        {
            int best = -1;
            float bestD = reach * reach;
            for (int i = 0; i < MapNames.Labels.Count; i++)
            {
                float d = Vector2.DistanceSquared(new Vector2(MapNames.Labels[i].X, MapNames.Labels[i].Z), new Vector2(p.X, p.Z));
                if (d < bestD)
                {
                    (best, bestD) = (i, d);
                }
            }
            return best;
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 p)
            {
                return;
            }
            float reach = MathF.Max(6, Vector3.Distance(input.Origin, p) * 0.03f);
            var xz = new Vector2(p.X, p.Z);
            switch (input.Action)
            {
                case PointerAction.Press when input.Modifiers.HasFlag(KeyModifiers.Shift):
                    Record("Rename");
                    Start();
                    if (LabelNear(p, reach) is int k and >= 0)
                    {
                        MapNames.Labels.RemoveAt(k);
                    }
                    else if (MapNames.At(xz).FirstOrDefault() is { } inside && _draft.Count == 0)
                    {
                        _ = MapNames.Regions.Remove(inside);
                    }
                    MapNames.Touch();
                    Record("Delete name");
                    break;
                case PointerAction.Press when RegionMode(ctx):
                    if (_draft.Count >= 3 && Vector2.Distance(new Vector2(_draft[0].X, _draft[0].Z), xz) < reach)
                    {
                        Close(ctx);
                    }
                    else
                    {
                        _draft.Add(p);
                    }
                    break;
                case PointerAction.Press when LabelNear(p, reach) is int near and >= 0:
                    Record("Rename");
                    _dragging = near;
                    List(ctx, near);
                    Fields(ctx);
                    break;
                case PointerAction.Press:
                    Record("Rename");
                    Start();
                    var size = (MapNames.Size)Math.Clamp(ctx.Choice("size"), 0, 2);
                    MapNames.Labels.Add(new MapNames.Label { Text = MapNames.NewName(size == MapNames.Size.Region ? "Land" : "Place"), X = p.X, Z = p.Z, Size = size });
                    MapNames.Touch();
                    Record("New name");
                    List(ctx, MapNames.Labels.Count - 1);
                    Fields(ctx);
                    break;
                case PointerAction.Drag when _dragging >= 0 && _dragging < MapNames.Labels.Count:
                    Start();
                    (MapNames.Labels[_dragging].X, MapNames.Labels[_dragging].Z) = (p.X, p.Z);
                    MapNames.Touch();
                    break;
                case PointerAction.Release when _dragging >= 0:
                    _dragging = -1;
                    Record("Move name");
                    break;
                default:
                    break;
            }
            Fields(ctx);
            Show(ctx);
        }

        /// <summary>The outline drawn becomes a region (three points or more), named and picked.</summary>
        private void Close(IToolContext ctx)
        {
            if (_draft.Count < 3)
            {
                return;
            }
            Record("Rename");
            Start();
            var region = new MapNames.Region { Name = MapNames.NewName("Region") };
            region.Points.AddRange(_draft.Select(q => new Vector2(q.X, q.Z)));
            MapNames.Regions.Add(region);
            _draft.Clear();
            MapNames.Touch();
            Record("New region: " + region.Name);
            List(ctx, MapNames.Labels.Count + MapNames.Regions.Count - 1);
            Fields(ctx);
        }

        /// <summary>The outline being drawn and the picked name's place.</summary>
        private void Show(IToolContext ctx)
        {
            var shapes = new List<Shape>();
            if (_draft.Count > 0)
            {
                var lifted = _draft.Select(q => q with { Y = q.Y + 0.6f }).ToList();
                shapes.Add(Band(lifted, 1.2f, new Vector4(1, 0.85f, 0.4f, 0.9f)));
                shapes.Add(Dots(lifted, 1.5f, new Vector4(1, 0.95f, 0.7f, 1)));
            }
            var (l, r) = Picked(ctx);
            if (l != null)
            {
                shapes.Add(Ring(ctx.Area, new Vector3(l.X, ctx.Area.GroundHeight(l.X, l.Z), l.Z), 4, new Vector4(1, 0.85f, 0.4f, 0.9f)));
            }
            else if (r != null)
            {
                var ring = r.Points.Select(q => new Vector3(q.X, ctx.Area.GroundHeight(q.X, q.Y) + 0.8f, q.Y)).ToList();
                shapes.Add(Band(ring, 2.5f, new Vector4(1, 0.85f, 0.4f, 0.6f), closed: true));
            }
            ctx.Preview(shapes);
        }
    }
}
