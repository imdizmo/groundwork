using System.Numerics;
using Groundwork.Sdk;
using SkiaSharp;

namespace Groundwork.Desktop;

/// <summary>
/// A picture of the whole map seen from straight above, north up, drawn as a map: the ground's colours shaded by its
/// hills (the light from the north-west), the sea and lakes, the rivers, the regions' outlines and the
/// names; a scale bar and a north arrow. In colour, or on parchment. For a game's own world map, a design page, a
/// team's wall.
/// </summary>
internal static class MapPicture
{
    /// <summary>The looks a picture is drawn in.</summary>
    internal enum Style
    {
        /// <summary>The ground's own colours.</summary>
        Colour,
        /// <summary>Browns on old paper, the water pale.</summary>
        Parchment,
    }

    /// <summary>The picture as a PNG, its longer side <paramref name="longSide"/> pixels, and its size.</summary>
    /// <param name="area">The map's ground (world metres).</param>
    /// <param name="sea">The sea level.</param>
    /// <param name="style">The look.</param>
    /// <param name="longSide">The longer side in pixels.</param>
    internal static (byte[] Png, int W, int H) Of(IAreaView area, float sea, Style style, int longSide = 2048)
    {
        float mw = area.MaxX - area.MinX, md = area.MaxZ - area.MinZ;
        float px = longSide / MathF.Max(mw, md);   // pixels a metre
        int w = Math.Max(16, (int)MathF.Round(mw * px)), h = Math.Max(16, (int)MathF.Round(md * px));
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        Ground(area, sea, style, bmp, px);
        using (var g = new SKCanvas(bmp))
        {
            Vector2 ToPx(Vector2 world) => new((world.X - area.MinX) * px, (area.MaxZ - world.Y) * px);
            float k = longSide / 1400f;   // the lettering's and the lines' scale
            bool old = style == Style.Parchment;
            Water(g, ToPx, px, old);
            Regions(g, ToPx, k, old);
            Names(g, ToPx, k, old, w, h);
            Furniture(g, w, h, px, k, old);
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return (data.ToArray(), w, h);
    }

    /// <summary>The ground, a pixel at a time: its colour (between the four grid points round it), shaded by the slope, or the sea.</summary>
    private static void Ground(IAreaView area, float sea, Style style, SKBitmap bmp, float px)
    {
        int w = bmp.Width, h = bmp.Height;
        var light = Vector3.Normalize(new Vector3(-1, 1.4f, 1));   // from the north-west, high
        uint[] pixels = new uint[w * h];
        float step = MathF.Max(0.5f, 1 / px);
        Parallel.For(0, h, y =>
        {
            float z = area.MaxZ - ((y + 0.5f) / px);
            for (int x = 0; x < w; x++)
            {
                float wx = area.MinX + ((x + 0.5f) / px);
                float hgt = area.GroundHeight(wx, z);
                // twice the slope: low hills still read
                float dx = (area.GroundHeight(wx + step, z) - area.GroundHeight(wx - step, z)) / (2 * step) * 2;
                float dz = (area.GroundHeight(wx, z + step) - area.GroundHeight(wx, z - step)) / (2 * step) * 2;
                float shade = Math.Clamp(0.45f + (0.75f * Vector3.Dot(Vector3.Normalize(new Vector3(-dx, 1, -dz)), light)), 0.35f, 1.25f);
                Vector3 c;
                if (hgt < sea)
                {
                    float deep = Math.Clamp((sea - hgt) / 25, 0, 1);
                    c = style == Style.Parchment ? Vector3.Lerp(new(0.80f, 0.80f, 0.70f), new(0.62f, 0.66f, 0.62f), deep) : Vector3.Lerp(new(0.36f, 0.62f, 0.72f), new(0.10f, 0.28f, 0.45f), deep);
                }
                else
                {
                    c = Srgb(ColourAt(area, wx, z)) * shade;
                    if (style == Style.Parchment)
                    {
                        float lum = (c.X * 0.3f) + (c.Y * 0.59f) + (c.Z * 0.11f);
                        c = Vector3.Lerp(new Vector3(0.93f, 0.86f, 0.70f) * (0.55f + (0.6f * lum)), c, 0.25f);
                    }
                }
                // a dark line where the land meets the sea
                if (hgt >= sea && (area.GroundHeight(wx + step, z) < sea || area.GroundHeight(wx - step, z) < sea || area.GroundHeight(wx, z + step) < sea || area.GroundHeight(wx, z - step) < sea))
                {
                    c *= 0.45f;
                }
                pixels[(y * w) + x] = Pack(c);
            }
        });
        unsafe
        {
            fixed (uint* p = pixels)
            {
                Buffer.MemoryCopy(p, (void*)bmp.GetPixels(), pixels.Length * 4L, pixels.Length * 4L);
            }
        }
        bmp.NotifyPixelsChanged();
    }

    /// <summary>The ground's colour at a world point, between its four grid points (linear).</summary>
    private static Vector3 ColourAt(IAreaView area, float x, float z)
    {
        int x0 = (int)MathF.Floor(x), z0 = (int)MathF.Floor(z);
        float fx = x - x0, fz = z - z0;
        return Vector3.Lerp(Vector3.Lerp(area.GroundColor(x0, z0), area.GroundColor(x0 + 1, z0), fx), Vector3.Lerp(area.GroundColor(x0, z0 + 1), area.GroundColor(x0 + 1, z0 + 1), fx), fz);
    }

    private static Vector3 Srgb(Vector3 linear) => new(MathF.Pow(Math.Clamp(linear.X, 0, 1), 1 / 2.2f), MathF.Pow(Math.Clamp(linear.Y, 0, 1), 1 / 2.2f), MathF.Pow(Math.Clamp(linear.Z, 0, 1), 1 / 2.2f));

    /// <summary>A colour as a premultiplied, opaque RGBA pixel (red lowest).</summary>
    private static uint Pack(Vector3 c) => (uint)(Math.Clamp(c.X, 0, 1) * 255) | ((uint)(Math.Clamp(c.Y, 0, 1) * 255) << 8) | ((uint)(Math.Clamp(c.Z, 0, 1) * 255) << 16) | 0xFF000000u;

    /// <summary>The lakes (their outlines filled) and the rivers (as wide as they are, at least a line).</summary>
    private static void Water(SKCanvas g, Func<Vector2, Vector2> toPx, float px, bool old)
    {
        var colour = old ? new SKColor(196, 200, 182) : new SKColor(70, 140, 180);
        using var fill = new SKPaint { Color = colour, IsAntialias = true };
        using var edge = new SKPaint { Color = old ? new SKColor(90, 80, 60) : new SKColor(30, 70, 100), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(1, px * 0.6f) };
        foreach (var lake in MapWater.Lakes.Where(l => l.Outline.Count >= 3))
        {
            using var path = new SKPath();
            // the water file is Unreal metres: the editor's z = -Y
            var pts = lake.Outline.Select(p => toPx(new Vector2(p.X, -p.Y))).ToArray();
            path.MoveTo(pts[0].X, pts[0].Y);
            foreach (var p in pts.Skip(1))
            {
                path.LineTo(p.X, p.Y);
            }
            path.Close();
            g.DrawPath(path, fill);
            g.DrawPath(path, edge);
        }
        using var river = new SKPaint { Color = colour, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        foreach (var r in MapWater.Rivers.Where(r => r.Points.Count >= 2))
        {
            for (int i = 0; i + 1 < r.Points.Count; i++)
            {
                var (a, b) = (r.Points[i], r.Points[i + 1]);
                var pa = toPx(new Vector2(a.X, -a.Y));
                var pb = toPx(new Vector2(b.X, -b.Y));
                river.StrokeWidth = MathF.Max(1.5f, (a.HalfWidth + b.HalfWidth) * px);
                g.DrawLine(pa.X, pa.Y, pb.X, pb.Y, river);
            }
        }
    }

    /// <summary>The regions' outlines, dashed.</summary>
    private static void Regions(SKCanvas g, Func<Vector2, Vector2> toPx, float k, bool old)
    {
        using var line = new SKPaint { Color = old ? new SKColor(120, 40, 30, 200) : new SKColor(255, 240, 200, 220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f * k, PathEffect = SKPathEffect.CreateDash([10 * k, 6 * k], 0) };
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, old ? (byte)0 : (byte)110), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * k };
        foreach (var r in MapNames.Regions)
        {
            using var path = new SKPath();
            var pts = r.Points.Select(toPx).ToArray();
            path.MoveTo(pts[0].X, pts[0].Y);
            foreach (var p in pts.Skip(1))
            {
                path.LineTo(p.X, p.Y);
            }
            path.Close();
            g.DrawPath(path, shadow);
            g.DrawPath(path, line);
        }
    }

    /// <summary>The names: regions' over their middles, places' beside a dot; bigger ones first, none covering another.</summary>
    private static void Names(SKCanvas g, Func<Vector2, Vector2> toPx, float k, bool old, int w, int h)
    {
        var all = MapNames.Regions.Select(r => (Text: r.Name, Size: MapNames.Size.Region, At: MapNames.Middle(r)))
            .Concat(MapNames.Labels.Select(l => (l.Text, l.Size, At: new Vector2(l.X, l.Z)))).OrderBy(n => n.Size).ToList();
        // the north arrow's and the scale bar's corners stay clear
        var taken = new List<SKRect> { SKRect.Create(w - (64 * k), 0, 64 * k, 72 * k), SKRect.Create(0, h - (60 * k), w / 4f + (60 * k), 60 * k) };
        using var dot = new SKPaint { Color = old ? new SKColor(60, 35, 20) : new SKColor(30, 25, 20), IsAntialias = true };
        using var ring = new SKPaint { Color = new SKColor(255, 248, 230), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * k };
        foreach (var (text, size, at) in all)
        {
            using var pic = GlView.NamePicture(text, size, k, old);
            var p = toPx(at);
            bool beside = size != MapNames.Size.Region;
            // a place's name stands to the right of its dot
            if (p.X < 0 || p.Y < 0 || p.X > w || p.Y > h)
            {
                continue;
            }
            // kept inside the picture
            float x = Math.Clamp(beside ? p.X + (5 * k) : p.X - (pic.Width / 2f), 4 * k, MathF.Max(4 * k, w - pic.Width - (4 * k)));
            float y = Math.Clamp(p.Y - (pic.Height / 2f), 4 * k, MathF.Max(4 * k, h - pic.Height - (4 * k)));
            var box = SKRect.Create(x, y, pic.Width, pic.Height);
            if (taken.Any(t => t.IntersectsWith(box)))
            {
                continue;
            }
            taken.Add(box);
            if (beside)
            {
                float r = (size == MapNames.Size.Place ? 4f : 2.5f) * k;
                g.DrawCircle(p.X, p.Y, r, dot);
                g.DrawCircle(p.X, p.Y, r, ring);
            }
            g.DrawBitmap(pic, x, y);
        }
    }

    /// <summary>A scale bar (a round length near a fifth of the width) and a north arrow, and a frame.</summary>
    private static void Furniture(SKCanvas g, int w, int h, float px, float k, bool old)
    {
        var ink = old ? new SKColor(70, 45, 25) : new SKColor(255, 250, 240);
        using var stroke = new SKPaint { Color = ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * k };
        using var fill = new SKPaint { Color = ink, IsAntialias = true };
        using var back = new SKPaint { Color = old ? new SKColor(240, 228, 200, 200) : new SKColor(20, 20, 20, 140), IsAntialias = true };
        float want = w / 5f / px;
        float metres = new[] { 10f, 20, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000 }.LastOrDefault(m => m <= want, 10);
        float len = metres * px, x0 = 24 * k, y0 = h - (28 * k);
        using var font = new SKFont(SKTypeface.FromFamilyName("serif", SKFontStyle.Bold) ?? SKTypeface.Default, 15 * k);
        string label = metres >= 1000 ? $"{metres / 1000:0.#} km" : $"{metres:0} m";
        g.DrawRoundRect(SKRect.Create(x0 - (8 * k), y0 - (24 * k), len + (16 * k) + font.MeasureText(label) + (8 * k), 36 * k), 6 * k, 6 * k, back);
        g.DrawLine(x0, y0, x0 + len, y0, stroke);
        g.DrawLine(x0, y0 - (6 * k), x0, y0 + (2 * k), stroke);
        g.DrawLine(x0 + len, y0 - (6 * k), x0 + len, y0 + (2 * k), stroke);
        g.DrawText(label, x0 + len + (8 * k), y0 + (4 * k), SKTextAlign.Left, font, fill);
        // north
        float nx = w - (34 * k), ny = 40 * k;
        g.DrawCircle(nx, ny + (2 * k), 22 * k, back);
        using var arrow = new SKPath();
        arrow.MoveTo(nx, ny - (16 * k));
        arrow.LineTo(nx + (8 * k), ny + (10 * k));
        arrow.LineTo(nx, ny + (5 * k));
        arrow.LineTo(nx - (8 * k), ny + (10 * k));
        arrow.Close();
        g.DrawPath(arrow, fill);
        g.DrawText("N", nx, ny + (22 * k), SKTextAlign.Center, font, fill);
        using var frame = new SKPaint { Color = old ? new SKColor(80, 55, 30) : new SKColor(20, 18, 15), Style = SKPaintStyle.Stroke, StrokeWidth = 6 * k };
        g.DrawRect(SKRect.Create(0, 0, w, h), frame);
    }
}
