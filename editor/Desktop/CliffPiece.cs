using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Cliff pieces: a map's kit of rock walls the generator sets along its steep ground, overlapping, each at a slope's
/// foot facing down it and reaching back into the hill, so that the slope shows as a face of rock (the landscape
/// cannot stand that steep, nor hang over). Each a wall of rock in strata, ledged, cracked and leaning out here and
/// there, its sides curving back into its neighbours, its ragged top under the ground above (the ground's own grass to
/// the edge; where it shows, it is of the map's ground too).
/// </summary>
public static class CliffPiece
{
    /// <summary>The catalog group the pieces are kinds of.</summary>
    public const string Category = "Cliffs";
    /// <summary>How many shapes a map has.</summary>
    public const int Variants = 6;
    /// <summary>A piece's width and height (its face) and depth back into the hill at scale 1, metres.</summary>
    public const float Width = 12, Height = 12, Depth = 18;
    /// <summary>Corners across the face and up it.</summary>
    private const int Across = 24, Up = 30;

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

    /// <summary>The face's top edge's height along it (ragged, up to 3 m under the piece's height), metres at scale 1.</summary>
    internal static Func<float, float> Top(int variant) =>
        x => Height - (3 * Math.Clamp(0.5f + (0.8f * ZoneGenerator.Fbm(x / 3.5f, 0.5f, 1300 + variant, 3)), 0, 1));

    /// <summary>How far the face stands out of its plane (z = 0, facing +z) at a point of it, metres at scale 1.</summary>
    internal static Func<float, float, float> Face(int variant)
    {
        var top = Top(variant);
        var rng = new Random(7411 + (variant * 6007));
        // strata: bands up the face, each standing out its own way, its lower edge further (ledges, a lean out)
        var bands = new List<(float From, float To, float Out, float Lean)>();
        for (float y = 0; y < Height;)
        {
            float tall = 1.2f + (2.2f * rng.NextSingle());
            bands.Add((y, MathF.Min(Height, y + tall), 2.2f * rng.NextSingle(), 0.5f + (1.1f * rng.NextSingle())));
            y += tall;
        }
        // cracks down it, here and there
        var cracks = Enumerable.Range(0, 3 + rng.Next(3)).Select(_ => ((rng.NextSingle() - 0.5f) * Width * 0.9f, 0.3f + (0.5f * rng.NextSingle()), 0.6f + (0.8f * rng.NextSingle()))).ToArray();
        int seed = 900 + variant;
        return (x, y) =>
        {
            var (from, to, stands, lean) = bands.FirstOrDefault(b => y >= b.From && y <= b.To, bands[^1]);
            float t = (y - from) / MathF.Max(0.01f, to - from);
            float z = stands + (lean * (1 - t));
            z -= cracks.Sum(c => c.Item3 * MathF.Exp(-MathF.Pow((x - c.Item1) / c.Item2, 2)));
            z += (0.45f * ZoneGenerator.Fbm(x / 2.5f, y / 2.5f, seed, 3)) + (1.4f * ZoneGenerator.Fbm(x / 7, y / 7, seed + 1, 2));
            // the sides curve back hard into the hill (and into the next piece); the top rounds back into the lip
            float e = MathF.Abs(2 * x / Width), t0 = top(x);
            z -= 7 * MathF.Pow(MathF.Max(0, e - 0.4f) / 0.6f, 2);
            z -= 2.2f * MathF.Pow(Math.Clamp((y - (t0 - 2)) / 2, 0, 1), 2);
            return z;
        };
    }

    /// <summary>One shape's mesh: its rock (face, sides, back, bottom) and its top (corners: position, normal, uv; triangles).</summary>
    internal static (List<float> V, List<int> I)[] Mesh(int variant, float rockSize, float topSize)
    {
        var face = Face(variant);
        var top = Top(variant);
        var rock = (V: new List<float>(), I: new List<int>());
        void Corner(List<float> v, Vector3 p, Vector3 n, Vector2 uv) => v.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, uv.X, uv.Y]);
        Vector3 F(int i, int j)
        {
            float x = (i / (float)Across - 0.5f) * Width, y = j / (float)Up * top(x);
            return new Vector3(x, y, face(x, y));
        }
        // the face: smooth, its picture laid on it upright
        for (int j = 0; j <= Up; j++)
        {
            for (int i = 0; i <= Across; i++)
            {
                var p = F(i, j);
                var n = Vector3.Normalize(Vector3.Cross(F(Math.Min(Across, i + 1), j) - F(Math.Max(0, i - 1), j), F(i, Math.Min(Up, j + 1)) - F(i, Math.Max(0, j - 1))));
                Corner(rock.V, p, n.Z < 0 ? -n : n, new Vector2(p.X, p.Y) / rockSize);
            }
        }
        for (int j = 0; j < Up; j++)
        {
            for (int i = 0; i < Across; i++)
            {
                int a = (j * (Across + 1)) + i, b = a + 1, c = a + Across + 1, d = c + 1;
                Facing(rock.I, rock.V, a, b, c, Vector3.UnitZ);
                Facing(rock.I, rock.V, b, d, c, Vector3.UnitZ);
            }
        }
        // the sides, the back and the bottom: flat, back into the hill
        void Quad((List<float> V, List<int> I) part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Func<Vector3, Vector2> uv)
        {
            int first = part.V.Count / 8;
            foreach (var p in new[] { a, b, c, d })
            {
                Corner(part.V, p, outward, uv(p));
            }
            Facing(part.I, part.V, first, first + 1, first + 2, outward);
            Facing(part.I, part.V, first, first + 2, first + 3, outward);
        }
        for (int j = 0; j < Up; j++)
        {
            foreach (int i in new[] { 0, Across })
            {
                var (p, q) = (F(i, j), F(i, j + 1));
                Quad(rock, p, q, new Vector3(q.X, q.Y, -Depth), new Vector3(p.X, p.Y, -Depth), i == 0 ? -Vector3.UnitX : Vector3.UnitX, v => new Vector2(v.Z, v.Y) / rockSize);
            }
        }
        float hw = Width / 2;
        Quad(rock, new(-hw, 0, -Depth), new(hw, 0, -Depth), new(hw, Height, -Depth), new(-hw, Height, -Depth), -Vector3.UnitZ, v => new Vector2(v.X, v.Y) / rockSize);
        Quad(rock, new(-hw, 0, F(0, 0).Z), new(hw, 0, F(Across, 0).Z), new(hw, 0, -Depth), new(-hw, 0, -Depth), -Vector3.UnitY, v => new Vector2(v.X, v.Z) / rockSize);
        // the top, from the face's top edge back, a little uneven (under the ground, or level with it), its picture laid flat
        var lip = (V: new List<float>(), I: new List<int>());
        int lipFirst = 0;
        const int Back = 10;
        for (int k = 0; k <= Back; k++)
        {
            for (int i = 0; i <= Across; i++)
            {
                var edge = F(i, Up);
                float z = edge.Z + ((-Depth - edge.Z) * k / Back);
                float y = edge.Y + (k == 0 ? 0 : 0.3f * ZoneGenerator.Fbm(edge.X / 3, z / 3, 977 + variant, 2)) - (1.2f * MathF.Pow(MathF.Abs(2 * edge.X / Width), 4));
                Corner(lip.V, new Vector3(edge.X, y, z), Vector3.UnitY, new Vector2(edge.X, z) / topSize);
            }
        }
        for (int k = 0; k < Back; k++)
        {
            for (int i = 0; i < Across; i++)
            {
                int a = lipFirst + (k * (Across + 1)) + i, b = a + 1, c = a + Across + 1, d = c + 1;
                Facing(lip.I, lip.V, a, b, c, Vector3.UnitY);
                Facing(lip.I, lip.V, b, d, c, Vector3.UnitY);
            }
        }
        return [rock, lip];
    }

    /// <summary>A triangle turned to face <paramref name="side"/> (counter-clockwise from there, as glTF draws its front).</summary>
    private static void Facing(List<int> idx, List<float> v, int a, int b, int c, Vector3 side)
    {
        Vector3 P(int i) => new(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]);
        bool turned = Vector3.Dot(Vector3.Cross(P(b) - P(a), P(c) - P(a)), side) < 0;
        idx.AddRange(turned ? [a, c, b] : [a, b, c]);
    }
}
