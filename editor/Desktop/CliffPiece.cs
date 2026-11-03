using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Cliff pieces: a map's kit of rock walls the generator sets along its steep ground, overlapping, each at a slope's
/// foot facing down it and reaching back into the hill, so that the slope shows as a face of rock (the landscape
/// cannot stand that steep, nor hang over). Each a wall of pillars between vertical joints, in beds, set back at
/// ledges with grass on them, its foot flaring, its sides curving back into its neighbours, its top under the ground
/// above and its grass rolling over the edge (of the map's ground: its own grass to the edge).
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
    private const int Across = 60, Up = 96;

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

    /// <summary>The face's top edge's height along it (a little uneven, up to 1.2 m under the piece's height), metres at scale 1.</summary>
    internal static Func<float, float> Top(int variant) =>
        x => Height - (1.2f * Math.Clamp(0.5f + (0.8f * ZoneGenerator.Fbm(x / 3.5f, 0.5f, 1300 + variant, 3)), 0, 1));

    /// <summary>How far the face stands out of its plane (z = 0, facing +z) at a point of it, metres at scale 1.</summary>
    internal static Func<float, float, float> Face(int variant)
    {
        var top = Top(variant);
        var rng = new Random(7411 + (variant * 6007));
        // pillars: blocks across the face between vertical joints, each standing out its own way
        var pillars = new List<(float From, float Out)>();
        for (float x = -Width; x < Width;)
        {
            pillars.Add((x, 2.0f * rng.NextSingle()));
            x += 1.4f + (2.4f * rng.NextSingle());
        }
        // beds: layers up the face, each with a groove at its foot; now and then a ledge, the face above it set back
        var beds = new List<(float From, float Out)>();
        float back = 0;
        for (float y = 0; y < Height + 2;)
        {
            if (y > 1.5f && rng.NextSingle() < 0.35f)
            {
                back -= 0.8f + (0.8f * rng.NextSingle());
            }
            beds.Add((y, back));   // flush but for the ledges: a step of a few centimetres would grow a line of grass
            y += 0.7f + (1.1f * rng.NextSingle());
        }
        var cracks = Enumerable.Range(0, 2 + rng.Next(2)).Select(_ => ((rng.NextSingle() - 0.5f) * Width * 0.9f, 0.15f + (0.2f * rng.NextSingle()), 0.4f + (0.5f * rng.NextSingle()))).ToArray();
        int seed = 900 + variant;
        return (x, y) =>
        {
            // the pillar it is in, its edge eased over 0.12 m into the next (a sharp joint, not a cut)
            int k = Math.Max(0, pillars.FindLastIndex(p => p.From <= x));
            float into = x - pillars[k].From;
            float z = k > 0 ? pillars[k - 1].Out + ((pillars[k].Out - pillars[k - 1].Out) * Math.Clamp(into / 0.12f, 0, 1)) : pillars[k].Out;
            int b = Math.Max(0, beds.FindLastIndex(d => d.From <= y));
            z += beds[b].Out - (0.15f * MathF.Max(0, 1 - (MathF.Abs(y - beds[b].From) / 0.25f)));
            z += 1.2f * MathF.Pow(MathF.Max(0, 1 - (y / 2.5f)), 2);   // its foot flaring out
            z -= cracks.Sum(c => c.Item3 * MathF.Exp(-MathF.Pow((x - c.Item1) / c.Item2, 2)));
            z += (0.12f * ZoneGenerator.Fbm(x / 1.5f, y / 1.5f, seed, 2)) + (0.6f * ZoneGenerator.Fbm(x / 7, y / 7, seed + 1, 2));
            // the sides curve back hard into the hill (and into the next piece); the top rounds back into the lip
            float e = MathF.Abs(2 * x / Width), t0 = top(x);
            z -= 7 * MathF.Pow(MathF.Max(0, e - 0.4f) / 0.6f, 2);
            z -= 1.2f * MathF.Pow(Math.Clamp((y - (t0 - 1.2f)) / 1.2f, 0, 1), 2);
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
        var lip = (V: new List<float>(), I: new List<int>());
        Vector3 P(List<float> v, int i) => new(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]);
        void Triangle(int a, int b, int c)
        {
            var n = Vector3.Normalize(Vector3.Cross(P(rock.V, b) - P(rock.V, a), P(rock.V, c) - P(rock.V, a)));
            if ((n.Z < 0 ? -n : n).Y > 0.75f)   // facing up, near flat (a groove's lower side is no ledge)
            {
                // a ledge: grass on it, its picture laid flat
                int first = lip.V.Count / 8;
                foreach (int i in new[] { a, b, c })
                {
                    var p = P(rock.V, i);
                    Corner(lip.V, p, Vector3.UnitY, new Vector2(p.X, p.Z) / topSize);
                }
                Facing(lip.I, lip.V, first, first + 1, first + 2, Vector3.UnitY);
            }
            else
            {
                Facing(rock.I, rock.V, a, b, c, Vector3.UnitZ);
            }
        }
        for (int j = 0; j < Up; j++)
        {
            for (int i = 0; i < Across; i++)
            {
                int a = (j * (Across + 1)) + i, b = a + 1, c = a + Across + 1, d = c + 1;
                Triangle(a, b, c);
                Triangle(b, d, c);
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
        int lipFirst = lip.V.Count / 8;
        const int Back = 10;
        for (int k = -1; k <= Back; k++)
        {
            for (int i = 0; i <= Across; i++)
            {
                var edge = F(i, Up);
                // the first row hangs over the edge: the grass rolling forward and down over the rock
                float z = k < 0 ? edge.Z + 0.45f : edge.Z + ((-Depth - edge.Z) * k / Back);
                float y = edge.Y + (k <= 0 ? (k < 0 ? -0.7f : 0.15f) : 0.3f * ZoneGenerator.Fbm(edge.X / 3, z / 3, 977 + variant, 2)) - (1.2f * MathF.Pow(MathF.Abs(2 * edge.X / Width), 4));
                Corner(lip.V, new Vector3(edge.X, y, z), Vector3.UnitY, new Vector2(edge.X, z) / topSize);
            }
        }
        for (int k = 0; k < Back + 1; k++)
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
