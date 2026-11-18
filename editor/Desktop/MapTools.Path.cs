using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// A painted stroke as the few points that keep its shape (Douglas-Peucker, within <paramref name="within"/> metres),
    /// with a point at least every <paramref name="most"/> metres so a long straight stretch can be nudged too.
    /// </summary>
    internal static List<Vector2> StrokeDots(IReadOnlyList<Vector2> stroke, float within, float most)
    {
        if (stroke.Count < 3)
        {
            return [.. stroke];
        }
        bool[] keep = new bool[stroke.Count];
        keep[0] = keep[^1] = true;
        var todo = new Stack<(int A, int B)>();
        todo.Push((0, stroke.Count - 1));
        while (todo.Count > 0)
        {
            var (a, b) = todo.Pop();
            int far = -1;
            float worst = within;
            for (int i = a + 1; i < b; i++)
            {
                float d = MapRoads.Distance(stroke[i], stroke[a], stroke[b]);
                if (d > worst)
                {
                    (worst, far) = (d, i);
                }
            }
            if (far >= 0)
            {
                keep[far] = true;
                todo.Push((a, far));
                todo.Push((far, b));
            }
        }
        var dots = new List<Vector2>();
        for (int i = 0; i < stroke.Count; i++)
        {
            if (!keep[i])
            {
                continue;
            }
            if (dots.Count > 0 && Vector2.Distance(dots[^1], stroke[i]) is float gap && gap > most)
            {
                var from = dots[^1];
                int n = (int)MathF.Ceiling(gap / most);
                for (int k = 1; k < n; k++)
                {
                    dots.Add(Vector2.Lerp(from, stroke[i], k / (float)n));
                }
            }
            dots.Add(stroke[i]);
        }
        return dots;
    }

    /// <summary>
    /// Path: click points along a path, road or ramp, then Make it: the ground levelled along it (Paths), a layer
    /// painted on it, objects along its edges, a bridge where it crosses a river, as one step. Kept as a road
    /// (MapRoads), it can be picked again, changed and made again (its old ground put back first), or taken away.
    /// </summary>
    public sealed class PathTool(MainWindow w, List<Kind> kinds) : Base(w)
    {
        private readonly List<Vector2> _points = [];
        private int _drag = -1;
        private bool _moved;
        private List<Vector2>? _stroke;
        private Vector2? _hover;
        private MapRoads.Road? _road;
        private Action? _refresh;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("path", "Path", "Paint a path, road or ramp: drag along the ground (or click point by point); it is made when you let go. Dots stay along it: drag one to nudge it, click the line to add one, Shift+click one to delete it, and it is made again. Making it levels the ground under it, blends its shoulders into the ground round it, paints a layer on it and puts objects along its edges and a bridge over each river it crosses, in one step. After a slider change, Make it (Enter). Pick a kept road in the list to change it, or take it away; Esc lets go of it, and painting away from its ends starts a new one.") { Icon = "M8 21L11 3M16 21L13 3M12 6v2M12 11v2M12 16v2" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ListControl("roads", "Kept roads"),
            new SliderControl("width", "Width", 1, 20, 3) { Step = 0.5f, Unit = "m" },
            new SliderControl("shoulder", "Shoulder", 0, 20, 3) { Step = 0.5f, Unit = "m" },
            new ChoiceControl("slope", "Slope", ["Follow the ground, smoothed", "Even between the points (ramp)"]),
            new SliderControl("smoothing", "Smoothing", 2, 60, 15) { Step = 1, Unit = "m" },
            new ChoiceControl("layer", "Paint", ["Nothing", .. MapProject.Current?.Layers.Select(l => l.Name) ?? []]),
            new ChoiceControl("bridge", "Bridges", ["None", .. kinds.Select(k => k.Label.Length > 0 ? k.Label : k.Model)]),
            new ToggleControl("keep", "Keep it as a road (change it later)", true),
            new ButtonControl("make", "Make it"),
            new ButtonControl("clear", "Clear the line"),
            new ButtonControl("delete", "Take this road away"),
            new TextControl("status", "Drag along the ground to paint a line, or click point by point."),
            // what follows a section is folded with it: the buttons stay above
            new SectionControl("secEdge", "Along the edges") { Folded = true },
            new ChoiceControl("edge", "Objects", ["Nothing", .. kinds.Select(k => k.Label.Length > 0 ? k.Label : k.Model)]),
            new SliderControl("spacing", "Every", 1, 30, 3) { Step = 0.5f, Unit = "m" },
            new SliderControl("offset", "Past the edge", 0, 5, 0.5f) { Step = 0.1f, Unit = "m" },
            new ToggleControl("both", "Both sides", true),
        ];

        /// <summary>The path as the card says.</summary>
        private Paths.Spec Spec(IToolContext ctx) => new(ctx.Number("width"), ctx.Number("shoulder"), ctx.Choice("slope") == 1, ctx.Number("smoothing"), ctx.Choice("layer") - 1,
            Model(ctx, "edge"), ctx.Number("spacing"), ctx.Number("offset"), ctx.IsOn("both"));

        /// <summary>The model a kind choice names, or null for its first entry (none).</summary>
        private string? Model(IToolContext ctx, string id) => ctx.Choice(id) is int e and > 0 && e <= kinds.Count ? kinds[e - 1].Model : null;

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            MapRoads.Changed -= _refresh;
            _refresh = () =>
            {
                if (_road != null && !MapRoads.All.Contains(_road))
                {
                    _road = MapRoads.All.FirstOrDefault(r => r.Name == _road.Name);
                    // undone or redone: the dots are the road's as it is now
                    _points.Clear();
                    _points.AddRange(_road?.Points ?? []);
                }
                List(ctx);
                Show(ctx);
            };
            MapRoads.Changed += _refresh;
            List(ctx);
            Show(ctx);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx) => MapRoads.Changed -= _refresh;

        private void List(IToolContext ctx) => ctx.SetList("roads", [("New road", "paint one on the ground", ""), .. MapRoads.All.Select(r => (r.Name, $"{r.Length:0} m, {r.Width:0.#} m wide", r.Bridge != null ? "bridges" : ""))],
            _road is { } r ? MapRoads.All.IndexOf(r) + 1 : 0);

        /// <summary>A kept road picked: its line and its settings on the card.</summary>
        private void Pick(IToolContext ctx, MapRoads.Road? road)
        {
            _road = road;
            _points.Clear();
            if (road == null)
            {
                return;
            }
            _points.AddRange(road.Points);
            ctx.SetNumber("width", road.Width);
            ctx.SetNumber("shoulder", road.Shoulder);
            ctx.SetNumber("slope", road.Even ? 1 : 0);
            ctx.SetNumber("smoothing", road.Smoothing);
            ctx.SetNumber("layer", (MapProject.Current?.Layers.FindIndex(l => l.Name == road.Layer) ?? -1) + 1);
            ctx.SetNumber("edge", kinds.FindIndex(k => k.Model == road.Edge) + 1);
            ctx.SetNumber("bridge", kinds.FindIndex(k => k.Model == road.Bridge) + 1);
            ctx.SetNumber("spacing", road.Spacing);
            ctx.SetNumber("offset", road.Offset);
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            switch (id)
            {
                case "roads":
                    int i = ctx.Choice("roads");
                    Pick(ctx, i >= 1 && i <= MapRoads.All.Count ? MapRoads.All[i - 1] : null);
                    break;
                case "make":
                    Make(ctx);
                    break;
                case "clear":
                    _points.Clear();
                    _road = null;
                    List(ctx);
                    break;
                case "delete" when _road != null:
                    TakeAway(ctx, _road);
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
                    _road = null;
                    List(ctx);
                    break;
                case "Back" when _points.Count > 0:
                    _points.RemoveAt(_points.Count - 1);
                    if (_road != null)
                    {
                        Remake(ctx);
                    }
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
            _hover = at;
            float width = ctx.Number("width");
            int near = _points.FindIndex(q => Vector2.Distance(q, at) < MathF.Max(1.5f, width / 2));
            switch (input.Action)
            {
                case PointerAction.Press when near >= 0 && input.Modifiers.HasFlag(KeyModifiers.Shift):
                    _points.RemoveAt(near);
                    Remake(ctx);
                    break;
                case PointerAction.Press when near >= 0:
                    (_drag, _moved) = (near, false);
                    break;
                case PointerAction.Press when OnLine(at, MathF.Max(2, width / 2)) is int seg:
                    // on the line between two dots: a dot there, to nudge
                    _points.Insert(seg + 1, at);
                    (_drag, _moved) = (seg + 1, true);
                    break;
                case PointerAction.Press:
                    _stroke = [at];
                    break;
                case PointerAction.Drag when _drag >= 0 && _drag < _points.Count:
                    _points[_drag] = at;
                    _moved = true;
                    break;
                case PointerAction.Drag when _stroke != null && Vector2.Distance(_stroke[^1], at) > 1:
                    _stroke.Add(at);
                    break;
                case PointerAction.Release when _stroke != null:
                    // a stroke away from the picked road's ends starts a new road
                    if (_road != null && _points.Count > 0 && Vector2.Distance(_stroke[0], _points[0]) > width + 5 && Vector2.Distance(_stroke[0], _points[^1]) > width + 5)
                    {
                        Pick(ctx, null);
                        List(ctx);
                    }
                    Join(_stroke.Zip(_stroke.Skip(1), Vector2.Distance).Sum() < 2 ? [at] : StrokeDots(_stroke, MathF.Max(0.5f, width / 4), MathF.Max(10, width * 4)));
                    _stroke = null;
                    Remake(ctx);
                    break;
                case PointerAction.Release:
                    if (_drag >= 0 && _moved)
                    {
                        Remake(ctx);
                    }
                    _drag = -1;
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        /// <summary>Made on letting go: the line as it is now, when it has two points or more (a kept road is made again).</summary>
        private void Remake(IToolContext ctx)
        {
            if (_points.Count >= 2)
            {
                Make(ctx);
            }
        }

        /// <summary>The stretch of the line a point is on (within so many metres), or null.</summary>
        private int? OnLine(Vector2 at, float within)
        {
            for (int i = 0; i + 1 < _points.Count; i++)
            {
                if (MapRoads.Distance(at, _points[i], _points[i + 1]) < within)
                {
                    return i;
                }
            }
            return null;
        }

        /// <summary>Painted dots joined to the line: at its nearer end (a stroke from its start goes before it).</summary>
        private void Join(List<Vector2> dots)
        {
            if (_points.Count > 1 && Vector2.Distance(dots[0], _points[0]) < Vector2.Distance(dots[0], _points[^1]))
            {
                dots.Reverse();
                _points.InsertRange(0, dots);
            }
            else
            {
                _points.AddRange(dots);
            }
        }

        /// <summary>The scene's objects that belong to a road (its edge objects and bridges).</summary>
        private static List<int> ThingsOf(EditSession s, string road)
        {
            var things = s.Scene.Things;
            lock (things)
            {
                return [.. Enumerable.Range(0, things.Count).Where(i => !things[i].Gone && s.Edits.DataOf(things[i].Id).TryGetValue(MapRoads.DataKey, out string? r) && r == road)];
            }
        }

        /// <summary>A road's old ground put back (when it was numbered on this grid): the points put back.</summary>
        private static List<int> PutBack(Ground g, MapRoads.Road? road)
        {
            var back = new List<int>();
            if (road?.Before is not { } before || road.Grid != (g.W, g.H, g.X0, g.Z0))
            {
                return back;
            }
            foreach (var (p, (h, paint)) in before)
            {
                g.SetHeight(p, h);
                paint.AsSpan(0, Math.Min(paint.Length, g.C)).CopyTo(g.Paint.AsSpan(p * g.C, g.C));
                back.Add(p);
            }
            return back;
        }

        /// <summary>The grid box round some points.</summary>
        private static (int X0, int Z0, int X1, int Z1) Box(Ground g, IEnumerable<int> points)
        {
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = -1, z1 = -1;
            foreach (int p in points)
            {
                int gx = p % g.W, gz = p / g.W;
                (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
            }
            return x1 < 0 ? (0, 0, 0, 0) : (x0 - 1, z0 - 1, x1 + 1, z1 + 1);
        }

        private void Make(IToolContext ctx)
        {
            if (Session is not { } s || _points.Count < 2)
            {
                ctx.Status("A path needs two points or more.");
                return;
            }
            var spec = Spec(ctx);
            string? bridge = Model(ctx, "bridge");
            var old = _road;
            bool keep = old != null || ctx.IsOn("keep");
            string name = old?.Name ?? MapRoads.NewName();
            List<Vector2> line = [.. _points];
            var crossings = bridge != null ? MapRoads.Crossings(line) : [];
            float reach = (spec.Width / 2) + spec.Shoulder;
            // under a bridge the river's channel is left as it is
            Func<Vector2, bool>? leave = crossings.Count == 0 ? null
                : q => crossings.Any(c => Vector2.Distance(q, c.At) < reach + c.HalfWidth + 2 && MapRoads.Distance(q, c.A, c.B) < c.HalfWidth + 0.5f);
            var before = new Dictionary<int, MapRoads.Point>();
            var adds = new List<(NewObject Object, bool Piece)>();
            var was = MapRoads.Snapshot();
            MapRoads.Road[]? now = null;
            (int, int, int, int) grid = default;
            string label = old != null ? "Road again: " + name : spec.Even && line.Count == 2 ? "Ramp" : keep ? "Road: " + name : "Path";
            int n = s.Commit(label, g =>
            {
                grid = (g.W, g.H, g.X0, g.Z0);
                var touched = PutBack(g, old);
                var (made, _) = Paths.Apply(g, line, spec, adds, keep ? before : null, leave);
                foreach (var (at, yaw, half, _, _) in crossings)
                {
                    // the bridge's deck at the road's height on the banks
                    var d = new Vector2(MathF.Sin(yaw * MathF.PI / 180), MathF.Cos(yaw * MathF.PI / 180)) * (half + 1.5f);
                    float h = MathF.Max(Paths.HeightAt(g, at.X - d.X, at.Y - d.Y), Paths.HeightAt(g, at.X + d.X, at.Y + d.Y));
                    adds.Add((new NewObject(0, Groundwork.App.AssetIds.Of(bridge!), new Vector3(at.X, h, at.Y), new Vector3(0, yaw, 0), 0), false));
                }
                if (keep)
                {
                    var data = new Dictionary<string, string> { [MapRoads.DataKey] = name };
                    for (int i = 0; i < adds.Count; i++)
                    {
                        adds[i] = (adds[i].Object with { Data = data }, adds[i].Piece);
                    }
                }
                touched.AddRange(made);
                return (touched, Box(g, touched));
            }, old != null ? ThingsOf(s, old.Name) : [], adds, keep ? again => MapRoads.Restore(again ? now! : was) : null).Count;
            if (keep)
            {
                var road = new MapRoads.Road(name, line, spec.Width, spec.Shoulder, spec.Even, spec.Smoothing,
                    spec.Layer >= 0 ? MapProject.Current?.Layers.ElementAtOrDefault(spec.Layer)?.Name : null, spec.Edge, spec.Spacing, spec.Offset, spec.BothSides, bridge)
                { Before = before, Grid = grid };
                now = [.. was.Where(r => r != old), road];
                if (old != null)
                {
                    now = [.. was.Select(r => r == old ? road : r)];
                }
                MapRoads.Restore(now);
                // it stays picked, its dots there to nudge
                _road = road;
            }
            else
            {
                _points.Clear();
            }
            ctx.Status($"{label} made" + (n > 0 ? $", {n} objects" : "") + (crossings.Count > 0 ? $", {crossings.Count} bridges." : "."));
            List(ctx);
        }

        /// <summary>A kept road taken away: its old ground put back, its objects removed, one step.</summary>
        private void TakeAway(IToolContext ctx, MapRoads.Road road)
        {
            if (Session is not { } s)
            {
                return;
            }
            var was = MapRoads.Snapshot();
            MapRoads.Road[] now = [.. was.Where(r => r != road)];
            _ = s.Commit("Take away " + road.Name, g =>
            {
                var back = PutBack(g, road);
                return (back, Box(g, back));
            }, ThingsOf(s, road.Name), [], again => MapRoads.Restore(again ? now : was));
            MapRoads.Restore(now);
            _road = null;
            _points.Clear();
            ctx.Status(road.Before == null ? $"{road.Name} taken away (its old ground was not known: the map grew since)." : $"{road.Name} taken away, its old ground put back.");
            List(ctx);
        }

        /// <summary>The line drawn as a band the path's width on the ground, its points as dots.</summary>
        private void Show(IToolContext ctx)
        {
            var area = ctx.Area;
            var shapes = new List<Shape>();
            float mark = MathF.Max(1, ctx.Number("width") / 2);
            if (_points.Count > 0)
            {
                shapes.Add(Discs(_points.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.3f, q.Y)), mark, new Vector4(1, 0.85f, 0.2f, 0.9f)));
            }
            if (_stroke is { Count: > 1 })
            {
                shapes.Add(Band([.. _stroke.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.15f, q.Y))], ctx.Number("width"), new Vector4(1, 0.85f, 0.4f, 0.4f)));
            }
            // where the next point goes, and the stretch to it from the last one
            else if (_hover is Vector2 h && _drag < 0)
            {
                shapes.Add(Ring(area, new Vector3(h.X, area.GroundHeight(h.X, h.Y), h.Y), mark, new Vector4(1, 0.85f, 0.2f, 0.9f)));
                if (_points.Count > 0)
                {
                    var (next, _) = Paths.Profile([_points[^1], h], area.GroundHeight, true, 0);
                    shapes.Add(Band([.. next.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.15f, q.Y))], ctx.Number("width"), new Vector4(1, 0.85f, 0.4f, 0.25f)));
                }
            }
            if (_points.Count > 1)
            {
                var (line, _) = Paths.Profile(_points, area.GroundHeight, true, 0);
                var band = line.Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.15f, q.Y)).ToList();
                shapes.Add(Band(band, ctx.Number("width"), new Vector4(1, 0.7f, 0.2f, 0.45f)));
                shapes.Add(Band(band, ctx.Number("width") + (2 * ctx.Number("shoulder")), new Vector4(1, 0.7f, 0.2f, 0.15f)));
            }
            // the kept roads, faint
            foreach (var r in MapRoads.All.Where(r => r != _road))
            {
                var (kept, _) = Paths.Profile(r.Points, area.GroundHeight, true, 0);
                shapes.Add(Band([.. kept.Where((_, i) => i % 4 == 0).Select(q => new Vector3(q.X, area.GroundHeight(q.X, q.Y) + 0.2f, q.Y))], r.Width, new Vector4(0.3f, 0.75f, 1, 0.3f)));
            }
            ctx.Preview(shapes);
            ctx.SetText("status", _road != null ? $"{_road.Name}: drag a dot to nudge it, paint on from an end, or take it away. Esc lets go of it." : _points.Count switch
            {
                0 => "Drag along the ground to paint a line, or click point by point.",
                1 => "Click the next point.",
                _ => $"{_points.Count} points, {_points.Zip(_points.Skip(1), Vector2.Distance).Sum():0} m: Make it (Enter), or add more.",
            });
        }
    }
}
