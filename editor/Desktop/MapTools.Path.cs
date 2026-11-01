using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// Path: click points along a path, road or ramp, then Make it: the ground levelled along it (Paths), a layer
    /// painted on it, objects along its edges, as one step. The line is not kept once made.
    /// </summary>
    // ponytail: a made path is not kept or editable again (undo and draw it anew); keep paths in a file when that matters
    public sealed class PathTool(MainWindow w, List<Kind> kinds) : Base(w)
    {
        private readonly List<Vector2> _points = [];
        private int _drag = -1;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("path", "Path", "Click points along a path, road or ramp (drag one to move it, Shift+click to delete it), then Make it (Enter): the ground under it levelled, its shoulders blended into the ground round it, a layer painted on it, objects along its edges, in one step. Even slope with two points makes a ramp. Esc clears the line.") { Icon = "M8 21L11 3M16 21L13 3M12 6v2M12 11v2M12 16v2" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new SliderControl("width", "Width", 1, 20, 3) { Step = 0.5f, Unit = "m" },
            new SliderControl("shoulder", "Shoulder", 0, 20, 3) { Step = 0.5f, Unit = "m" },
            new ChoiceControl("slope", "Slope", ["Follow the ground, smoothed", "Even between the points (ramp)"]),
            new SliderControl("smoothing", "Smoothing", 2, 60, 15) { Step = 1, Unit = "m" },
            new ChoiceControl("layer", "Paint", ["Nothing", .. MapProject.Current?.Layers.Select(l => l.Name) ?? []]),
            new SectionControl("secEdge", "Along the edges") { Folded = true },
            new ChoiceControl("edge", "Objects", ["Nothing", .. kinds.Select(k => k.Label.Length > 0 ? k.Label : k.Model)]),
            new SliderControl("spacing", "Every", 1, 30, 3) { Step = 0.5f, Unit = "m" },
            new SliderControl("offset", "Past the edge", 0, 5, 0.5f) { Step = 0.1f, Unit = "m" },
            new ToggleControl("both", "Both sides", true),
            new ButtonControl("make", "Make it"),
            new ButtonControl("clear", "Clear the line"),
            new TextControl("status", "Click the ground to start a line."),
        ];

        /// <summary>The path as the card says.</summary>
        private Paths.Spec Spec(IToolContext ctx) => new(ctx.Number("width"), ctx.Number("shoulder"), ctx.Choice("slope") == 1, ctx.Number("smoothing"), ctx.Choice("layer") - 1,
            ctx.Choice("edge") is int e and > 0 && e <= kinds.Count ? kinds[e - 1].Model : null, ctx.Number("spacing"), ctx.Number("offset"), ctx.IsOn("both"));

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => Show(ctx);

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            switch (id)
            {
                case "make":
                    Make(ctx);
                    break;
                case "clear":
                    _points.Clear();
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        /// <inheritdoc/>
        public override bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
        {
            switch (key)
            {
                case "Enter" or "Return":
                    Make(ctx);
                    break;
                case "Escape" when _points.Count > 0:
                    _points.Clear();
                    break;
                case "Back" when _points.Count > 0:
                    _points.RemoveAt(_points.Count - 1);
                    break;
                default:
                    return false;
            }
            Show(ctx);
            return true;
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 p)
            {
                return;
            }
            var at = new Vector2(p.X, p.Z);
            int near = _points.FindIndex(q => Vector2.Distance(q, at) < MathF.Max(1.5f, ctx.Number("width") / 2));
            switch (input.Action)
            {
                case PointerAction.Press when near >= 0 && input.Modifiers.HasFlag(KeyModifiers.Shift):
                    _points.RemoveAt(near);
                    break;
                case PointerAction.Press when near >= 0:
                    _drag = near;
                    break;
                case PointerAction.Press:
                    _points.Add(at);
                    _drag = _points.Count - 1;
                    break;
                case PointerAction.Drag when _drag >= 0 && _drag < _points.Count:
                    _points[_drag] = at;
                    break;
                case PointerAction.Release:
                    _drag = -1;
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        private void Make(IToolContext ctx)
        {
            if (Session is not { } s || _points.Count < 2)
            {
                ctx.Status("A path needs two points or more.");
                return;
            }
            var spec = Spec(ctx);
            var adds = new List<(NewObject Object, bool Piece)>();
            string label = spec.Even && _points.Count == 2 ? "Ramp" : "Path";
            int n = s.Commit(label, g => Paths.Apply(g, [.. _points], spec, adds), [], adds).Count;
            ctx.Status($"{label} made" + (n > 0 ? $", {n} objects along it." : "."));
            _points.Clear();
        }

        /// <summary>The line drawn as a band the path's width on the ground, its points as dots.</summary>
        private void Show(IToolContext ctx)
        {
            var area = ctx.Area;
            var shapes = new List<Shape>();
            if (_points.Count > 0)
            {
                shapes.Add(Dots(_points.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.2f, q.Y)), 0.6f, new Vector4(1, 0.85f, 0.2f, 1)));
            }
            if (_points.Count > 1)
            {
                var (line, _) = Paths.Profile(_points, area.GroundHeight, true, 0);
                var band = line.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.15f, q.Y)).ToList();
                shapes.Add(Band(band, ctx.Number("width"), new Vector4(1, 0.7f, 0.2f, 0.45f)));
                shapes.Add(Band(band, ctx.Number("width") + (2 * ctx.Number("shoulder")), new Vector4(1, 0.7f, 0.2f, 0.15f)));
            }
            ctx.Preview(shapes);
            ctx.SetText("status", _points.Count switch
            {
                0 => "Click the ground to start a line.",
                1 => "Click the next point.",
                _ => $"{_points.Count} points, {_points.Zip(_points.Skip(1), Vector2.Distance).Sum():0} m: Make it (Enter), or add more.",
            });
        }
    }
}
