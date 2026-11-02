using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapTools
{
    /// <summary>
    /// Notes: what the user wants Claude to look at. A click pins the note typed on the card there (on the ground or an
    /// object), a drag draws a line, Shift+click deletes one; Send takes a picture of the view with them in it, with a
    /// message. Claude reads them (map_notes), answers them and pins its own. Kept in the map's notes folder (MapNotes).
    /// </summary>
    // ponytail: pins and lines show while this tool is chosen only, as the areas do
    public sealed class NotesTool(MainWindow w) : Base(w)
    {
        private static readonly Vector4 Yours = new(1f, 0.55f, 0.1f, 0.9f), Claudes = new(0.3f, 0.6f, 1f, 0.9f), Dealt = new(0.35f, 0.8f, 0.45f, 0.7f), Ink = new(1f, 0.15f, 0.15f, 0.9f);
        private List<Vector3>? _draft;
        private Action? _refresh;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("notes", "Notes", "Notes for Claude: type a note and click where it belongs (the ground or an object) to pin it there, drag to draw a line round what you mean, Shift+click deletes one. Send the view to Claude takes a picture of the view with them, and your message. Claude reads them, answers them and pins its own (blue).") { Icon = "M12 21s-6-5.5-6-11a6 6 0 0 1 12 0c0 5.5-6 11-6 11zM10 10a2 2 0 1 0 4 0a2 2 0 1 0-4 0" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new TextBoxControl("text", "Note", "what to look at here"),
            new ListControl("pins", "Notes"),
            new ButtonControl("done", "Done or not done"),
            new ButtonControl("delete", "Delete this note"),
            new ButtonControl("clear", "Clear the lines"),
            new TextBoxControl("message", "Message", "for Claude, with the picture"),
            new ButtonControl("send", "Send the view to Claude"),
            new TextControl("status", "Click: pin the note · drag: draw · Shift+click: delete"),
        ];

        private static MapNotes.Pin? Picked(IToolContext ctx) => ctx.Choice("pins") is int i && i >= 0 && i < MapNotes.Pins.Count ? MapNotes.Pins[i] : null;

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            MapNotes.Changed -= _refresh;
            _refresh = () =>
            {
                List(ctx);
                Show(ctx);
            };
            MapNotes.Changed += _refresh;
            List(ctx);
            Show(ctx);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx) => MapNotes.Changed -= _refresh;

        private static void List(IToolContext ctx, int? pick = null) =>
            ctx.SetList("pins", [.. MapNotes.Pins.Select(p => ($"{p.Id}. {(p.Text.Length > 0 ? p.Text : "(no text)")}", p.Reply is { } r ? "Claude: " + r : $"at {p.X:0}, {p.Z:0}", p.Done ? "done" : p.By == "Claude" ? "Claude" : ""))],
                Math.Clamp(pick ?? ctx.Choice("pins"), 0, Math.Max(0, MapNotes.Pins.Count - 1)));

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            switch (id)
            {
                case "pins":
                    Show(ctx);
                    break;
                case "done" when Picked(ctx) is { } p:
                    p.Done = !p.Done;
                    MapNotes.Touch();
                    break;
                case "delete" when Picked(ctx) is { } p:
                    _ = MapNotes.Pins.Remove(p);
                    MapNotes.Touch();
                    break;
                case "clear":
                    MapNotes.Strokes.Clear();
                    MapNotes.Touch();
                    break;
                case "send":
                    _ = Send(ctx);
                    break;
                default:
                    break;
            }
        }

        /// <summary>The view as it is (pins and lines in it) to the notes folder, with the message.</summary>
        private async Task Send(IToolContext ctx)
        {
            string path = Path.Combine(Path.GetTempPath(), $"gw-note-{Guid.NewGuid():N}.png");
            try
            {
                ctx.SetText("status", "Taking the picture...");
                await W.View.Picture(path).WaitAsync(TimeSpan.FromSeconds(60));
                _ = MapNotes.Send(ctx.Typed("message"), Numbered(await File.ReadAllBytesAsync(path)));
                ctx.SetText("message", "");
                ctx.SetText("status", "Sent: Claude sees it with map_notes.");
            }
            catch (Exception e) when (e is IOException or TimeoutException or InvalidOperationException)
            {
                ctx.SetText("status", "Not sent: " + e.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>The picture with each pin's number by its head, so the reader can tell which note is which.</summary>
        private byte[] Numbered(byte[] png)
        {
            if (W.View.Scene is not { } scene || W.View.Bounds.Width <= 0)
            {
                return png;
            }
            using var bmp = SkiaSharp.SKBitmap.Decode(png);
            float k = (float)(bmp.Width / W.View.Bounds.Width);
            using (var g = new SkiaSharp.SKCanvas(bmp))
            using (var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 15 * k))
            using (var back = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(20, 20, 20, 220), IsAntialias = true })
            using (var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White, IsAntialias = true })
            {
                foreach (var p in MapNotes.Pins)
                {
                    if (W.View.ScreenOfGrid(new Vector2(p.X - scene.OriginX, p.Z - scene.OriginZ), 8f) is { } at)
                    {
                        float x = (float)at.X * k, y = (float)at.Y * k;
                        g.DrawCircle(x, y - (10 * k), 11 * k, back);
                        g.DrawText(p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), x, y - (5 * k), SkiaSharp.SKTextAlign.Center, font, ink);
                    }
                }
            }
            using var data = bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if ((input.Target == PointerTarget.Thing ? input.Point : Ground(ctx, input)) is not Vector3 p)
            {
                return;
            }
            switch (input.Action)
            {
                case PointerAction.Press when input.Modifiers.HasFlag(KeyModifiers.Shift):
                    Delete(p);
                    break;
                case PointerAction.Press:
                    _draft = [p];
                    break;
                case PointerAction.Drag when _draft != null && Vector3.Distance(_draft[^1], p) > 0.3f:
                    _draft.Add(p);
                    break;
                case PointerAction.Release when _draft is { } d:
                    _draft = null;
                    if (d.Count > 1 || Vector3.Distance(d[0], p) > 1)
                    {
                        d.Add(p);
                        MapNotes.Strokes.Add(d);
                        MapNotes.Touch();
                    }
                    else
                    {
                        _ = MapNotes.Add(d[0].X, d[0].Y, d[0].Z, ctx.Typed("text").Trim());
                        ctx.SetText("text", "");
                        List(ctx, MapNotes.Pins.Count - 1);
                    }
                    break;
                default:
                    break;
            }
            Show(ctx);
        }

        /// <summary>The pin within 3 m of a point, else the line passing within 2 m, deleted.</summary>
        private static void Delete(Vector3 p)
        {
            if (MapNotes.Pins.OrderBy(q => Vector3.Distance(new(q.X, q.Y, q.Z), p)).FirstOrDefault() is { } pin && Vector3.Distance(new(pin.X, pin.Y, pin.Z), p) < 3)
            {
                _ = MapNotes.Pins.Remove(pin);
            }
            else if (MapNotes.Strokes.FirstOrDefault(s => s.Any(q => Vector3.Distance(q, p) < 2)) is { } line)
            {
                _ = MapNotes.Strokes.Remove(line);
            }
            else
            {
                return;
            }
            MapNotes.Touch();
        }

        /// <summary>Each pin a post with a head (yours orange, Claude's blue, done green; the picked one bigger), each line a red ribbon.</summary>
        private void Show(IToolContext ctx)
        {
            var shapes = new List<Shape>();
            var picked = Picked(ctx);
            foreach (var p in MapNotes.Pins)
            {
                var colour = p.Done ? Dealt : p.By == "Claude" ? Claudes : Yours;
                float k = p == picked ? 1.6f : 1;
                var foot = new Vector3(p.X, p.Y, p.Z);
                shapes.Add(Cube(foot + new Vector3(0, 2 * k, 0), new Vector3(0.08f, 2 * k, 0.08f), colour));
                shapes.Add(Cube(foot + new Vector3(0, 4.4f * k, 0), new Vector3(0.5f * k), colour));
            }
            foreach (var line in _draft is { Count: > 1 } d ? [.. MapNotes.Strokes, d] : MapNotes.Strokes)
            {
                shapes.Add(Ribbon(line, 0.3f, Ink));
            }
            ctx.Preview(shapes);
        }

        /// <summary>A box round a centre, half its size each way.</summary>
        private static Shape Cube(Vector3 c, Vector3 half, Vector4 colour)
        {
            Vector3[] v = [.. Enumerable.Range(0, 8).Select(i => c + (half * new Vector3((i & 1) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1)))];
            int[] t = [0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3];
            return new Shape(v, t, colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>A flat ribbon along points, a little above them, half wide each side.</summary>
        private static Shape Ribbon(List<Vector3> points, float half, Vector4 colour)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                var along = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(i - 1, 0)];
                var side = Vector3.Cross(along, Vector3.UnitY);
                side = side.LengthSquared() > 1e-6f ? Vector3.Normalize(side) * half : Vector3.UnitX * half;
                var at = points[i] + new Vector3(0, 0.2f, 0);
                v.Add(at - side);
                v.Add(at + side);
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    t.AddRange([b, b + 1, b + 3, b, b + 3, b + 2]);
                }
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }
    }
}
