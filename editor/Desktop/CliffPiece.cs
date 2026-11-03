using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Cliff pieces: a map's kit of rock walls the generator sets along its steep ground, overlapping, each at a slope's
/// foot facing down it and reaching back into the hill, so that the slope shows as a face of rock (the landscape
/// cannot stand that steep, nor hang over). Each a few columns of rock side by side, overlapping: prisms of five to
/// seven flat fluted sides, two to two and a half times as tall as wide, their feet drawn in, their tops cut flat with
/// grass on them, the tallest to the piece's top (under the ground above) and the others stepping down under it, the
/// tall ones leaning back into the hill a few degrees (each shape its own way); some
/// with blocks at the top, pillars, low blocks or boulders at the foot; a core behind them back into the hill.
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
    /// Writes a map's cliff pieces into its models folder (its rock, its first layer on top) and puts them in its catalog
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
            Write(Path.Combine(folder, names[v] + ".glb"), v, GeneratedModels.Look(m, rock), GeneratedModels.Look(m, m.Layers[0]));
        }
        var kinds = names.Select((n, v) => new MapTools.Kind(n, $"Cliff {v + 1}", Category, 1, 1, null)).ToList();
        GeneratedModels.AddToCatalog(m, kinds);
        return kinds;
    }

    /// <summary>One shape as a .glb: its rock, and its top of the ground's look.</summary>
    internal static void Write(string file, int variant, (string? Png, Vector3 Colour, float Size) rock, (string? Png, Vector3 Colour, float Size) top) =>
        GeneratedModels.WriteGlb(file, Mesh(variant, rock.Size > 0 ? rock.Size : 4, top.Size > 0 ? top.Size : 4), [(rock.Png, rock.Colour), (top.Png, top.Colour)], ["Rock", "Top"]);

    /// <summary>
    /// A column of rock in a piece (x across, y up, z out of the face; metres at scale 1): a prism of a few flat sides,
    /// each side creased up it now and then, standing from <see cref="Base"/>, its section <see cref="Foot"/> of its middle's at its
    /// foot (drawn in over <see cref="FootUp"/> of its height), <see cref="Taper"/> of its top's above the foot (widening
    /// or narrowing evenly up it) and <see cref="Crown"/> of it at its top, cut flat there; leaning <see cref="Tilt"/>
    /// (radians) towards <see cref="Way"/> (radians round from +x towards +z; NaN: its own way), its top tilting with it.
    /// </summary>
    internal readonly record struct Column(float X, float Z, float W, float D, float H, float Turn, int Corners, float Foot, float FootUp, float Crown, int Seed, float Base = 0, float Taper = 1, float Tilt = 0, float Way = float.NaN);

    /// <summary>Its sections up it, from its foot to its top: each a height and the corners round it (all alike in number).</summary>
    internal static (float Y, Vector2[] P, float[] Dy)[] Rings(Column c)
    {
        var rng = new Random(c.Seed);
        float R(float a, float b) => a + ((b - a) * rng.NextSingle());
        int k = c.Corners;
        // its section: k corners round it, unevenly spaced and each its own way out, fitted to its width and depth
        var corner = Enumerable.Range(0, k).Select(i =>
        {
            float a = (i + R(-0.4f, 0.4f)) / k * MathF.Tau, r = R(0.8f, 1.2f);
            return new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }).ToArray();
        var lo = new Vector2(corner.Min(q => q.X), corner.Min(q => q.Y));
        var hi = new Vector2(corner.Max(q => q.X), corner.Max(q => q.Y));
        var size = new Vector2(c.W, c.D);
        corner = [.. corner.Select(q => (((q - lo) / (hi - lo)) - new Vector2(0.5f)) * size)];
        // about half the sides creased up the middle (the crease in or out a little): joints up the column
        float crease = 0.07f * MathF.Min(c.W, c.D);
        var flat = new List<Vector2>();
        for (int i = 0; i < k; i++)
        {
            var (a, b) = (corner[i], corner[(i + 1) % k]);
            flat.Add(a);
            var outward = Vector2.Normalize(new Vector2(b.Y - a.Y, a.X - b.X));
            if (Vector2.Dot(outward, (a + b) / 2) < 0)
            {
                outward = -outward;
            }
            if (rng.NextSingle() < 0.5f)
            {
                flat.Add(Vector2.Lerp(a, b, R(0.3f, 0.7f)) + (outward * crease * (rng.NextSingle() < 0.6f ? -1 : 0.5f)));
            }
            else
            {
                flat.Add((a + b) / 2);   // every section the same number of corners
            }
        }
        float cos = MathF.Cos(c.Turn), sin = MathF.Sin(c.Turn);
        var section = flat.Select(q => new Vector2((q.X * cos) - (q.Y * sin), (q.X * sin) + (q.Y * cos))).ToArray();
        // up it: its foot drawn in, a few sections between each a little wider or narrower and leaning, its top edge
        // bevelled to its crown
        float bevel = MathF.Min(0.6f, 0.1f * MathF.Min(c.W, c.D)), footUp = c.FootUp * c.H;
        var up = new List<(float Y, float S)> { (0, c.Foot * c.Taper), (footUp * 0.45f, (c.Foot + ((1 - c.Foot) * 0.7f)) * c.Taper), (footUp, c.Taper) };
        int mids = 2 + rng.Next(3);
        for (int m = 1; m <= mids; m++)
        {
            float t = (m + R(-0.3f, 0.3f)) / (mids + 1);
            up.Add((footUp + ((c.H - bevel - footUp) * t), (c.Taper + ((1 - c.Taper) * t)) * R(0.97f, 1.03f)));
        }
        up.AddRange([(c.H - bevel, 1), (c.H, c.Crown)]);
        up.Sort((a, b) => a.Y.CompareTo(b.Y));
        // the foot drawn in towards a point near one side (one side straight down, the other cut away on a slant)
        var anchor = section[rng.Next(section.Length)] * 0.6f;
        // its lean: each section moved its way the higher it is, and tilted with it (a corner its way lower)
        float way = R(0, MathF.Tau), tan = MathF.Tan(c.Tilt);
        way = float.IsNaN(c.Way) ? way : c.Way;
        var tilt = new Vector2(MathF.Cos(way), MathF.Sin(way)) * tan;
        var lean = Vector2.Zero;
        var rings = new List<(float, Vector2[], float[])>();
        foreach (var (y, s) in up)
        {
            lean += new Vector2(R(-1, 1), R(-1, 1)) * 0.02f * c.W;
            var shift = lean + (tilt * y) + new Vector2(c.X, c.Z);
            var about = y < footUp ? anchor : Vector2.Zero;
            var ring = section.Select(q => about + ((q - about) * s * R(0.985f, 1.015f))).ToArray();
            rings.Add((c.Base + y, [.. ring.Select(q => q + shift)], [.. ring.Select(q => -Vector2.Dot(q, tilt))]));
        }
        return [.. rings];
    }

    /// <summary>A section of a column at a height (between two of its rings), or null outside it.</summary>
    private static Vector2[]? SectionAt((float Y, Vector2[] P, float[] Dy)[] rings, float y)
    {
        if (y < rings[0].Y || y > rings[^1].Y)
        {
            return null;
        }
        int j = Math.Clamp(Array.FindLastIndex(rings, r => r.Y <= y), 0, rings.Length - 2);
        float t = (y - rings[j].Y) / MathF.Max(1e-4f, rings[j + 1].Y - rings[j].Y);
        return [.. rings[j].P.Select((p, i) => Vector2.Lerp(p, rings[j + 1].P[i], t))];
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

    /// <summary>The front of the core behind a piece's columns (a block back into the hill, filling the gaps), z.</summary>
    private const float Core = -2;

    /// <summary>
    /// A shape's columns. All stand side by side across the face, overlapping, each its own width (two to two and a half
    /// times as tall as wide), standing out its own way, the tallest to the piece's top and the others a little under it
    /// (grass on their tops); some shapes with blocks on a lower wall at the top, short pillars or low blocks and
    /// boulders in front at the foot.
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
            // (the lower ones standing further out: ledges with grass on them, stepping up to the top)
            float x = (-Width / 2) + R(0, 0.8f), tallest = R(-Width / 3, Width / 3), highest = 0;
            while (x < Width / 2)
            {
                bool tall = MathF.Abs(x - tallest) < 2.5f;
                float h = tall ? top : top - R(low * 0.2f, low * 1.6f);
                float w = MathF.Min(h / R(2, 2.6f), R(1.8f, 6)), d = w * R(0.5f, 0.7f);
                list.Add(Make(x + (w * 0.4f), R(-1.2f, 1.5f) - (d * 0.2f) + ((top - h) / top * R(4, 9)), w, d, h, R(0.35f, 0.5f), R(0.15f, 0.25f), R(0.8f, 0.88f), 0, R(0.92f, 0.98f)));
                highest = MathF.Max(highest, h);
                x += w * R(0.45f, 0.8f);
            }
            return highest;
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
        var rings = Columns(variant).Select(Rings).ToArray();
        return x => rings.Where(r => SectionAt(r, (r[0].Y + r[^1].Y) / 2) is { } s && s.Min(p => p.X) <= x && s.Max(p => p.X) >= x)
            .Select(r => r[^1].Y).DefaultIfEmpty(Height - 1.5f).Max();
    }

    /// <summary>How far the face stands out of its plane (z = 0, facing +z) at a point of it: its columns' fronts, metres at scale 1.</summary>
    internal static Func<float, float, float> Face(int variant)
    {
        var rings = Columns(variant).Select(Rings).ToArray();
        return (x, y) =>
        {
            float z = Core;
            foreach (var r in rings)
            {
                if (SectionAt(r, y) is { } s && FrontAt(s, x) is { } f)
                {
                    z = MathF.Max(z, f);
                }
            }
            return z;
        };
    }

    /// <summary>One shape's mesh: its rock and its top (grass on what faces up) (corners: position, normal, uv; triangles).</summary>
    internal static (List<float> V, List<int> I)[] Mesh(int variant, float rockSize, float topSize)
    {
        var rock = (V: new List<float>(), I: new List<int>());
        var top = (V: new List<float>(), I: new List<int>());
        foreach (var c in Columns(variant))
        {
            ColumnMesh(c, rock, top, rockSize, topSize);
        }
        // the core: a block behind the columns back into the hill, under the top, filling the gaps between them
        float hw = (Width / 2) - 0.3f, ch = Height - 1.5f;
        Vector3[] box = [new(-hw, 0, Core), new(hw, 0, Core), new(hw, ch, Core), new(-hw, ch, Core), new(-hw, 0, -Depth), new(hw, 0, -Depth), new(hw, ch, -Depth), new(-hw, ch, -Depth)];
        foreach (var (q, n) in new[] { ((0, 1, 2, 3), Vector3.UnitZ), ((5, 4, 7, 6), -Vector3.UnitZ), ((4, 0, 3, 7), -Vector3.UnitX), ((1, 5, 6, 2), Vector3.UnitX), ((3, 2, 6, 7), Vector3.UnitY) })
        {
            Flat(rock, [box[q.Item1], box[q.Item2], box[q.Item3], box[q.Item4]], n, p => new Vector2(p.X + p.Z, p.Y) / rockSize);
        }
        return [rock, top];
    }

    /// <summary>The role samples the kit's shapes are made of, one each, alone (for comparing them with other kits').</summary>
    internal static IEnumerable<(string Name, (List<float> V, List<int> I)[] Parts)> RoleSamples(float rockSize, float topSize)
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
            var top = (V: new List<float>(), I: new List<int>());
            ColumnMesh(c, rock, top, rockSize, topSize);
            yield return (name, [rock, top]);
        }
    }

    /// <summary>A column's flat sides (into the rock, or the top where one faces up), its foot and its top (grass, rolling a little over its edge).</summary>
    private static void ColumnMesh(Column c, (List<float> V, List<int> I) rock, (List<float> V, List<int> I) top, float rockSize, float topSize)
    {
        var rings = Rings(c);
        int n = rings[0].P.Length;
        var centre = new Vector2(c.X, c.Z);
        float[] along = new float[n + 1];
        for (int i = 0; i < n; i++)
        {
            along[i + 1] = along[i] + Vector2.Distance(rings[0].P[i], rings[0].P[(i + 1) % n]);
        }
        Vector3 At(int j, int i) => new(rings[j].P[i % n].X, rings[j].Y + rings[j].Dy[i % n], rings[j].P[i % n].Y);
        for (int j = 0; j + 1 < rings.Length; j++)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3[] q = [At(j, i), At(j, i + 1), At(j + 1, i + 1), At(j + 1, i)];
                var mid = (q[0] + q[2]) / 2;
                var outward = new Vector3(mid.X - centre.X, 0, mid.Z - centre.Y);
                Vector2[] uv = [new(along[i], q[0].Y), new(along[i + 1], q[1].Y), new(along[i + 1], q[2].Y), new(along[i], q[3].Y)];
                foreach (var (a, b, d) in new[] { (0, 1, 2), (0, 2, 3) })
                {
                    var nrm = Vector3.Normalize(Vector3.Cross(q[b] - q[a], q[d] - q[a]));
                    if (Vector3.Dot(nrm, outward) < 0)
                    {
                        nrm = -nrm;
                    }
                    bool up = nrm.Y > 0.6f;   // a chamfer or a step facing up: grass on it
                    var (V, I) = up ? top : rock;
                    int first = V.Count / 8;
                    foreach (int e in new[] { a, b, d })
                    {
                        Corner(V, q[e], nrm, up ? new Vector2(q[e].X, q[e].Z) / topSize : uv[e] / rockSize);
                    }
                    Facing(I, V, first, first + 1, first + 2, nrm);
                }
            }
        }
        // its foot, and its top: domed a little, its grass rolling 0.15 m over the edge and 0.3 m down it
        var (Y, P, Dy) = rings[0];
        var cap = rings[^1];
        var mid0 = new Vector3(P.Average(p => p.X), Y + Dy.Average(), P.Average(p => p.Y));
        var mid1 = new Vector3(cap.P.Average(p => p.X), cap.Y + cap.Dy.Average() + 0.08f, cap.P.Average(p => p.Y));
        for (int i = 0; i < n; i++)
        {
            Flat(rock, [mid0, At(0, i + 1), At(0, i)], -Vector3.UnitY, p => new Vector2(p.X, p.Z) / rockSize);
            var (a, b) = (At(rings.Length - 1, i), At(rings.Length - 1, i + 1));
            Flat(top, [mid1, a, b], Vector3.UnitY, p => new Vector2(p.X, p.Z) / topSize);
            Vector3 Over(Vector3 p)
            {
                var o = Vector3.Normalize(new Vector3(p.X - mid1.X, 0, p.Z - mid1.Z));
                return p + (o * 0.15f) - new Vector3(0, 0.3f, 0);
            }
            var side = Vector3.Normalize(new Vector3(((a.X + b.X) / 2) - mid1.X, 0.6f, ((a.Z + b.Z) / 2) - mid1.Z));
            Flat(top, [a, Over(a), Over(b), b], side, p => new Vector2(p.X, p.Z) / topSize);
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
