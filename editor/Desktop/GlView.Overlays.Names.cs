using System.Numerics;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace Groundwork.Desktop;

// The map's names (MapNames) over the view: each a picture of its text standing over its place, the small ones only up
// close, the bigger ones first where they would cover each other; and the regions' outlines on the ground.
public sealed partial class GlView
{
    /// <summary>The screen's size in pixels and its scaling, as the last frame drew it.</summary>
    private (int W, int H, float Scale) _screenPx = (1, 1, 1);

    /// <summary>Whether the map's names and regions are shown (View panel).</summary>
    internal bool ShowNames
    {
        get; set
        {
            field = value;
            Wake();
        }
    } = true;

    /// <summary>The farthest a place's name shows from, and a small place's (metres from the eye).</summary>
    internal const float PlaceNamesFrom = 2500, SmallNamesFrom = 400;

    /// <summary>Each name's picture on the graphics card: its texture and its size in pixels (unscaled).</summary>
    private readonly Dictionary<(string Text, MapNames.Size Size), (uint Tex, int W, int H)> _nameTex = [];

    private uint _textProg, _textVao, _textVbo, _regionVao, _regionVbo;

    /// <summary>The regions' outlines in view space, worked out once a scene and a change of the names.</summary>
    private (WorldScene S, int Version, float[] Lines)? _regionLines;

    /// <summary>Counts changes of the names, so the outlines are worked out again.</summary>
    private int _namesVersion;

    /// <summary>The names changed: their outlines and pictures are made again as needed.</summary>
    internal void NamesChanged()
    {
        _namesVersion++;
        Wake();
    }

    /// <summary>The names shown last frame: their text and where they stood on the picture (pixels), for tests.</summary>
    internal IReadOnlyList<(string Text, float X, float Y)> NamesShown { get; private set; } = [];

    /// <summary>The regions' outlines: lines along the ground a metre over it, every 2 m or so.</summary>
    internal static float[] RegionLines(WorldScene s)
    {
        var o = new List<float>();
        foreach (var r in MapNames.Regions)
        {
            for (int i = 0; i < r.Points.Count; i++)
            {
                Vector2 a = r.Points[i], b = r.Points[(i + 1) % r.Points.Count];
                int n = Math.Clamp((int)(Vector2.Distance(a, b) / 2), 1, 2000);
                var prev = OnGround(s, a.X - s.OriginX, a.Y - s.OriginZ, 1);
                for (int k = 1; k <= n; k++)
                {
                    var q = Vector2.Lerp(a, b, k / (float)n);
                    var p = OnGround(s, q.X - s.OriginX, q.Y - s.OriginZ, 1);
                    o.AddRange([prev.X, prev.Y, prev.Z, p.X, p.Y, p.Z]);
                    prev = p;
                }
            }
        }
        return [.. o];
    }

    /// <summary>The regions' outlines, in one colour (each region's colour would need a draw each; the names say which is which).</summary>
    private void DrawRegions(WorldScene s, Matrix4x4 vp)
    {
        if (!ShowNames || MapNames.Regions.Count == 0)
        {
            return;
        }
        if (_regionLines is not { } d || d.S != s || d.Version != _namesVersion)
        {
            _regionLines = d = (s, _namesVersion, RegionLines(s));
        }
        if (d.Lines.Length > 0)
        {
            var c = MapNames.Regions[0].Colour;
            DrawLines(ref _regionVao, ref _regionVbo, d.Lines, vp, new Vector4(c, 0.8f), blend: true, width: 1.2f, depth: true);
        }
    }

    /// <summary>
    /// Which names show and where (pixels of the picture, their middle), biggest first and nearest first: one that
    /// would cover a shown one is left out; small places' names only from SmallNamesFrom, places' from PlaceNamesFrom.
    /// </summary>
    internal static List<(string Text, MapNames.Size Size, float X, float Y, float Alpha)> NamesToShow(
        IEnumerable<(string Text, MapNames.Size Size, Vector3 World)> names, Func<Vector3, Vector4> clip, Vector3 eye, int w, int h, Func<string, MapNames.Size, (float W, float H)> size)
    {
        var all = new List<(string Text, MapNames.Size Size, float X, float Y, float Alpha, float D)>();
        foreach (var (text, sz, world) in names)
        {
            float d = Vector3.Distance(world, eye);
            float far = sz switch { MapNames.Size.Small => SmallNamesFrom, MapNames.Size.Place => PlaceNamesFrom, _ => float.MaxValue };
            if (d > far)
            {
                continue;
            }
            var q = clip(world);
            if (q.W <= 0)
            {
                continue;
            }
            float x = ((q.X / q.W) + 1) / 2 * w, y = (1 - (q.Y / q.W)) / 2 * h;
            if (x < -200 || x > w + 200 || y < -50 || y > h + 50)
            {
                continue;
            }
            // fades in over the last fifth of its distance
            float alpha = far == float.MaxValue ? 1 : Math.Clamp((far - d) / (far * 0.2f), 0, 1);
            all.Add((text, sz, x, y, alpha, d));
        }
        var shown = new List<(string, MapNames.Size, float, float, float)>();
        var boxes = new List<(float X0, float Y0, float X1, float Y1)>();
        foreach (var (text, sz, x, y, alpha, _) in all.OrderBy(n => n.Size).ThenBy(n => n.D))
        {
            var (bw, bh) = size(text, sz);
            var (x0, y0, x1, y1) = (x - (bw / 2), y - (bh / 2), x + (bw / 2), y + (bh / 2));
            if (boxes.Any(o => x0 < o.X1 && o.X0 < x1 && y0 < o.Y1 && o.Y0 < y1))
            {
                continue;
            }
            boxes.Add((x0, y0, x1, y1));
            shown.Add((text, sz, x, y, alpha));
        }
        return shown;
    }

    /// <summary>Every name and region name with the place it stands (world metres, on the ground and lifted a little).</summary>
    private static IEnumerable<(string Text, MapNames.Size Size, Vector3 World)> NamesInWorld(WorldScene s)
    {
        Vector3 At(float x, float z, float lift) => new(x, OnGround(s, x - s.OriginX, z - s.OriginZ, lift).Y, z);
        foreach (var l in MapNames.Labels)
        {
            yield return (l.Text, l.Size, At(l.X, l.Z, l.Size == MapNames.Size.Region ? 30 : 6));
        }
        foreach (var r in MapNames.Regions)
        {
            var m = MapNames.Middle(r);
            yield return (r.Name, MapNames.Size.Region, At(m.X, m.Y, 30));
        }
    }

    /// <summary>The names over the view, each the picture of its text, over everything else.</summary>
    private unsafe void DrawNames(WorldScene s, Matrix4x4 vp)
    {
        if (!ShowNames || MapNames.None)
        {
            NamesShown = [];
            return;
        }
        var (w, h, k) = _screenPx;
        var eye = new Vector3(_lastEye.X + s.Cx, _lastEye.Y, -_lastEye.Z + s.Cz);
        var shown = NamesToShow(NamesInWorld(s), p => Vector4.Transform(new Vector4(ToView(s, p), 1), vp), eye, w, h, (t, z) => NameTexture(t, z) is var (_, tw, th) ? (tw * k, th * k) : (0, 0));
        NamesShown = [.. shown.Select(n => (n.Text, n.X, n.Y))];
        if (shown.Count == 0)
        {
            return;
        }
        if (_textProg == 0)
        {
            _textProg = Program(Shaders.TextVs, Shaders.TextFs);
            _textVao = _gl.GenVertexArray();
            _textVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_textVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 16, (void*)8);
        }
        _gl.UseProgram(_textProg);
        _gl.Uniform1(_gl.GetUniformLocation(_textProg, "uTex"), 0);
        int uAlpha = _gl.GetUniformLocation(_textProg, "uAlpha");
        _gl.BindVertexArray(_textVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _textVbo);
        _gl.Disable(EnableCap.DepthTest);
        GlBlend.Over(_gl);
        _gl.ActiveTexture(TextureUnit.Texture0);
        foreach (var (text, size, x, y, alpha) in shown)
        {
            if (NameTexture(text, size) is not var (tex, tw, th))
            {
                continue;
            }
            // whole pixels, so the text stays sharp
            float x0 = MathF.Round(x - (tw * k / 2)), y0 = MathF.Round(y - (th * k / 2)), x1 = x0 + (tw * k), y1 = y0 + (th * k);
            float l = (x0 / w * 2) - 1, r = (x1 / w * 2) - 1, t = 1 - (y0 / h * 2), b = 1 - (y1 / h * 2);
            float[] quad = [l, t, 0, 0, r, t, 1, 0, r, b, 1, 1, l, t, 0, 0, r, b, 1, 1, l, b, 0, 1];
            fixed (float* p = quad)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * 4), p, BufferUsageARB.StreamDraw);
            }
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.Uniform1(uAlpha, alpha);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
        _gl.BindVertexArray(0);
    }

    /// <summary>A name's picture on the graphics card, made the first time it is drawn (null: no context).</summary>
    private unsafe (uint Tex, int W, int H)? NameTexture(string text, MapNames.Size size)
    {
        if (_nameTex.TryGetValue((text, size), out var got))
        {
            return got;
        }
        if (_gl == null)
        {
            return null;
        }
        // ponytail: one texture a name; pictures of names no longer on the map are dropped once there are 512
        if (_nameTex.Count >= 512)
        {
            foreach (var key in _nameTex.Keys.Where(key => !MapNames.Labels.Any(l => l.Text == key.Text) && !MapNames.Regions.Any(r => r.Name == key.Text)).ToList())
            {
                _gl.DeleteTexture(_nameTex[key].Tex);
                _ = _nameTex.Remove(key);
            }
        }
        using var bmp = NamePicture(text, size, _screenPx.Scale);
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)bmp.Width, (uint)bmp.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)bmp.GetPixels());
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        // its size in screen pixels at scale 1: the picture was made at the screen's scale
        var entry = (tex, (int)MathF.Ceiling(bmp.Width / _screenPx.Scale), (int)MathF.Ceiling(bmp.Height / _screenPx.Scale));
        _nameTex[(text, size)] = entry;
        return entry;
    }

    /// <summary>
    /// A name drawn as the map shows it: a serif face, light with a dark edge so it reads on any ground; a region's in
    /// spaced capitals. Made at <paramref name="scale"/> times its size in screen pixels; <paramref name="ink"/>: dark
    /// with a pale edge instead (on paper).
    /// </summary>
    internal static SKBitmap NamePicture(string text, MapNames.Size size, float scale, bool ink = false)
    {
        float px = size switch { MapNames.Size.Region => 26, MapNames.Size.Place => 17, _ => 13 } * scale;
        string shown = size == MapNames.Size.Region ? text.ToUpperInvariant() : text;
        float spacing = size == MapNames.Size.Region ? px * 0.28f : 0;
        using var face = SKTypeface.FromFamilyName("serif", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var font = new SKFont(face, px) { Edging = SKFontEdging.Antialias };
        float edge = MathF.Max(2, px * 0.16f);
        float textW = 0;
        foreach (char c in shown)
        {
            textW += font.MeasureText(c.ToString()) + spacing;
        }
        textW = MathF.Max(1, textW - spacing);
        int bw = (int)MathF.Ceiling(textW + (edge * 2) + 4), bh = (int)MathF.Ceiling((px * 1.35f) + (edge * 2));
        var bmp = new SKBitmap(new SKImageInfo(bw, bh, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var g = new SKCanvas(bmp);
        g.Clear(SKColors.Transparent);
        using var outline = new SKPaint { Color = ink ? new SKColor(245, 235, 210, 200) : new SKColor(25, 22, 18, 210), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = edge, StrokeJoin = SKStrokeJoin.Round };
        using var fill = new SKPaint { Color = ink ? new SKColor(75, 42, 22) : size == MapNames.Size.Region ? new SKColor(250, 236, 200) : new SKColor(255, 250, 240), IsAntialias = true };
        float baseline = edge + (px * 1.02f);
        foreach (var paint in new[] { outline, fill })
        {
            float x = edge + 2;
            foreach (char c in shown)
            {
                g.DrawText(c.ToString(), x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(c.ToString()) + spacing;
            }
        }
        return bmp;
    }
}
