using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>The kinds of area the catalog names ("areas"), else the built-in ones.</summary>
    internal static IReadOnlyList<MapAreas.Kind> AreaKinds()
    {
        string file = CatalogFile;
        return MapAreas.Kinds(File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null);
    }

    /// <summary>
    /// Areas: boxes on the ground that become volumes in Unreal (triggers, blocking walls, sound, kill zones...). Drag
    /// on the ground draws one; click one to pick it, drag it to move it, Shift+click deletes it.
    /// </summary>
    // ponytail: the areas are drawn while this tool is chosen only; with the water when they should always show
    public sealed class AreasTool(MainWindow w, IReadOnlyList<MapAreas.Kind> kinds) : Base(w)
    {
        private string? _before;
        private Vector2? _from, _now, _grab;
        private Vector2 _grabbedAt;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("areas", "Areas", "Drag on the ground to draw an area of the kind picked: a box that becomes a volume in Unreal (a trigger, a blocking wall, a sound or post-process zone, a kill zone). Click one to pick it and change it below, drag it to move it, Shift+click to delete it. Every change is a step in History.") { Icon = "M4 7l8-4 8 4v10l-8 4-8-4zM4 7l8 4 8-4M12 11v10" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ChoiceControl("kind", "Kind", [.. kinds.Select(k => k.Label)]),
            new ListControl("area", "Areas"),
            new ButtonControl("delete", "Delete this one"),
            new SliderControl("width", "Width", 0.5f, 500, 10) { Step = 0.5f, Unit = "m" },
            new SliderControl("depth", "Depth", 0.5f, 500, 10) { Step = 0.5f, Unit = "m" },
            new SliderControl("height", "Height", 0.5f, 200, 5) { Step = 0.5f, Unit = "m" },
            new SliderControl("bottom", "Bottom", -200, 800, 0) { Step = 0.5f, Unit = "m", Help = "The box's bottom height; a new one starts a metre under the ground." },
            new SliderControl("yaw", "Turn", -180, 180, 0) { Step = 1, Unit = "°" },
            new TextControl("status", ""),
        ];

        private Action? _refresh;

        private static MapAreas.Area? Picked(IToolContext ctx) => ctx.Choice("area") is int i && i >= 0 && i < MapAreas.All.Count ? MapAreas.All[i] : null;

        private MapAreas.Kind KindOf(MapAreas.Area a) => kinds.FirstOrDefault(k => k.Id == a.Kind) ?? kinds[0];

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            MapAreas.Changed -= _refresh;
            _refresh = () =>
            {
                List(ctx);
                Show(ctx);
            };
            MapAreas.Changed += _refresh;
            List(ctx);
            Fields(ctx);
            Show(ctx);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx) => MapAreas.Changed -= _refresh;

        private void List(IToolContext ctx, int? pick = null) => ctx.SetList("area", [.. MapAreas.All.Select(a => (a.Name, $"{a.Width:0.#} x {a.Depth:0.#} x {a.Top - a.Bottom:0.#} m", KindOf(a).Label))],
            Math.Clamp(pick ?? ctx.Choice("area"), 0, Math.Max(0, MapAreas.All.Count - 1)));

        /// <summary>The sliders show the picked area's values.</summary>
        private static void Fields(IToolContext ctx)
        {
            if (Picked(ctx) is { } a)
            {
                ctx.SetNumber("width", a.Width);
                ctx.SetNumber("depth", a.Depth);
                ctx.SetNumber("height", a.Top - a.Bottom);
                ctx.SetNumber("bottom", a.Bottom);
                ctx.SetNumber("yaw", a.Yaw);
            }
            ctx.SetText("status", MapAreas.All.Count == 0 ? "No areas yet: drag on the ground to draw one." : Picked(ctx) is { } p ? $"{p.Name.ToUpperInvariant()}: the sliders change it." : "");
        }

        /// <summary>The areas as they are before an edit.</summary>
        private void Start() => _before ??= MapAreas.Snapshot();

        /// <summary>The edit since Start as a step in the history.</summary>
        private void Record(string label)
        {
            if (_before is not string before || Session is not { } s)
            {
                return;
            }
            _before = null;
            string after = MapAreas.Snapshot();
            if (after != before)
            {
                _ = s.RecordExtra(label, again => MapAreas.Restore(again ? after : before));
            }
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id == "area")
            {
                Fields(ctx);
                Show(ctx);
                return;
            }
            if (Picked(ctx) is not { } a)
            {
                return;
            }
            Start();
            switch (id)
            {
                case "delete":
                    _ = MapAreas.All.Remove(a);
                    break;
                case "kind":
                    a.Kind = kinds[Math.Clamp(ctx.Choice("kind"), 0, kinds.Count - 1)].Id;
                    break;
                default:
                    (a.Width, a.Depth, a.Bottom, a.Yaw) = (ctx.Number("width"), ctx.Number("depth"), ctx.Number("bottom"), ctx.Number("yaw"));
                    a.Top = a.Bottom + ctx.Number("height");
                    break;
            }
            MapAreas.Touch();
            Record(id == "delete" ? "Delete area" : "Area " + id);
            Fields(ctx);
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 p)
            {
                return;
            }
            var u = new Vector2(p.X, -p.Z);   // Unreal metres
            int under = MapAreas.All.FindLastIndex(a => MapAreas.Holds(a, u));
            switch (input.Action)
            {
                case PointerAction.Press when under >= 0 && input.Modifiers.HasFlag(KeyModifiers.Shift):
                    Start();
                    MapAreas.All.RemoveAt(under);
                    MapAreas.Touch();
                    Record("Delete area");
                    break;
                case PointerAction.Press when under >= 0:
                    List(ctx, under);
                    Fields(ctx);
                    (_grab, _grabbedAt) = (u, new Vector2(MapAreas.All[under].X, MapAreas.All[under].Y));
                    break;
                case PointerAction.Press:
                    (_from, _now) = (u, u);
                    break;
                case PointerAction.Drag when _grab is Vector2 g && Picked(ctx) is { } a:
                    Start();
                    (a.X, a.Y) = (_grabbedAt.X + u.X - g.X, _grabbedAt.Y + u.Y - g.Y);
                    MapAreas.Touch();
                    break;
                case PointerAction.Drag when _from != null:
                    _now = u;
                    break;
                case PointerAction.Release when _grab != null:
                    _grab = null;
                    Record("Move area");
                    break;
                case PointerAction.Release when _from is Vector2 f:
                    Draw(ctx, f, u);
                    (_from, _now) = (null, null);
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        /// <summary>A new area over the box dragged (a metre or more each way), its bottom a metre under its lowest ground.</summary>
        private void Draw(IToolContext ctx, Vector2 a, Vector2 b)
        {
            var size = Vector2.Abs(b - a);
            if (size.X < 1 || size.Y < 1)
            {
                return;
            }
            var kind = kinds[Math.Clamp(ctx.Choice("kind"), 0, kinds.Count - 1)];
            var area = new MapAreas.Area { Name = MapAreas.NewName(kind.Label), Kind = kind.Id, X = (a.X + b.X) / 2, Y = (a.Y + b.Y) / 2, Width = size.X, Depth = size.Y };
            float low = MapAreas.Corners(area).Append(new Vector2(area.X, area.Y)).Min(q => ctx.Area.GroundHeight(q.X, -q.Y));
            (area.Bottom, area.Top) = (MathF.Round(low - 1), MathF.Round(low - 1) + ctx.Number("height"));
            Start();
            MapAreas.All.Add(area);
            MapAreas.Touch();
            Record("New area: " + area.Name);
            List(ctx, MapAreas.All.Count - 1);
            Fields(ctx);
        }

        /// <summary>Every area as a see-through box in its kind's colour (the picked one stronger), and the box being dragged.</summary>
        private void Show(IToolContext ctx)
        {
            var shapes = new List<Shape>();
            var picked = Picked(ctx);
            foreach (var a in MapAreas.All)
            {
                var c = KindOf(a).Colour;
                shapes.Add(Box(a, new Vector4(c, a == picked ? 0.45f : 0.22f)));
            }
            if (_from is Vector2 f && _now is Vector2 n)
            {
                float y = ctx.Area.GroundHeight((f.X + n.X) / 2, -(f.Y + n.Y) / 2);
                shapes.Add(Box(new MapAreas.Area { X = (f.X + n.X) / 2, Y = (f.Y + n.Y) / 2, Width = MathF.Abs(n.X - f.X), Depth = MathF.Abs(n.Y - f.Y), Bottom = y - 1, Top = y - 1 + ctx.Number("height") }, new Vector4(1, 1, 1, 0.3f)));
            }
            ctx.Preview(shapes);
        }

        /// <summary>An area's box in the editor's frame (x = X, z = -Y).</summary>
        private static Shape Box(MapAreas.Area a, Vector4 colour)
        {
            var c = MapAreas.Corners(a);
            Vector3[] v = [.. c.Select(q => new Vector3(q.X, a.Bottom, -q.Y)), .. c.Select(q => new Vector3(q.X, a.Top, -q.Y))];
            int[] t = [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0];
            return new Shape(v, t, colour) { BothSides = true, WorldSpace = true };
        }
    }
}
