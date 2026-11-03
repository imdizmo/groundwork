using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Cliff pieces: a map's kit of rock walls the generator sets along its steep ground, overlapping, each at a slope's
/// foot facing down it and reaching back into the hill, so that the slope shows as a face of rock (the landscape
/// cannot stand that steep, nor hang over). Each a few columns of rock side by side, overlapping: prisms of five to
/// seven flat sides, two to two and a half times as tall as wide, their corners and tops chipped off in flat facets,
/// their feet drawn in, the tallest to the piece's top (under the ground above) and the others stepping down under it,
/// the tall ones leaning back into the hill a few degrees (each shape its own way); some with blocks at the top,
/// pillars, low blocks or boulders at the foot; a row of columns behind them filling the gaps. All of it rock (what
/// grows on it is the map's to paint).
/// </summary>
public static class CliffPiece
{
    /// <summary>The catalog group the pieces are kinds of.</summary>
    public const string Category = "Cliffs";
    /// <summary>How many shapes a map has.</summary>
    public const int Variants = 6;
    /// <summary>A piece's width and height (its face) and depth back into the hill at scale 1, metres.</summary>
    public const float Width = 12, Height = 12, Depth = 18;

    /// <summary>Which shape a cliff model is (its name's last number), or -1.</summary>
    public static int VariantOf(string model) =>
        model.LastIndexOf('_') is var u and >= 0 && int.TryParse(model[(u + 1)..], out int v) && v is >= 0 and < Variants ? v : -1;

    /// <summary>A map's cliff models' names (one per shape), from its name.</summary>
    public static string[] Models(string map)
    {
        string safe = new([.. map.Where(char.IsAsciiLetterOrDigit)]);
        return [.. Enumerable.Range(0, Variants).Select(v => $"Cliff_{safe}_{v}")];
    }

    /// <summary>
    /// Writes a map's cliff pieces into its models folder (in its rock) and puts them in its catalog
    /// under <see cref="Category"/>.
    /// </summary>
    /// <returns>The pieces' kinds.</returns>
    public static List<MapTools.Kind> WriteFor(MapProject m)
    {
        string folder = GeneratedModels.Folder(m, "cliff pieces");
        var rock = GeneratedModels.Layer(m, "Rock", "Cliff", "Stone") ?? m.Layers[^1];
        string[] names = Models(m.Name);
        GeneratedModels.NoLinks(folder, names);
        for (int v = 0; v < Variants; v++)
        {
            Write(Path.Combine(folder, names[v] + ".glb"), v, GeneratedModels.Look(m, rock));
        }
        var kinds = names.Select((n, v) => new MapTools.Kind(n, $"Cliff {v + 1}", Category, 1, 1, null)).ToList();
        GeneratedModels.AddToCatalog(m, kinds);
        return kinds;
    }

    /// <summary>One shape as a .glb, in the rock's look.</summary>
    internal static void Write(string file, int variant, (string? Png, Vector3 Colour, float Size) rock) =>
        GeneratedModels.WriteGlb(file, [Mesh(variant, rock.Size > 0 ? rock.Size : 4)], [(rock.Png, rock.Colour)], ["Rock"]);

    /// <summary>
    /// A column of rock in a piece (x across, y up, z out of the face; metres at scale 1): a prism of a few flat sides
    /// standing from <see cref="Base"/>, its section <see cref="Taper"/> of its top's at its foot (widening or narrowing
    /// up it), cut by flat planes: its foot drawn in on a slant (to about <see cref="Foot"/> of its width, over
    /// <see cref="FootUp"/> of its height), chips off its corners, its top on a slant and chamfered in to about
    /// <see cref="Crown"/> of it; leaning <see cref="Tilt"/> (radians) towards <see cref="Way"/> (radians round from +x
    /// towards +z; NaN: its own way), its top tilting with it.
    /// </summary>
    internal readonly record struct Column(float X, float Z, float W, float D, float H, float Turn, int Corners, float Foot, float FootUp, float Crown, int Seed, float Base = 0, float Taper = 1, float Tilt = 0, float Way = float.NaN);

    /// <summary>A column as a solid: its flat faces, each a convex polygon.</summary>
    internal static List<Vector3[]> Solid(Column c)
    {
        var rng = new Random(c.Seed);
        float R(float a, float b) => a + ((b - a) * rng.NextSingle());
        int k = c.Corners;
        // its section: k corners round it, unevenly spaced and each its own way out, fitted to its width and depth
        var corner = Enumerable.Range(0, k).Select(i =>
        {
            float a = (i + R(-0.3f, 0.3f)) / k * MathF.Tau, r = R(0.85f, 1.15f);
            return new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }).ToArray();
        var lo = new Vector2(corner.Min(q => q.X), corner.Min(q => q.Y));
        var hi = new Vector2(corner.Max(q => q.X), corner.Max(q => q.Y));
        var size = new Vector2(c.W, c.D);
        float cos = MathF.Cos(c.Turn), sin = MathF.Sin(c.Turn);
        var section = Hull([.. corner.Select(q => (((q - lo) / (hi - lo)) - new Vector2(0.5f)) * size).Select(q => new Vector2((q.X * cos) - (q.Y * sin), (q.X * sin) + (q.Y * cos)))]);
        int n = section.Length;
        var faces = new List<Vector3[]>
        {
            section.Select(q => new Vector3(q.X * c.Taper, 0, q.Y * c.Taper)).ToArray(),
            section.Select(q => new Vector3(q.X, c.H, q.Y)).ToArray(),
        };
        for (int i = 0; i < n; i++)
        {
            var (a, b) = (section[i], section[(i + 1) % n]);
            faces.Add([new(a.X * c.Taper, 0, a.Y * c.Taper), new(b.X * c.Taper, 0, b.Y * c.Taper), new(b.X, c.H, b.Y), new(a.X, c.H, a.Y)]);
        }
        float Reach(Vector2 dir) => section.Max(q => Vector2.Dot(q, dir));
        Vector2 Way(float a) => new(MathF.Cos(a), MathF.Sin(a));
        // a cut along a line in the upright plane through the axis its way: from (out, y) to (out1, y1), what lies outside it gone
        void Slant(Vector2 dir, float out0, float y0, float out1, float y1)
        {
            var normal = Vector3.Normalize(new Vector3(dir.X * (y0 - y1), out1 - out0, dir.Y * (y0 - y1)));
            if ((normal.X * dir.X) + (normal.Z * dir.Y) < 0)
            {
                normal = -normal;   // facing out its way: what lies beyond the line gone
            }
            Cut(faces, normal, Vector3.Dot(normal, new Vector3(dir.X * out0, y0, dir.Y * out0)));
        }
        // its corners bevelled a little up their length
        foreach (var q in section)
        {
            var dir = Vector2.Normalize(q);
            Cut(faces, new Vector3(dir.X, 0, dir.Y), Reach(dir) * (1 - R(0.04f, 0.1f)));
        }
        // its foot drawn in: cut away under it on a slant, deep on one side, less on the other
        float footUp = c.FootUp * c.H, one = R(0, MathF.Tau);
        var d0 = Way(one);
        var d1 = Way(one + MathF.PI + R(-0.6f, 0.6f));
        float r0 = Reach(d0) * c.Taper, r1 = Reach(d1) * c.Taper;
        Slant(d0, r0 * ((2 * c.Foot) - 1), 0, r0, footUp);
        Slant(d1, r1 * R(0.6f, 0.9f), 0, r1, footUp * R(0.4f, 0.8f));
        // chips off its corners: flat cuts a little in from a corner, upright anywhere up it, slanting up near its top and
        // down near its foot (or the slant would run in across it and take its top or foot away)
        for (int m = 2 + rng.Next(3) + (int)(MathF.Max(c.W, c.H) / 4); m > 0; m--)
        {
            var dir = Vector2.Normalize(section[rng.Next(n)] + (Way(R(0, MathF.Tau)) * 0.3f * Reach(Way(0))));
            float lean = R(-0.5f, 0.6f), y = (lean > 0.15f ? R(0.8f, 1) : lean < -0.15f ? R(0, 0.25f) : R(0.1f, 1)) * c.H, t = c.Taper + ((1 - c.Taper) * y / c.H);
            var normal = Vector3.Normalize(new Vector3(dir.X, lean, dir.Y));
            Cut(faces, normal, Vector3.Dot(normal, new Vector3(dir.X, 0, dir.Y) * (Reach(dir) * t * R(0.78f, 0.93f))) + (normal.Y * y));
        }
        // its top: on a slant (lower its one way), chamfered in at some corners (all of them on the rounder ones)
        var slant = Way(R(0, MathF.Tau)) * R(0.05f, 0.3f);
        var up = Vector3.Normalize(new Vector3(slant.X, 1, slant.Y));
        Cut(faces, up, up.Y * c.H);
        foreach (var q in section.Where(_ => c.Crown < 0.7f || rng.NextSingle() < 0.45f))
        {
            var dir = Vector2.Normalize(q);
            float reach = Reach(dir);
            Slant(dir, reach * c.Crown, c.H, reach, c.H - (reach * (1 - c.Crown) * R(0.6f, 1.3f)));
        }
        // its lean, and where it stands
        float way = R(0, MathF.Tau), tan = MathF.Tan(c.Tilt);
        way = float.IsNaN(c.Way) ? way : c.Way;
        var tilt = Way(way) * tan;
        return [.. faces.Select(f => f.Select(p => new Vector3(p.X + (tilt.X * p.Y) + c.X, p.Y - (tilt.X * p.X) - (tilt.Y * p.Z) + c.Base, p.Z + (tilt.Y * p.Y) + c.Z)).ToArray())];
    }

    /// <summary>A convex solid's faces cut by a plane: what lies past it (n·p > d) gone, the cut closed by a new face.</summary>
    private static void Cut(List<Vector3[]> faces, Vector3 n, float d)
    {
        var cap = new List<Vector3>();
        for (int f = faces.Count - 1; f >= 0; f--)
        {
            var poly = faces[f];
            var kept = new List<Vector3>();
            for (int i = 0; i < poly.Length; i++)
            {
                var (a, b) = (poly[i], poly[(i + 1) % poly.Length]);
                float da = Vector3.Dot(n, a) - d, db = Vector3.Dot(n, b) - d;
                if (da <= 0)
                {
                    kept.Add(a);
                }
                if ((da < 0 && db > 0) || (da > 0 && db < 0))
                {
                    var x = Vector3.Lerp(a, b, da / (da - db));
                    kept.Add(x);
                    cap.Add(x);
                }
            }
            if (kept.Count >= 3)
            {
                faces[f] = [.. kept];
            }
            else
            {
                faces.RemoveAt(f);
            }
        }
        // the new face: its corners in order round its middle
        var mid = cap.Count > 0 ? cap.Aggregate(Vector3.Zero, (s, p) => s + p) / cap.Count : Vector3.Zero;
        var u = Vector3.Normalize(Vector3.Cross(n, MathF.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
        var v = Vector3.Cross(n, u);
        var ring = cap.OrderBy(p => MathF.Atan2(Vector3.Dot(p - mid, v), Vector3.Dot(p - mid, u))).ToList();
        ring = [.. ring.Where((p, i) => Vector3.DistanceSquared(p, ring[(i + 1) % ring.Count]) > 1e-8f)];
        if (ring.Count >= 3)
        {
            faces.Add([.. ring]);
        }
    }

    /// <summary>The convex hull of some points, counter-clockwise.</summary>
    private static Vector2[] Hull(Vector2[] points)
    {
        var p = points.OrderBy(q => q.X).ThenBy(q => q.Y).ToArray();
        static float Turn(Vector2 o, Vector2 a, Vector2 b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
        var h = new List<Vector2>();
        foreach (var pass in new[] { p, [.. p.Reverse()] })
        {
            int start = h.Count;
            foreach (var q in pass)
            {
                while (h.Count >= start + 2 && Turn(h[^2], h[^1], q) <= 0)
                {
                    h.RemoveAt(h.Count - 1);
                }
                h.Add(q);
            }
            h.RemoveAt(h.Count - 1);
        }
        return [.. h];
    }

    /// <summary>A column's sections every <paramref name="step"/> up it, from its lowest point to its highest: each a height and its outline (counter-clockwise).</summary>
    internal static (float Y, Vector2[] P)[] Sections(Column c, float step = 0.25f)
    {
        var faces = Solid(c);
        float y0 = faces.Min(f => f.Min(p => p.Y)), y1 = faces.Max(f => f.Max(p => p.Y));
        var list = new List<(float, Vector2[])>();
        for (float y = y0 + 0.01f; y < y1; y += step)
        {
            var at = new List<Vector2>();
            foreach (var f in faces)
            {
                for (int i = 0; i < f.Length; i++)
                {
                    var (a, b) = (f[i], f[(i + 1) % f.Length]);
                    if ((a.Y - y) * (b.Y - y) < 0)
                    {
                        var x = Vector3.Lerp(a, b, (y - a.Y) / (b.Y - a.Y));
                        at.Add(new Vector2(x.X, x.Z));
                    }
                }
            }
            if (at.Count >= 3)
            {
                list.Add((y, Hull([.. at])));
            }
        }
        return [.. list];
    }

    /// <summary>A column's section nearest a height, or null outside it.</summary>
    private static Vector2[]? SectionAt((float Y, Vector2[] P)[] sections, float y)
    {
        if (sections.Length == 0 || y < sections[0].Y - 0.13f || y > sections[^1].Y + 0.13f)
        {
            return null;
        }
        int j = Math.Clamp((int)MathF.Round((y - sections[0].Y) / 0.25f), 0, sections.Length - 1);
        return sections[j].P;
    }

    /// <summary>How far a section reaches out of the face (+z) along the line across it at x, or null when it misses it.</summary>
    private static float? FrontAt(Vector2[] section, float x)
    {
        float? best = null;
        for (int i = 0; i < section.Length; i++)
        {
            var (a, b) = (section[i], section[(i + 1) % section.Length]);
            if ((a.X <= x && b.X >= x) || (b.X <= x && a.X >= x))
            {
                float z = MathF.Abs(b.X - a.X) < 1e-5f ? MathF.Max(a.Y, b.Y) : a.Y + ((b.Y - a.Y) * (x - a.X) / (b.X - a.X));
                best = MathF.Max(best ?? z, z);
            }
        }
        return best;
    }

    /// <summary>Where a piece's face is taken to be where no column stands (the row behind fills it), z.</summary>
    private const float Core = -2;

    /// <summary>
    /// A shape's columns. All stand side by side across the face, overlapping, each its own width (two to two and a half
    /// times as tall as wide), standing out its own way, the tallest to the piece's top and the others a little under it;
    /// some shapes with blocks on a lower wall at the top, short pillars or low blocks and boulders in front at the foot;
    /// behind them all a row of columns a little under the top, filling the gaps between them.
    /// </summary>
    internal static List<Column> Columns(int variant)
    {
        var rng = new Random(7411 + (variant * 6007));
        float R(float a, float b) => a + ((b - a) * rng.NextSingle());
        int seed = 1000 * (variant + 1);
        var list = new List<Column>();
        // the wall leaning back into the hill (its own way: most a few degrees back, some upright, now and then a little
        // forward), its tall columns with it; the others leaning their own ways
        float back = (variant % Variants == 3 ? R(-3, 2) : R(2, 10)) * MathF.PI / 180;
        Column Make(float x, float z, float w, float d, float h, float foot, float footUp, float crown, float b = 0, float taper = 1) =>
            h > 2.2f * w
                ? new(x, z, w, d, h, R(-0.6f, 0.6f), 5 + rng.Next(3), foot, footUp, crown, seed++, b, taper, back * R(0.6f, 1.4f), (-MathF.PI / 2) + R(-0.7f, 0.7f))
                : new(x, z, w, d, h, R(-0.6f, 0.6f), 5 + rng.Next(3), foot, footUp, crown, seed++, b, taper, R(0, 14) * MathF.PI / 180);
        // the wall: from a little past one side to a little past the other
        float WallTo(float top, float low)
        {
            // (the lower ones standing further out: ledges stepping up to the top; some of them broken across)
            float x = (-Width / 2) + R(0, 0.8f), tallest = R(-Width / 3, Width / 3), highest = 0;
            while (x < Width / 2)
            {
                bool tall = MathF.Abs(x - tallest) < 2.5f;
                float h = tall ? top : top - R(low * 0.2f, low * 1.6f);
                float w = MathF.Min(h / R(1.9f, 2.4f), R(4, 6.5f)), d = w * R(0.5f, 0.7f);
                float cx = x + (w * 0.4f), cz = R(-1.4f, 2) - (d * 0.2f) + ((top - h) / top * R(4, 9));
                if (!tall && h > 6 && rng.NextSingle() < 0.6f)
                {
                    // broken across: a block standing on a shorter column (set a little back or out on it), both leaning
                    // back with the wall
                    float under = h * R(0.4f, 0.65f), footUp = R(0.15f, 0.25f) * h;
                    list.Add(new(cx, cz, w, d, under, R(-0.6f, 0.6f), 5 + rng.Next(3), R(0.35f, 0.5f), MathF.Min(0.5f, footUp / under), R(0.85f, 0.92f), seed++, 0, R(0.92f, 0.98f), back, -MathF.PI / 2));
                    list.Add(new(cx + R(-0.4f, 0.4f), cz - R(-0.3f, 0.8f) - (MathF.Tan(back) * under), w * R(0.85f, 1.05f), d * R(0.85f, 1.05f), h - under + 0.15f, R(-0.6f, 0.6f), 5 + rng.Next(3), R(0.8f, 0.95f), 0.1f, R(0.8f, 0.88f), seed++, under - 0.15f, R(0.95f, 1), back, -MathF.PI / 2));
                }
                else
                {
                    list.Add(Make(cx, cz, w, d, h, R(0.35f, 0.5f), R(0.15f, 0.25f), R(0.8f, 0.88f), 0, R(0.92f, 0.98f)));
                }
                highest = MathF.Max(highest, h);
                x += w * R(0.7f, 0.9f);
            }
            return highest;
        }
        // the row behind: wide columns, from side to side, upright from the foot to under the top (in the ground
        // there), the wall leaning back into it
        for (float x = (-Width / 2) - 0.5f; x < Width / 2;)
        {
            float w = R(5.8f, 7.5f), d = w * R(0.5f, 0.65f);
            list.Add(new(x + (w * 0.4f), (-d * 0.3f) - R(0.5f, 1), w, d, Height - R(0.8f, 2), R(-0.15f, 0.15f), 5 + rng.Next(3), R(0.85f, 0.95f), 0.1f, 0.9f, seed++, 0, 1));
            x += w * R(0.4f, 0.55f);
        }
        switch (variant % Variants)
        {
            case 0:
                WallTo(Height, 2.5f);
                break;
            case 1:   // short pillars in front
                WallTo(Height, 2);
                for (int n = 1 + rng.Next(2); n > 0; n--)
                {
                    float h = R(4, 7), w = h / R(2.4f, 3.2f);
                    list.Add(Make(R(-4, 4), R(1.6f, 2.4f), w, w * R(0.8f, 0.92f), h, R(0.2f, 0.35f), R(0.25f, 0.35f), R(0.9f, 0.95f), 0, R(0.82f, 0.92f)));
                }
                break;
            case 2:   // a lower wall with blocks on it at the top
            case 5:
            {
                float wall = WallTo(Height - R(3, 4.5f), 1.5f);
                for (float x = (-Width / 2) + R(0.5f, 1.5f); x < Width / 2;)
                {
                    // set back flush with the wall's front, its drawn-in foot sunk in the wall's top
                    float w = R(4, 6.5f), d = w * R(0.65f, 0.9f), h = Height - wall + 2.5f - R(0, x < 0 ? 0 : 1.2f);
                    list.Add(Make(x + (w * 0.4f), R(0, 0.9f) - (d / 2), w, d, h, R(0.35f, 0.55f), R(0.25f, 0.35f), R(0.8f, 0.92f), wall - 2.5f, R(0.7f, 0.8f)));
                    x += w * R(0.7f, 0.9f);
                }
                if (variant % Variants == 5)
                {
                    Boulders(list, Make, R, 3 + rng.Next(3));
                }
                break;
            }
            case 3:   // stepping down across
            {
                float x = (-Width / 2) + R(0, 0.6f), h = Height;
                while (x < Width / 2)
                {
                    float w = MathF.Min(h / R(2, 2.6f), R(3.5f, 5.5f)), d = w * R(0.5f, 0.7f);
                    list.Add(Make(x + (w * 0.4f), R(-0.4f, 0.8f), w, d, h, R(0.35f, 0.5f), R(0.15f, 0.25f), R(0.8f, 0.88f), 0, R(0.92f, 0.98f)));
                    x += w * R(0.65f, 0.85f);
                    h = MathF.Max(5, h - R(0.8f, 2.2f));
                }
                break;
            }
            default:   // low blocks and boulders at the foot
                WallTo(Height, 2.5f);
                for (float x = (-Width / 2) + R(0, 2); x < Width / 2; x += R(2.5f, 4.5f))
                {
                    float w = R(2, 4);
                    list.Add(Make(x, R(1.8f, 2.8f), w, w * R(0.7f, 0.85f), w * R(0.55f, 0.75f), 1, 0.05f, R(0.75f, 0.85f), 0, R(1.4f, 1.7f)));
                }
                Boulders(list, Make, R, 2 + rng.Next(3));
                break;
        }
        return list;
    }

    private static void Boulders(List<Column> list, Func<float, float, float, float, float, float, float, float, float, float, Column> make, Func<float, float, float> r, int n)
    {
        for (; n > 0; n--)
        {
            float w = r(0.8f, 1.8f);
            list.Add(make(r(-5, 5), r(2.5f, 4.5f), w, w * r(0.85f, 0.95f), w * r(0.9f, 1.2f), 0.85f, 0.1f, r(0.5f, 0.6f), 0, r(1.8f, 2.2f)));
        }
    }

    /// <summary>The top of a shape along it: its highest column over x, metres at scale 1 (the core's top between columns).</summary>
    internal static Func<float, float> Top(int variant)
    {
        var sections = Columns(variant).Select(c => Sections(c)).Where(s => s.Length > 0).ToArray();
        return x => sections.Where(r => r[r.Length / 2].P is var s && s.Min(p => p.X) <= x && s.Max(p => p.X) >= x)
            .Select(r => r[^1].Y).DefaultIfEmpty(Height - 1.5f).Max();
    }

    /// <summary>How far the face stands out of its plane (z = 0, facing +z) at a point of it: its columns' fronts, metres at scale 1.</summary>
    internal static Func<float, float, float> Face(int variant)
    {
        var sections = Columns(variant).Select(c => Sections(c)).ToArray();
        return (x, y) =>
        {
            float z = Core;
            foreach (var r in sections)
            {
                if (SectionAt(r, y) is { } s && FrontAt(s, x) is { } f)
                {
                    z = MathF.Max(z, f);
                }
            }
            return z;
        };
    }

    /// <summary>One shape's mesh (corners: position, normal, uv; triangles).</summary>
    internal static (List<float> V, List<int> I) Mesh(int variant, float rockSize)
    {
        var rock = (V: new List<float>(), I: new List<int>());
        foreach (var c in Columns(variant))
        {
            ColumnMesh(c, rock, rockSize);
        }
        return rock;
    }

    /// <summary>The role samples the kit's shapes are made of, one each, alone (for comparing them with other kits').</summary>
    internal static IEnumerable<(string Name, (List<float> V, List<int> I) Mesh)> RoleSamples(float rockSize)
    {
        (string, Column)[] samples =
        [
            ("role_column", new(0, 0, 5, 3, 11.5f, 0.2f, 6, 0.42f, 0.2f, 0.84f, 11, 0, 0.95f)),
            ("role_block", new(0, 0, 6, 4.8f, 5.5f, 0.1f, 7, 0.45f, 0.3f, 0.86f, 12, 0, 0.75f, 0.2f)),
            ("role_pillar", new(0, 0, 2.2f, 1.9f, 5.8f, 0.3f, 6, 0.28f, 0.3f, 0.84f, 13, 0, 0.88f)),
            ("role_foot", new(0, 0, 3.2f, 2.5f, 2.3f, 0.1f, 7, 1, 0.05f, 0.8f, 14, 0, 1.6f)),
            ("role_boulder", new(0, 0, 1.4f, 1.3f, 1.5f, 0.4f, 6, 0.85f, 0.1f, 0.55f, 15, 0, 2)),
        ];
        foreach (var (name, c) in samples)
        {
            var rock = (V: new List<float>(), I: new List<int>());
            ColumnMesh(c, rock, rockSize);
            yield return (name, rock);
        }
    }

    /// <summary>A column's flat faces (an upward one's texture laid from above, the others' from the side).</summary>
    private static void ColumnMesh(Column c, (List<float> V, List<int> I) rock, float rockSize)
    {
        var faces = Solid(c);
        var middle = faces.SelectMany(f => f).Aggregate(Vector3.Zero, (s, p) => s + p) / faces.Sum(f => f.Length);
        foreach (var f in faces)
        {
            var centre = f.Aggregate(Vector3.Zero, (s, p) => s + p) / f.Length;
            var n = Enumerable.Range(0, f.Length).Aggregate(Vector3.Zero, (s, i) => s + Vector3.Cross(f[i], f[(i + 1) % f.Length]));   // Newell's: sound however its corners lie
            if (n.LengthSquared() < 1e-12f)
            {
                continue;
            }
            n = Vector3.Normalize(n);
            n = Vector3.Dot(n, centre - middle) < 0 ? -n : n;
            var side = MathF.Abs(n.Y) > 0.6f ? Vector3.Zero : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n));
            Flat(rock, f, n, p => (side == Vector3.Zero ? new Vector2(p.X, p.Z) : new Vector2(Vector3.Dot(p, side), p.Y)) / rockSize);
        }
    }

    private static void Corner(List<float> v, Vector3 p, Vector3 n, Vector2 uv) => v.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, uv.X, uv.Y]);

    /// <summary>A flat polygon (a fan from its first corner) facing <paramref name="n"/>.</summary>
    private static void Flat((List<float> V, List<int> I) part, Vector3[] p, Vector3 n, Func<Vector3, Vector2> uv)
    {
        int first = part.V.Count / 8;
        foreach (var q in p)
        {
            Corner(part.V, q, n, uv(q));
        }
        for (int i = 1; i + 1 < p.Length; i++)
        {
            Facing(part.I, part.V, first, first + i, first + i + 1, n);
        }
    }

    /// <summary>A triangle turned to face <paramref name="side"/> (counter-clockwise from there, as glTF draws its front).</summary>
    private static void Facing(List<int> idx, List<float> v, int a, int b, int c, Vector3 side)
    {
        Vector3 P(int i) => new(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]);
        bool turned = Vector3.Dot(Vector3.Cross(P(b) - P(a), P(c) - P(a)), side) < 0;
        idx.AddRange(turned ? [a, c, b] : [a, b, c]);
    }
}
