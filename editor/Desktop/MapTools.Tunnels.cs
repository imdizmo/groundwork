using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// Tunnels: lines of points under the ground, each with a depth and a radius, drawn as tubes of rock with their mouths
    /// open in the hillside (MapCaves). Click to add a point, drag one to move it, Shift+click to delete it; the sliders
    /// set the picked point; Cut paints the mouths into the hole channel for Unreal. Every change is a step in History.
    /// </summary>
    public sealed class TunnelTool(MainWindow w) : Base(w)
    {
        private int _point = -1;
        private bool _dragging;
        private string? _before, _action;
        private (EditSession.Change Change, StepState State, string Label, DateTime At)? _lastStep;
        private Action? _refresh;

        /// <summary>The tunnels before and after one step (a slider dragged on keeps updating its step's after).</summary>
        private sealed class StepState(string before, string after)
        {
            public string Before { get; } = before;
            public string After { get; set; } = after;
        }

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("tunnel", "Tunnels", "Pick a tunnel or make one, then click the ground to add a point, drag a point to move it, Shift+click a point to delete it. The sliders set the picked point's depth (its roof under the ground) and radius; Cut paints the mouths into the ground's holes for Unreal.")
        {
            Icon = "M3 20v-8a9 8 0 0 1 18 0v8M8 20v-5a4 4 0 0 1 8 0v5",
        };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ListControl("tunnel", "Tunnels"),
            new ButtonControl("new", "New tunnel"),
            new ButtonControl("delete", "Delete this one"),
            new SliderControl("radius", "Radius", 1, 15, 3) { Step = 0.5f, Unit = "m" },
            new SliderControl("depth", "Depth", 0.5f, 60, 1.5f) { Step = 0.5f, Unit = "m" },
            new ToggleControl("endOpen", "Far end open", true),
            new ButtonControl("cut", "Cut the mouths"),
            new ButtonControl("cutAll", "Cut every tunnel's mouths"),
            new TextControl("status", ""),
        ];

        private static MapCaves.Tunnel? Tunnel(IToolContext ctx) => ctx.Choice("tunnel") is int i && i >= 0 && i < MapCaves.Tunnels.Count ? MapCaves.Tunnels[i] : null;

        /// <summary>The tunnels as they are before an edit.</summary>
        private void Start() => _before ??= MapCaves.Snapshot();

        /// <summary>
        /// The edit since Start as a step in the history; merged into the last one when it is the same slider moved
        /// again within a few seconds.
        /// </summary>
        private void Record(string label, bool merge = false)
        {
            if (_before is not string before || Session is not { } s)
            {
                return;
            }
            _before = null;
            string after = MapCaves.Snapshot();
            if (after == before)
            {
                return;
            }
            if (merge && _lastStep is var (change, state, last, at) && last == label && (DateTime.Now - at).TotalSeconds < 3 && s.UndoList.Count > 0 && ReferenceEquals(s.UndoList[^1], change))
            {
                state.After = after;
                _lastStep = (change, state, label, DateTime.Now);
                return;
            }
            var kept = new StepState(before, after);
            _lastStep = (s.RecordExtra(label, again => MapCaves.Restore(again ? kept.After : kept.Before)), kept, label, DateTime.Now);
        }

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            MapCaves.Changed -= _refresh;
            _refresh = () => List(ctx);
            MapCaves.Changed += _refresh;
            List(ctx);
            Fields(ctx);
            Show(ctx, null);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx)
        {
            MapCaves.Changed -= _refresh;
            _refresh = null;
            _before = null;
            ctx.Preview([]);
        }

        private static void List(IToolContext ctx)
        {
            int picked = Math.Clamp(ctx.Choice("tunnel"), 0, Math.Max(0, MapCaves.Tunnels.Count - 1));
            ctx.SetList("tunnel", [.. MapCaves.Tunnels.Select(t => (t.Name, $"{t.Points.Count} points", t.EndOpen ? "through" : "cave"))], picked);
        }

        /// <summary>The sliders show the picked point (or the tunnel's first).</summary>
        private void Fields(IToolContext ctx)
        {
            if (Tunnel(ctx) is not { } t)
            {
                ctx.SetText("status", MapCaves.Tunnels.Count == 0 ? "New tunnel, then click the ground where its mouth is and on into the hill." : "");
                return;
            }
            var p = _point >= 0 && _point < t.Points.Count ? t.Points[_point] : t.Points.FirstOrDefault();
            if (p != null)
            {
                ctx.SetNumber("radius", p.Radius);
                ctx.SetNumber("depth", p.Depth);
            }
            ctx.SetText("status", t.Points.Count < 2 ? "Click the ground to add points: the first is the mouth." : $"{t.Name}: {t.Points.Count} points.");
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            switch (id)
            {
                case "new":
                    Start();
                    MapCaves.Tunnels.Add(new MapCaves.Tunnel { Name = $"Tunnel {MapCaves.Tunnels.Count + 1}", EndOpen = ctx.IsOn("endOpen") });
                    _point = -1;
                    MapCaves.Touch();
                    ctx.SetList("tunnel", [.. MapCaves.Tunnels.Select(t => (t.Name, $"{t.Points.Count} points", t.EndOpen ? "through" : "cave"))], MapCaves.Tunnels.Count - 1);
                    Record("Tunnel: new");
                    break;
                case "delete" when Tunnel(ctx) is { } gone:
                    Start();
                    _ = MapCaves.Tunnels.Remove(gone);
                    _point = -1;
                    MapCaves.Touch();
                    Record("Tunnel: delete");
                    break;
                case "radius" or "depth" when Tunnel(ctx) is { } t && _point >= 0 && _point < t.Points.Count:
                    Start();
                    t.Points[_point] = t.Points[_point] with { Radius = ctx.Number("radius"), Depth = ctx.Number("depth") };
                    MapCaves.Touch();
                    Record(id == "radius" ? "Tunnel: radius" : "Tunnel: depth", merge: true);
                    break;
                case "endOpen" when Tunnel(ctx) is { } t:
                    Start();
                    t.EndOpen = ctx.IsOn("endOpen");
                    MapCaves.Touch();
                    Record("Tunnel: far end");
                    break;
                case "cut" when Tunnel(ctx) is { } t && Session is { } s:
                    ctx.Status($"Cut {MapCaves.Cut(s, t, ctx.Area.GroundHeight)} ground points at the mouths of {t.Name}.");
                    break;
                case "cutAll" when Session is { } s:
                    ctx.Status($"Cut {MapCaves.Tunnels.Sum(t => MapCaves.Cut(s, t, ctx.Area.GroundHeight))} ground points at the mouths.");
                    break;
                case "tunnel":
                    _point = -1;
                    break;
                default:
                    break;
            }
            Fields(ctx);
            Show(ctx, null);
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            // On the ground, or on a tunnel's wall: the point under the pointer, by its x and z.
            Vector3? at = input.Target == PointerTarget.Scene ? input.Point with { Y = ctx.Area.GroundHeight(input.Point.X, input.Point.Z) } : Ground(ctx, input);
            if (at is Vector3 p && Tunnel(ctx) is { } t)
            {
                switch (input.Action)
                {
                    case PointerAction.Press:
                        Start();
                        int near = t.Points.FindIndex(q => Vector2.Distance(new Vector2(q.X, q.Z), new Vector2(p.X, p.Z)) < MathF.Max(3, q.Radius * 0.8f));
                        _action = near < 0 ? "add point" : input.Modifiers.HasFlag(KeyModifiers.Shift) ? "delete point" : "move point";
                        if (near >= 0 && input.Modifiers.HasFlag(KeyModifiers.Shift))
                        {
                            t.Points.RemoveAt(near);
                            _point = -1;
                        }
                        else if (near >= 0)
                        {
                            _point = near;
                            _dragging = true;
                        }
                        else
                        {
                            _point = Add(ctx, t, p);
                            _dragging = true;
                        }
                        MapCaves.Touch();
                        Fields(ctx);
                        break;
                    case PointerAction.Drag when _dragging && _point >= 0 && _point < t.Points.Count:
                        t.Points[_point] = t.Points[_point] with { X = p.X, Z = p.Z };
                        MapCaves.Touch();
                        break;
                    case PointerAction.Release:
                        _dragging = false;
                        Record("Tunnel: " + (_action ?? "edit"));
                        break;
                    default:
                        break;
                }
            }
            Show(ctx, at);
        }

        /// <summary>A new point where it fits: inserted in the nearest stretch, or added at the nearer end.</summary>
        private static int Add(IToolContext ctx, MapCaves.Tunnel t, Vector3 p)
        {
            var u = new Vector2(p.X, p.Z);
            int at = t.Points.Count;
            if (t.Points.Count >= 2)
            {
                float best = float.MaxValue;
                for (int i = 0; i < t.Points.Count - 1; i++)
                {
                    float d = MapWater.SegmentDistance(u, new Vector2(t.Points[i].X, t.Points[i].Z), new Vector2(t.Points[i + 1].X, t.Points[i + 1].Z), out float k);
                    if (d < best && k > 0.02f && k < 0.98f)
                    {
                        (best, at) = (d, i + 1);
                    }
                }
                if (best > 12)
                {
                    at = Vector2.Distance(u, new Vector2(t.Points[0].X, t.Points[0].Z)) < Vector2.Distance(u, new Vector2(t.Points[^1].X, t.Points[^1].Z)) ? 0 : t.Points.Count;
                }
            }
            t.Points.Insert(at, new MapCaves.Point(p.X, p.Z, ctx.Number("depth"), ctx.Number("radius")));
            return at;
        }

        /// <summary>The picked tunnel's points as marks on the ground, its line as a band, the pointer's spot as a ring.</summary>
        private void Show(IToolContext ctx, Vector3? at)
        {
            var area = ctx.Area;
            var shapes = new List<Shape>();
            var (white, orange, line) = (new Vector4(1, 1, 1, 0.95f), new Vector4(1, 0.6f, 0.1f, 1), new Vector4(0.9f, 0.75f, 0.5f, 0.7f));
            if (Tunnel(ctx) is { } t && t.Points.Count > 0)
            {
                Vector3 Lift(MapCaves.Point q) => new(q.X, area.GroundHeight(q.X, q.Z) + 0.4f, q.Z);
                var pts = t.Points.Select(Lift).ToList();
                if (pts.Count > 1)
                {
                    shapes.Add(Band(pts, 0.5f, line));
                }
                shapes.Add(Dots(pts.Where((_, i) => i != _point), 0.6f, white));
                if (_point >= 0 && _point < pts.Count)
                {
                    shapes.Add(Dots([pts[_point]], 0.8f, orange));
                    shapes.Add(Ring(area, pts[_point], t.Points[_point].Radius, orange));
                }
            }
            if (at is { } p)
            {
                shapes.Add(Ring(area, p, ctx.Number("radius"), white));
            }
            ctx.Preview(shapes);
        }
    }
}
