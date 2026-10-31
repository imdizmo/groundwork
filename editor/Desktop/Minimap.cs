using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Groundwork.Desktop;

/// <summary>
/// The map's map, in the view's corner: the ground from above (its colours, hill shading, the sea),
/// the rivers and lakes over it, and the camera as an arrow; a click turns the camera there. Drawn again when the map
/// changed (every two seconds at most); the camera follows four times a second. North is up.
/// </summary>
internal sealed class Minimap : IDisposable
{
    private const int N = 200;
    private readonly MainWindow _w;
    private readonly WriteableBitmap _bitmap = new(new PixelSize(N, N), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private readonly int[] _pixels = new int[N * N];
    private readonly Canvas _over = new() { Width = N, Height = N, ClipToBounds = true };
    private readonly Polygon _camera = new() { Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1, Points = [new Point(0, -7), new Point(5, 5), new Point(0, 2), new Point(-5, 5)] };
    private readonly Border _body;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _drawnVersion = -1;
    private bool _waterChanged = true;
    private DateTime _drawnAt;

    /// <summary>A minimap for the window's map (shown with Card).</summary>
    public Minimap(MainWindow w)
    {
        _w = w;
        var image = new Image { Source = _bitmap, Width = N, Height = N };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
        _over.Children.Add(_camera);
        var map = new Grid { Width = N, Height = N, Cursor = new Cursor(StandardCursorType.Hand), Children = { image, _over } };
        ToolTip.SetTip(map, "Click to look there (north is up)");
        map.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(map);
            if (Grid() is var (g, _) && g != null)
            {
                var (x, z) = (g.OriginX + (p.X / N * g.W), g.OriginZ + ((N - p.Y) / N * g.H));
                float yaw = _w.View.WorldCamera is { } c ? c.Yaw * 180 / MathF.PI : 30;
                float pitch = _w.View.WorldCamera is { } c2 ? c2.Pitch * 180 / MathF.PI : 50;
                _w.View.Orbit((float)x, (float)z, yaw, pitch, _w.View.WorldCamera?.Distance ?? 80);
            }
            e.Handled = true;
        };
        _body = new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = map, IsVisible = w.Prefs.Get("map.map.open", true) };
        var toggle = new Button { Content = _body.IsVisible ? "Map ▾" : "Map ▴", FontSize = 11, Padding = new Thickness(6, 1), HorizontalAlignment = HorizontalAlignment.Right };
        toggle.Click += (_, _) =>
        {
            _body.IsVisible = !_body.IsVisible;
            toggle.Content = _body.IsVisible ? "Map ▾" : "Map ▴";
            w.Prefs.Set("map.map.open", _body.IsVisible);
        };
        Card = Ui.AsCard(new Border { Child = new StackPanel { Spacing = 4, Children = { toggle, _body } } });
        Card.Padding = new Thickness(6);
        Card.HorizontalAlignment = HorizontalAlignment.Right;
        Card.VerticalAlignment = VerticalAlignment.Bottom;
        MapWater.Changed += () => _waterChanged = true;
        _timer.Tick += (_, _) => Tick();
        Card.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty)
            {
                _timer.IsEnabled = Card.IsVisible;
            }
        };
        _timer.IsEnabled = Card.IsVisible;
    }

    /// <summary>Stops following the camera and lets the picture go.</summary>
    public void Dispose()
    {
        _timer.Stop();
        _bitmap.Dispose();
    }

    /// <summary>The map's card.</summary>
    public Border Card { get; }

    private (Ground? Ground, EditSession? Session) Grid() => _w.Session is { Scene.IsMap: true } s ? (s.Ground, s) : (null, null);

    private void Tick()
    {
        if (!_body.IsVisible || Grid() is not (Ground g, EditSession s))
        {
            return;
        }
        if ((s.Edits.Version != _drawnVersion || _waterChanged) && DateTime.Now - _drawnAt > TimeSpan.FromSeconds(2))
        {
            Draw(g);
            Water(g);
            _drawnVersion = s.Edits.Version;
            _waterChanged = false;
            _drawnAt = DateTime.Now;
        }
        if (_w.View.WorldCamera is { } c)
        {
            var (px, py) = ToMap(g, c.World.X, c.World.Z);
            _camera.RenderTransform = new TransformGroup { Children = { new RotateTransform(-c.Yaw * 180 / MathF.PI), new TranslateTransform(px, py) } };
        }
    }

    private static (double X, double Y) ToMap(Ground g, float x, float z) => ((x - g.OriginX) / g.W * N, N - ((z - g.OriginZ) / g.H * N));

    /// <summary>The ground from above in its layers' colours, shaded by its slope; the sea blue by depth.</summary>
    private void Draw(Ground g)
    {
        float sea = _w.Session?.Scene.Water ?? 0;
        var scene = _w.Session!.Scene;
        for (int py = 0; py < N; py++)
        {
            int gz = Math.Clamp((int)((N - 1 - py) * (g.H - 1) / (float)(N - 1)), 1, g.H - 2);
            for (int px = 0; px < N; px++)
            {
                int gx = Math.Clamp((int)(px * (g.W - 1) / (float)(N - 1)), 1, g.W - 2);
                int p = (gz * g.W) + gx;
                float h = g.HeightOf(p);
                float r, gr, b;
                if (h < sea)
                {
                    float deep = Math.Clamp((sea - h) / 8f, 0, 1);
                    (r, gr, b) = (0.2f - (0.1f * deep), 0.42f - (0.15f * deep), 0.5f - (0.1f * deep));
                }
                else
                {
                    var c = scene.GroundColor(p);   // the layers blended, as the 3D view draws them
                    (r, gr, b) = (MathF.Pow(c.X, 1 / 2.2f), MathF.Pow(c.Y, 1 / 2.2f), MathF.Pow(c.Z, 1 / 2.2f));
                    // Light from the north-west.
                    float shade = Math.Clamp(1 + ((g.HeightOf(p - 1) - g.HeightOf(p + 1) + g.HeightOf(p + g.W) - g.HeightOf(p - g.W)) * 0.35f), 0.55f, 1.35f);
                    (r, gr, b) = (r * shade, gr * shade, b * shade);
                }
                _pixels[(py * N) + px] = unchecked((int)0xFF000000) | (Byte(r) << 16) | (Byte(gr) << 8) | Byte(b);
            }
        }
        using var fb = _bitmap.Lock();
        for (int y = 0; y < N; y++)
        {
            Marshal.Copy(_pixels, y * N, fb.Address + (y * fb.RowBytes), N);
        }
        _over.InvalidateVisual();
    }

    private static int Byte(float v) => (int)Math.Clamp(v * 255, 0, 255);

    /// <summary>The rivers (as wide as they are) and lakes over the ground.</summary>
    private void Water(Ground g)
    {
        _over.Children.Clear();
        var water = new SolidColorBrush(Color.FromRgb(70, 150, 170));
        foreach (var r in MapWater.Rivers.Where(r => r.Points.Count > 1))
        {
            for (int i = 0; i + 1 < r.Points.Count; i++)
            {
                var (a, b) = (r.Points[i], r.Points[i + 1]);
                var (ax, ay) = ToMap(g, a.X, -a.Y);
                var (bx, by) = ToMap(g, b.X, -b.Y);
                _over.Children.Add(new Line { StartPoint = new Point(ax, ay), EndPoint = new Point(bx, by), Stroke = water, StrokeThickness = Math.Max(1.5, (a.HalfWidth + b.HalfWidth) / g.W * N), StrokeLineCap = PenLineCap.Round });
            }
        }
        foreach (var l in MapWater.Lakes.Where(l => l.Outline.Count > 2))
        {
            _over.Children.Add(new Polygon { Fill = water, Points = [.. l.Outline.Select(q => ToMap(g, q.X, -q.Y)).Select(m => new Point(m.X, m.Y))] });
        }
        _over.Children.Add(_camera);
    }
}
