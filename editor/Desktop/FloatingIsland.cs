using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// Floating islands: lumps of ground hanging in the air, a domed top of the map's ground and rock below tapering to a
/// point. The landscape cannot hang over itself (here or in Unreal), so each is a glTF model the zone places like any
/// other, its top planted by the generator (<see cref="TopAt"/>).
/// </summary>
public static class FloatingIsland
{
    /// <summary>The catalog group the islands are kinds of.</summary>
    public const string Category = "Islands";
    /// <summary>How many shapes a map has.</summary>
    public const int Variants = 6;
    /// <summary>An island's mean radius at scale 1, metres.</summary>
    public const float Radius = 20;
    /// <summary>Around the island, and rings from its middle to its edge (the top) and from its edge to its point (below).</summary>
    private const int Around = 32, TopRings = 8, BelowRings = 9, EarthRings = 4;

    /// <summary>
    /// A shape's wobbles: the edge's and the top's (amplitude, frequency, phase), how deep it hangs, its underside's lobes
    /// (deeper on some sides) and where its point leans.
    /// </summary>
    private sealed record Shape((float A, int F, float P)[] Edge, (float A, Vector2 F, float P)[] Bumps, float Depth, int Seed, (float A, int F, float P)[] Lobes, Vector2 Lean);

    private static Shape Of(int variant)
    {
        var rng = new Random(9137 + (variant * 7919));
        float R() => rng.NextSingle();
        return new Shape(
            [.. Enumerable.Range(2, 4).Select(f => (0.22f / f * (0.6f + R()), f, R() * MathF.Tau))],
            [.. Enumerable.Range(0, 4).Select(_ => (0.5f + R(), new Vector2(R() - 0.5f, R() - 0.5f) * 0.5f, R() * MathF.Tau))],
            Radius * (1.1f + (0.7f * R())), 4513 + variant,
            [.. Enumerable.Range(2, 2).Select(f => (0.2f + (0.15f * R()), f, R() * MathF.Tau))],
            new Vector2(R() - 0.5f, R() - 0.5f) * Radius * 0.5f);
    }

    /// <summary>The edge's distance from the middle at an angle (radians), metres at scale 1.</summary>
    public static float Edge(int variant, float angle) =>
        Radius * (1 + Of(variant).Edge.Sum(e => e.A * MathF.Sin((e.F * angle) + e.P)));

    /// <summary>The top's height over the edge at a point of the model (metres at scale 1; null: off the island).</summary>
    public static float? TopAt(int variant, float x, float z)
    {
        var s = Of(variant);
        float t = MathF.Sqrt((x * x) + (z * z)) / Edge(variant, MathF.Atan2(z, x));
        if (t > 1)
        {
            return null;
        }
        float bumps = s.Bumps.Sum(b => b.A * MathF.Sin((b.F.X * x) + (b.F.Y * z) + b.P));
        return (1 - (t * t)) * ((Radius * 0.12f) + bumps);
    }

    /// <summary>Which shape a model of the islands is (its name's last number), or -1.</summary>
    public static int VariantOf(string model) =>
        model.LastIndexOf('_') is var u and >= 0 && int.TryParse(model[(u + 1)..], out int v) && v is >= 0 and < Variants ? v : -1;

    /// <summary>A map's island models' names (one per shape), from its name.</summary>
    public static string[] Models(string map)
    {
        string safe = new([.. map.Where(char.IsAsciiLetterOrDigit)]);
        return [.. Enumerable.Range(0, Variants).Select(v => $"FloatingIsland_{safe}_{v}")];
    }

    /// <summary>
    /// Writes a map's islands into its models folder (its first layer on top, its earth under the edge, its rock below) and puts them in its
    /// catalog under <see cref="Category"/>; written again, they take the map's layers as they are now.
    /// </summary>
    /// <returns>The islands' kinds.</returns>
    public static List<MapTools.Kind> WriteFor(MapProject m)
    {
        string folder = m.Models ?? Path.Combine(m.Folder, "models");
        Directory.CreateDirectory(folder);
        var top = m.Layers[0];
        MapProject.Layer? Named(params string[] words) => m.Layers.FirstOrDefault(l => words.Any(w => l.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
        var below = Named("Rock", "Cliff", "Stone") ?? m.Layers[^1];
        var earth = Named("Dirt", "Earth", "Mud", "Ground") ?? below;
        string? File(MapProject.Layer l) => l.Texture is { } t && System.IO.File.Exists(Path.Combine(m.Folder, t)) ? Path.Combine(m.Folder, t) : null;
        string[] names = Models(m.Name);
        for (int v = 0; v < Variants; v++)
        {
            Write(Path.Combine(folder, names[v] + ".glb"), v, [(File(top), top.Colour, top.Size), (File(earth), earth.Colour, earth.Size), (File(below), below.Colour, below.Size)]);
        }
        var kinds = names.Select((n, v) => new MapTools.Kind(n, $"Floating island {v + 1}", Category, 1, 1, null)).ToList();
        if (m.Catalog is { } cat && System.IO.File.Exists(cat))
        {
            var j = JsonNode.Parse(System.IO.File.ReadAllText(cat))!.AsObject();
            var list = j["kinds"] as JsonArray ?? [];
            foreach (var k in kinds.Where(k => !list.Any(e => e?["model"]?.GetValue<string>() == k.Model)))
            {
                list.Add(new JsonObject { ["model"] = k.Model, ["label"] = k.Label, ["category"] = Category, ["scale"] = new JsonArray(1, 1) });
            }
            j["kinds"] = list;
            System.IO.File.WriteAllText(cat, j.ToJsonString(new() { WriteIndented = true }));
        }
        return kinds;
    }

    /// <summary>One shape's mesh: the top's, the earth's under the edge and the rock's corners (position, normal, uv) and triangles.</summary>
    internal static (List<float> V, List<int> I)[] Mesh(int variant, float topSize, float earthSize, float belowSize)
    {
        var s = Of(variant);
        var top = (V: new List<float>(), I: new List<int>());
        void Corner(List<float> v, Vector3 p, Vector3 n, Vector2 uv) => v.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, uv.X, uv.Y]);
        Vector3 TopPoint(float x, float z) => new(x, TopAt(variant, x, z) ?? 0, z);
        // the top: smooth, its normals from its slope, its picture laid flat
        Vector3 At(int ring, int a)
        {
            float ang = a * MathF.Tau / Around, r = Edge(variant, ang) * ring / TopRings * (ring == TopRings ? 0.9999f : 1);
            return TopPoint(r * MathF.Cos(ang), r * MathF.Sin(ang));
        }
        for (int ring = 0; ring <= TopRings; ring++)
        {
            for (int a = 0; a < Around; a++)
            {
                var p = At(ring, a);
                var n = Vector3.Normalize(new Vector3(TopPoint(p.X - 0.5f, p.Z).Y - TopPoint(p.X + 0.5f, p.Z).Y, 1, TopPoint(p.X, p.Z - 0.5f).Y - TopPoint(p.X, p.Z + 0.5f).Y));
                Corner(top.V, p, n, new Vector2(p.X, p.Z) / topSize);
            }
        }
        for (int ring = 0; ring < TopRings; ring++)
        {
            for (int a = 0; a < Around; a++)
            {
                int b = (a + 1) % Around, p = (ring * Around) + a, q = (ring * Around) + b;
                Facing(top.I, top.V, p, q, p + Around, Vector3.UnitY);
                Facing(top.I, top.V, q, q + Around, p + Around, Vector3.UnitY);
            }
        }
        // below: the edge, a lip of earth bulging out, then rock hanging deeper on some sides, its point leaning; each
        // face flat, its points jagged
        var rng = new Random(s.Seed);
        var earth = (V: new List<float>(), I: new List<int>());
        var below = (V: new List<float>(), I: new List<int>());
        var grid = new Vector3[BelowRings + 1, Around + 1];
        for (int k = 0; k <= BelowRings; k++)
        {
            for (int a = 0; a <= Around; a++)
            {
                float ang = a * MathF.Tau / Around, e = Edge(variant, ang);
                float u = Math.Max(0, (k - 1f) / (BelowRings - 1)), lobes = 1 + (s.Lobes.Sum(l => l.A * MathF.Sin((l.F * ang) + l.P)) * MathF.Sin(MathF.PI * u));
                (float r, float y) = k switch
                {
                    0 => (e, 0f),
                    1 => (e * 1.02f, -Radius * 0.07f),
                    _ => (e * MathF.Pow(1 - MathF.Pow(u, 1.4f), 0.85f), -(Radius * 0.07f) - (s.Depth * MathF.Pow(u, 1.2f) * lobes)),
                };
                float jag = k is 0 or BelowRings || a == Around ? 1 : k == 1 ? 0.97f + (0.06f * rng.NextSingle()) : 0.78f + (0.44f * rng.NextSingle());
                var lean = s.Lean * u * u;
                grid[k, a] = a == Around ? grid[k, 0] : new Vector3((r * jag * MathF.Cos(ang)) + lean.X, y + (k is 0 or BelowRings ? 0 : (rng.NextSingle() - 0.5f) * 5), (r * jag * MathF.Sin(ang)) + lean.Y);
            }
        }
        // faces turned away from a point inside the rock
        var middle = new Vector3(0, -s.Depth * 0.35f, 0);
        float circ = Radius * MathF.Tau;
        for (int k = 0; k < BelowRings; k++)
        {
            for (int a = 0; a < Around; a++)
            {
                float u0 = circ * a / Around, u1 = circ * (a + 1) / Around;
                var (p, q, r, t) = (grid[k, a], grid[k, a + 1], grid[k + 1, a], grid[k + 1, a + 1]);
                var part = k < EarthRings ? earth : below;
                float size = k < EarthRings ? earthSize : belowSize;
                FaceUv(part, size, p, q, r, u0, u1, u0);
                if (k + 1 < BelowRings)
                {
                    FaceUv(part, size, q, t, r, u1, u1, u0);
                }
            }
        }
        return [top, earth, below];

        void FaceUv((List<float> V, List<int> I) part, float size, Vector3 p, Vector3 q, Vector3 r, float up, float uq, float ur)
        {
            var n = Vector3.Normalize(Vector3.Cross(q - p, r - p));
            if (Vector3.Dot(n, ((p + q + r) / 3) - middle) < 0)
            {
                (q, r, uq, ur, n) = (r, q, ur, uq, -n);
            }
            int first = part.V.Count / 8;
            Corner(part.V, p, n, new Vector2(up, p.Y) / size);
            Corner(part.V, q, n, new Vector2(uq, q.Y) / size);
            Corner(part.V, r, n, new Vector2(ur, r.Y) / size);
            part.I.AddRange([first, first + 1, first + 2]);
        }
    }

    /// <summary>A triangle turned to face <paramref name="side"/> (counter-clockwise from there, as glTF draws its front).</summary>
    private static void Facing(List<int> idx, List<float> v, int a, int b, int c, Vector3 side)
    {
        Vector3 P(int i) => new(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]);
        bool turned = Vector3.Dot(Vector3.Cross(P(b) - P(a), P(c) - P(a)), side) < 0;
        idx.AddRange(turned ? [a, c, b] : [a, b, c]);
    }

    /// <summary>
    /// One shape as a .glb: three parts, the top, the earth under its edge and the rock below, each with its layer's
    /// picture (embedded) or its colour.
    /// </summary>
    internal static void Write(string file, int variant, (string? Png, Vector3 Colour, float Size)[] looks)
    {
        float Size(int k) => looks[k].Size > 0 ? looks[k].Size : 4;
        var parts = Mesh(variant, Size(0), Size(1), Size(2));
        var bin = new MemoryStream();
        var views = new JsonArray();
        var accessors = new JsonArray();
        int View(byte[] bytes, int? target)
        {
            while (bin.Length % 4 != 0)
            {
                bin.WriteByte(0);
            }
            var view = new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = bytes.Length };
            if (target is { } t)
            {
                view["target"] = t;
            }
            bin.Write(bytes);
            views.Add(view);
            return views.Count - 1;
        }
        int Accessor(float[] data, int offset, int width, string type, bool bounds)
        {
            int n = data.Length / 8;
            float[] flat = new float[n * width];
            for (int i = 0; i < n; i++)
            {
                Array.Copy(data, (i * 8) + offset, flat, i * width, width);
            }
            byte[] bytes = new byte[flat.Length * 4];
            Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
            var acc = new JsonObject { ["bufferView"] = View(bytes, 34962), ["componentType"] = 5126, ["count"] = n, ["type"] = type };
            if (bounds)
            {
                acc["min"] = new JsonArray([.. Enumerable.Range(0, width).Select(c => (JsonNode)Enumerable.Range(0, n).Min(i => flat[(i * width) + c]))]);
                acc["max"] = new JsonArray([.. Enumerable.Range(0, width).Select(c => (JsonNode)Enumerable.Range(0, n).Max(i => flat[(i * width) + c]))]);
            }
            accessors.Add(acc);
            return accessors.Count - 1;
        }
        var materials = new JsonArray();
        var textures = new JsonArray();
        var images = new JsonArray();
        var prims = new JsonArray();
        foreach (var (part, look, name) in parts.Zip(looks, ["Top", "Earth", "Rock"]))
        {
            float[] v = [.. part.V];
            byte[] idx = new byte[part.I.Count * 4];
            Buffer.BlockCopy(part.I.ToArray(), 0, idx, 0, idx.Length);
            int indices = accessors.Count;
            accessors.Add(new JsonObject { ["bufferView"] = View(idx, 34963), ["componentType"] = 5125, ["count"] = part.I.Count, ["type"] = "SCALAR" });
            var pbr = new JsonObject { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
            if (look.Png != null)
            {
                images.Add(new JsonObject { ["bufferView"] = View(System.IO.File.ReadAllBytes(look.Png), null), ["mimeType"] = "image/png" });
                textures.Add(new JsonObject { ["source"] = images.Count - 1 });
                pbr["baseColorTexture"] = new JsonObject { ["index"] = textures.Count - 1 };
            }
            else
            {
                pbr["baseColorFactor"] = new JsonArray(look.Colour.X, look.Colour.Y, look.Colour.Z, 1);
            }
            materials.Add(new JsonObject { ["name"] = name, ["pbrMetallicRoughness"] = pbr });
            prims.Add(new JsonObject
            {
                ["attributes"] = new JsonObject { ["POSITION"] = Accessor(v, 0, 3, "VEC3", true), ["NORMAL"] = Accessor(v, 3, 3, "VEC3", false), ["TEXCOORD_0"] = Accessor(v, 6, 2, "VEC2", false) },
                ["indices"] = indices,
                ["material"] = materials.Count - 1,
            });
        }
        while (bin.Length % 4 != 0)
        {
            bin.WriteByte(0);
        }
        var gltf = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Groundwork" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0, ["name"] = Path.GetFileNameWithoutExtension(file) }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = prims }),
            ["materials"] = materials,
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length }),
            ["bufferViews"] = views,
            ["accessors"] = accessors,
        };
        if (images.Count > 0)
        {
            (gltf["images"], gltf["textures"]) = (images, textures);
        }
        byte[] json = Encoding.UTF8.GetBytes(gltf.ToJsonString());
        int jsonLength = (json.Length + 3) & ~3;
        using var w = new BinaryWriter(System.IO.File.Create(file));
        w.Write(0x46546C67u);
        w.Write(2u);
        w.Write((uint)(12 + 8 + jsonLength + 8 + bin.Length));
        w.Write((uint)jsonLength);
        w.Write(0x4E4F534Au);
        w.Write(json);
        w.Write(Enumerable.Repeat((byte)' ', jsonLength - json.Length).ToArray());
        w.Write((uint)bin.Length);
        w.Write(0x004E4942u);
        w.Write(bin.ToArray());
    }
}
