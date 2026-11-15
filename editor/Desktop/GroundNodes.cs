using System.Globalization;
using System.Text;
using Spec = Groundwork.Desktop.ZoneGenerator.Spec;

namespace Groundwork.Desktop;

/// <summary>
/// The Ground graph's kinds of node, and the graph worked out: each node's field made from its inputs' and its numbers,
/// kept (<see cref="FieldCache"/>) under a key of all that went into it, so a change makes again only the nodes after it.
/// </summary>
public static class GroundNodes
{
    /// <summary>A node's number: its id, label, range, default, unit, and the names of a choice's options (null: a number).</summary>
    public sealed record Param(string Id, string Label, float Min, float Max, float Value, string Unit = "", float Step = 1, string[]? Options = null);

    /// <summary>A kind of node: its id, name, group on the add menu, inputs, numbers, whether it takes text, and a line of help.</summary>
    public sealed record Kind(string Id, string Name, string Group, string[] Inputs, Param[] Params, string Help, string? TextLabel = null);

    /// <summary>Where the graph is worked out: the field's size and place (Unreal metres of its first point), the sea, the open map's ground.</summary>
    public sealed record Where(int Cols, int Rows, float X0, float Y0, float Sea = 0, float[]? MapGround = null);

    private static readonly Param Seed = new("seed", "Seed", 1, 99999, 1);

    /// <summary>Every kind, in the add menu's order.</summary>
    public static readonly Kind[] Kinds =
    [
        new("hills", "Hills", "Make", [], [new("size", "Hills apart", 50, 3000, 300, "m", 10), new("low", "Lowest", -100, 1000, 20, "m"), new("high", "Highest", -100, 3000, 120, "m"), new("ridges", "Ridges", 0, 1, 0, "", 0.05f), Seed], "Rolling ground: noise of the hills' size and smaller, from Lowest to Highest, more low ground than high."),
        new("flat", "Flat", "Make", [], [new("height", "Height", -500, 3000, 1, "m", 0.5f)], "Level ground at a height."),
        new("map", "This map's ground", "Make", [], [], "The open map's ground as it is now."),
        new("heightmap", "Heightmap", "Make", [], [new("low", "Black is", -1000, 5000, 0, "m"), new("high", "White is", -1000, 5000, 200, "m")], "A heightmap file (16-bit PNG, .r16, .r32, GeoTIFF) stretched over the map; a GeoTIFF's own heights are used.", "File"),
        new("mountain", "Mountain", "Make", [], [new("kind", "Kind", 0, 5, 0, "", 1, [.. Mountain.Presets.Select(p => p.Name)]), new("height", "Height", 5, 1500, 80, "m"), new("radius", "Radius", 20, 3000, 180, "m", 5), new("rough", "Rough", 0, 1, 0.6f, "", 0.05f), new("turn", "Turn", 0, 179, 0, "°"), new("x", "East of the middle", -5000, 5000, 0, "m", 5), new("z", "North of the middle", -5000, 5000, 0, "m", 5), Seed], "A mountain on flat ground at 0 (Add it to other ground)."),
        new("formula", "Formula", "Make", [], [new("h", "h", -1000, 1000, 10, "m", 0.5f)], "Heights from a formula of x and z (metres east and north of the middle), w and l (the map's width and length), h, and n(x, z) noise.", "Formula"),

        new("expand", "Expand", "Shape", ["in"], [new("reach", "Reach", -300, 300, 40, "m")], "The high ground broadened into plateaus (a negative reach: the valleys widened to flat floors)."),
        new("valleys", "Valleys", "Shape", ["in"], [new("strength", "Strength", 0, 3, 1, "", 0.05f)], "Streams gathering off the high ground cut valleys, and lay what they cut lower down."),
        new("glaciers", "Glaciers", "Shape", ["in"], [new("width", "Width", 32, 1000, 250, "m", 10), new("line", "Snow line (0: auto)", 0, 5000, 0, "m", 10)], "Ice from the high ground carves U-shaped troughs down the valleys."),
        new("strata", "Strata", "Shape", ["in"], [new("thick", "Band", 1, 40, 6, "m", 0.5f), new("rock", "Rock from", 0, 90, 30, "°"), Seed], "Bands of rock cut back into the steep faces: ledges and risers."),
        new("erosion", "Erosion", "Shape", ["in"], [new("strength", "Strength", 0, 3, 1, "", 0.05f), Seed], "Rain over the ground: gullies cut, the low ground filled."),
        new("scree", "Scree", "Shape", ["in"], [new("angle", "Rests at", 10, 60, 38, "°")], "Ground steeper than the angle slides down and rests at it, piling at the foot of every face."),
        new("sea", "Sea cut back", "Shape", ["in"], [new("width", "Cut back", 5, 400, 60, "m", 5), Seed], "The sea wears the shore back to a bench: beaches, and cliffs under high ground."),
        new("dunes", "Dunes", "Shape", ["in"], [new("height", "Height", 0.5f, 30, 6, "m", 0.5f), new("wind", "Wind towards", 0, 359, 0, "°"), Seed], "Sand dunes across the wind on flat open land."),
        new("crags", "Crags", "Shape", ["in"], [new("depth", "Depth", 0.5f, 20, 3, "m", 0.5f), new("rock", "Rock from", 0, 90, 30, "°"), Seed], "The rock faces' grain: crags, buttresses, gullies."),
        new("smooth", "Smooth", "Shape", ["in"], [new("radius", "Radius", 1, 60, 4, "m")], "Softened: each point the average round it."),
        new("terraces", "Terraces", "Shape", ["in"], [new("step", "Step", 0.5f, 100, 8, "m", 0.5f), new("sharp", "Sharpness", 0, 1, 0.7f, "", 0.05f)], "Steps of so many metres: flat treads and steep risers."),
        new("warp", "Warp", "Shape", ["in"], [new("strength", "Strength", 0, 300, 30, "m"), new("size", "Size", 10, 3000, 200, "m", 10), Seed], "Bent sideways by noise: each point takes the ground from up to Strength metres away, so straight ridges wander and round hills twist."),
        new("detail", "Detail", "Shape", ["in"], [new("height", "Height", 0, 50, 2, "m", 0.1f), new("size", "Size", 2, 1000, 40, "m"), new("ridged", "Ridged", 0, 1, 0, "", 0.05f), Seed], "Fractal bumps of about Size metres and smaller added on top, Height metres up and down (ridged: sharp crests)."),
        new("fold", "Fold", "Shape", ["in"], [new("height", "Height", 0, 200, 15, "m"), new("apart", "Folds apart", 20, 3000, 300, "m", 10), new("turn", "Running towards", 0, 179, 30, "°"), new("bend", "Bend", 0, 1, 0.3f, "", 0.05f), Seed], "Folded like rock under pressure: long waves Height metres up and down, Folds apart metres apart, their lines running towards Turn and bending."),
        new("range", "Height range", "Shape", ["in"], [new("low", "Lowest", -1000, 3000, 0, "m"), new("high", "Highest", -1000, 5000, 100, "m")], "Stretched (or squeezed) so its lowest is Lowest and its highest Highest."),

        new("blend", "Blend", "Combine", ["a", "b", "mask"], [new("amount", "B's share", 0, 1, 0.5f, "", 0.05f)], "A and B mixed: B's share of B (or, with a mask joined, the mask's share)."),
        new("add", "Add", "Combine", ["a", "b"], [new("times", "B times", -5, 5, 1, "", 0.05f)], "A with B (times a number) added on top."),
        new("max", "Highest of", "Combine", ["a", "b"], [], "At each point the higher of A and B."),
        new("min", "Lowest of", "Combine", ["a", "b"], [], "At each point the lower of A and B."),

        new("terrain", "Terrain mask", "Mask", ["in"], [new("map", "Map", 0, 6, 1, "", 1, ["Flow", "Wetness", "Ridges", "Facing north", "Facing east", "Snow", "Slope"]), new("from", "From", 0, 1, 0.5f, "", 0.05f), new("to", "To", 0, 1, 1, "", 0.05f), new("soft", "Soft edge", 0, 0.5f, 0.1f, "", 0.01f)], "1 where the terrain map is between From and To, 0 elsewhere, softly between (for Blend's mask)."),
        new("heightmask", "Height mask", "Mask", ["in"], [new("from", "From", -1000, 5000, 0, "m"), new("to", "To", -1000, 5000, 100, "m"), new("soft", "Soft edge", 0, 200, 10, "m")], "1 where the ground is between the two heights, 0 elsewhere, softly between."),

        new("output", "Output", "Output", ["in"], [], "The ground the graph makes."),
    ];

    /// <summary>A kind by id, or null.</summary>
    public static Kind? Of(string id) => Kinds.FirstOrDefault(k => k.Id == id);

    /// <summary>A node's number (its default when unset).</summary>
    public static float Value(GroundGraph.Node n, string id) =>
        n.Values.TryGetValue(id, out float v) ? v : Of(n.Kind)?.Params.FirstOrDefault(p => p.Id == id)?.Value ?? 0;

    /// <summary>
    /// Works the graph out up to a node (the Output, when none is named): its field, or why there is none (an input not
    /// joined, a formula that does not read, a file missing).
    /// </summary>
    public static (float[]? Field, string? Problem) Run(GroundGraph g, Where at, FieldCache? cache = null, int? upTo = null)
    {
        var target = upTo is int id ? g.Nodes.FirstOrDefault(n => n.Id == id) : g.Nodes.FirstOrDefault(n => n.Kind == "output");
        if (target == null)
        {
            return (null, upTo == null ? "No Output node: add one and join the ground into it." : "No such node.");
        }
        try
        {
            return (Field(g, target, at, cache ?? new FieldCache(), []).Field, null);
        }
        catch (GraphProblem e)
        {
            return (null, e.Message);
        }
    }

    private sealed class GraphProblem(string message) : Exception(message);

    /// <summary>A node's field and its key (what went into it), worked out from its inputs' (kept in the cache).</summary>
    private static (float[] Field, string Key) Field(GroundGraph g, GroundGraph.Node n, Where at, FieldCache cache, HashSet<int> path)
    {
        var kind = Of(n.Kind) ?? throw new GraphProblem($"A node of an unknown kind ({n.Kind}).");
        if (!path.Add(n.Id))
        {
            throw new GraphProblem("The wires go round in a loop.");
        }
        var inputs = new (float[] Field, string Key)?[kind.Inputs.Length];
        for (int i = 0; i < kind.Inputs.Length; i++)
        {
            if (g.Into(n.Id, i) is { } from)
            {
                inputs[i] = Field(g, from, at, cache, path);
            }
            else if (!(kind.Id == "blend" && i == 2))
            {
                throw new GraphProblem($"{kind.Name}: its input {kind.Inputs[i].ToUpperInvariant()} is not joined.");
            }
        }
        path.Remove(n.Id);
        var key = new StringBuilder(kind.Id);
        foreach (var p in kind.Params)
        {
            key.Append('|').Append(Value(n, p.Id).ToString("R", CultureInfo.InvariantCulture));
        }
        key.Append('|').Append(n.Text).Append('|').Append(at.Cols).Append('x').Append(at.Rows).Append('@').Append(at.X0).Append(',').Append(at.Y0).Append(',').Append(at.Sea);
        if (kind.Id == "map")
        {
            key.Append("|map:").Append(at.MapGround == null ? "-" : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(at.MapGround));
        }
        foreach (var i in inputs)
        {
            key.Append("|(").Append(i?.Key ?? "-").Append(')');
        }
        string k = key.ToString();
        if (cache.Get(k) is { } kept)
        {
            return (kept, k);
        }
        float[] made = Make(kind, n, [.. inputs.Select(i => i?.Field)], at);
        cache.Put(k, made);
        return (made, k);
    }

    /// <summary>One node's field from its inputs' (never changed: a new field is made).</summary>
    private static float[] Make(Kind kind, GroundGraph.Node n, float[]?[] input, Where at)
    {
        int cols = at.Cols, rows = at.Rows, size = cols * rows;
        float V(string id) => Value(n, id);
        int SeedOf() => (int)V("seed");
        float[] Copy() => (float[])input[0]!.Clone();
        float[] Each(Func<int, float> f)
        {
            float[] o = new float[size];
            Parallel.For(0, rows, r =>
            {
                for (int c = 0; c < cols; c++)
                {
                    o[(r * cols) + c] = f((r * cols) + c);
                }
            });
            return o;
        }
        float[] Step(string step, Spec spec)
        {
            float[] h = Copy();
            _ = ZoneGenerator.RunStep(spec with { Seed = (int)MathF.Max(1, V("seed")) }, step, h, cols, rows, at.X0, at.Y0);
            return h;
        }
        var none = new Spec(RimHeight: 0, LakeHectares: []);
        switch (kind.Id)
        {
            case "hills":
                return Hills(V("size"), V("low"), V("high"), V("ridges"), SeedOf(), at);
            case "flat":
                float flat = V("height");
                return Each(_ => flat);
            case "map":
                return at.MapGround is { } m && m.Length == size ? (float[])m.Clone() : throw new GraphProblem("This map's ground: no map open (or another size).");
            case "heightmap":
                return Heightmap(n.Text, V("low"), V("high"), at);
            case "mountain":
                var preset = Mountain.Presets[Math.Clamp((int)V("kind"), 0, Mountain.Presets.Length - 1)];
                var shape = Mountain.Shape(new MountainSpec(preset.Kind, V("height"), V("radius"), V("rough"), V("turn"), SeedOf()));
                float mx = V("x"), mz = V("z");
                // north is up: the first row is the north edge
                return Each(p => shape((p % cols) - (cols / 2f) - mx, (rows / 2f) - (p / cols) - mz));
            case "formula":
                return FormulaField(n.Text, V("h"), at);
            case "expand":
                return Step("expand", none with { Expand = V("reach") });
            case "valleys":
                return Step("valleys", none with { Valleys = V("strength") });
            case "glaciers":
                return Step("glaciers", none with { Glaciers = V("width"), SnowLine = V("line") });
            case "strata":
                return Step("strata", none with { Strata = V("thick"), RockSlope = V("rock") });
            case "erosion":
                return Step("erosion", none with { Erosion = V("strength") });
            case "scree":
                return Step("scree", none with { Talus = V("angle") });
            case "sea":
                return Step("sea", none with { SeaWear = V("width") });
            case "dunes":
                return Step("dunes", none with { Dunes = V("height"), DuneWind = V("wind") });
            case "crags":
                return Step("crags", none with { Crags = V("depth"), RockSlope = V("rock") });
            case "smooth":
                int radius = Math.Max(1, (int)V("radius"));
                return ZoneGenerator.BoxBlur(ZoneGenerator.BoxBlur(input[0]!, cols, rows, Math.Max(1, radius / 2)), cols, rows, Math.Max(1, radius / 2));
            case "terraces":
                float stepH = MathF.Max(0.1f, V("step")), sharp = Math.Clamp(V("sharp"), 0, 1);
                float[] t = input[0]!;
                return Each(p =>
                {
                    float u = t[p] / stepH, f = u - MathF.Floor(u), ease = MathF.Pow(f, 1 + (sharp * 8));
                    return (MathF.Floor(u) + ease) * stepH;
                });
            case "warp":
                float strength = V("strength"), wsize = MathF.Max(5, V("size"));
                float[] wsrc = input[0]!;
                int wseed = SeedOf();
                return Each(p =>
                {
                    float x = (p % cols) + at.X0, y = (p / cols) + at.Y0;
                    float dx = strength * ZoneGenerator.Fbm(x / wsize, y / wsize, wseed + 301, 3), dy = strength * ZoneGenerator.Fbm(x / wsize, y / wsize, wseed + 303, 3);
                    return ZoneGenerator.At(wsrc, cols, rows, (p % cols) + dx, (p / cols) + dy);
                });
            case "detail":
                float dh = V("height"), dsize = MathF.Max(1, V("size")), ridgedShare = Math.Clamp(V("ridged"), 0, 1);
                float[] dsrc = input[0]!;
                var dnoise = new Noise(SeedOf() + 307);
                return Each(p =>
                {
                    float x = ((p % cols) + at.X0) / dsize, y = ((p / cols) + at.Y0) / dsize, v = ZoneGenerator.Fbm(x, y, SeedOf() + 305, 4);
                    if (ridgedShare > 0)
                    {
                        v += ridgedShare * (ZoneGenerator.RidgedMultifractal(dnoise, x, y) - v);
                    }
                    return dsrc[p] + (dh * v);
                });
            case "fold":
                float fh = V("height"), apart = MathF.Max(5, V("apart")), turn = V("turn") * MathF.PI / 180, bendShare = V("bend");
                float fx = MathF.Cos(turn), fy = MathF.Sin(turn);
                float[] fsrc = input[0]!;
                int fseed = SeedOf();
                return Each(p =>
                {
                    // across the folds' lines (they run towards Turn), bent by noise
                    float x = (p % cols) + at.X0, y = (p / cols) + at.Y0;
                    float across = (-x * fy) + (y * fx) + (bendShare * apart * ZoneGenerator.Fbm(x / (apart * 3), y / (apart * 3), fseed + 309, 2));
                    return fsrc[p] + (fh * MathF.Sin(across / apart * MathF.Tau));
                });
            case "range":
                float[] src = input[0]!;
                float lo = src.Min(), hi = src.Max(), toLo = V("low"), toHi = V("high");
                return Each(p => hi - lo < 1e-4f ? toLo : toLo + ((src[p] - lo) * (toHi - toLo) / (hi - lo)));
            case "blend":
                float[] a = input[0]!, b = input[1]!;
                float[]? mask = input[2];
                float share = V("amount");
                return Each(p => a[p] + ((b[p] - a[p]) * Math.Clamp(mask?[p] ?? share, 0, 1)));
            case "add":
                float[] a2 = input[0]!, b2 = input[1]!;
                float times = V("times");
                return Each(p => a2[p] + (b2[p] * times));
            case "max":
                return Each(p => MathF.Max(input[0]![p], input[1]![p]));
            case "min":
                return Each(p => MathF.Min(input[0]![p], input[1]![p]));
            case "terrain":
                string[] maps = ["flow", "wetness", "convexity", "facing_north", "facing_east", "snow", "slope"];
                int which = Math.Clamp((int)V("map"), 0, maps.Length - 1);
                float[] heights = input[0]!;
                float[] tm = which == 6
                    ? Each(p => ZoneGenerator.Slope(heights, cols, rows, p % cols, p / cols) / 90)
                    : TerrainMaps.One(maps[which], heights, cols, rows, at.Sea)!;
                return Window(tm, V("from"), V("to"), V("soft"));
            case "heightmask":
                return Window(input[0]!, V("from"), V("to"), V("soft"));
            case "output":
                return input[0]!;
            default:
                throw new GraphProblem($"A node of an unknown kind ({kind.Id}).");
        }
        float[] Window(float[] v, float from, float to, float soft) => Each(p =>
        {
            float x = v[p];
            float up = soft <= 0 ? (x >= from ? 1 : 0) : Math.Clamp((x - from + soft) / (2 * soft), 0, 1);
            float down = soft <= 0 ? (x <= to ? 1 : 0) : Math.Clamp((to + soft - x) / (2 * soft), 0, 1);
            return MathF.Min(up, down);
        });
    }

    /// <summary>Rolling hills: noise of the hills' size and smaller, its lowest and highest hundredth stretched to low and high.</summary>
    private static float[] Hills(float size, float low, float high, float ridges, int seed, Where at)
    {
        int cols = at.Cols, rows = at.Rows;
        float[] h = new float[cols * rows];
        var ridged = new Noise(seed + 19);
        size = MathF.Max(10, size);
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                float x = (c + at.X0) / size, y = (r + at.Y0) / size, v = ZoneGenerator.Fbm(x, y, seed, 5);
                h[(r * cols) + c] = ridges > 0 ? v + (ridges * (ZoneGenerator.RidgedMultifractal(ridged, x, y) - v)) : v;
            }
        });
        float[] sample = [.. h.Where((_, i) => i % 97 == 0).Order()];
        float lo = sample[sample.Length / 100], hi = sample[Math.Min(sample.Length - 1, sample.Length * 99 / 100)];
        Parallel.For(0, h.Length, p =>
        {
            float t = Math.Clamp((h[p] - lo) / MathF.Max(1e-4f, hi - lo), 0, 1);
            h[p] = low + ((high - low) * MathF.Pow(t, 1.3f));
        });
        return h;
    }

    /// <summary>A heightmap file stretched over the field (bilinear), black at low and white at high (a GeoTIFF: its own heights).</summary>
    private static float[] Heightmap(string file, float low, float high, Where at)
    {
        if (file.Length == 0 || !File.Exists(file))
        {
            throw new GraphProblem(file.Length == 0 ? "Heightmap: no file chosen." : $"Heightmap: {Path.GetFileName(file)} is not there.");
        }
        HeightmapImport.Heightmap hm;
        try
        {
            hm = HeightmapImport.Read(file);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new GraphProblem($"Heightmap: {e.Message}");
        }
        (low, high) = hm is { Low: float l, High: float h } ? (l, h) : (low, high);
        float[] o = new float[at.Cols * at.Rows];
        Parallel.For(0, at.Rows, r =>
        {
            for (int c = 0; c < at.Cols; c++)
            {
                float u = c * (hm.Cols - 1) / (float)Math.Max(1, at.Cols - 1), v = r * (hm.Rows - 1) / (float)Math.Max(1, at.Rows - 1);
                o[(r * at.Cols) + c] = low + ((high - low) * ZoneGenerator.At(hm.Values, hm.Cols, hm.Rows, u, v));
            }
        });
        return o;
    }

    /// <summary>A formula's heights at each point (x, z metres east and north of the middle).</summary>
    private static float[] FormulaField(string src, float h, Where at)
    {
        Func<Formula.Env, double> f;
        try
        {
            f = Formula.Compile(src, ["x", "z", "w", "l", "h"]);
        }
        catch (FormatException e)
        {
            throw new GraphProblem($"Formula: {e.Message}");
        }
        var noise = new Noise(1);
        float[] o = new float[at.Cols * at.Rows];
        Parallel.For(0, at.Rows, () => new Formula.Env { Noise = (x, z) => noise.Fbm((float)x, (float)z) / 1.6 }, (r, _, env) =>
        {
            for (int c = 0; c < at.Cols; c++)
            {
                (env.Vars["x"], env.Vars["z"], env.Vars["w"], env.Vars["l"], env.Vars["h"]) = (c - (at.Cols / 2.0), (at.Rows / 2.0) - r, at.Cols, at.Rows, h);
                double v;
                try
                {
                    v = f(env);
                }
                catch (ArithmeticException)
                {
                    v = 0;
                }
                o[(r * at.Cols) + c] = double.IsFinite(v) ? (float)v : 0;
            }
            return env;
        }, _ => { });
        return o;
    }
}

/// <summary>The fields a graph's nodes made, by what went into them: at most <see cref="MostBytes"/>, the oldest used dropped first.</summary>
public sealed class FieldCache
{
    /// <summary>The most the fields hold together.</summary>
    public const long MostBytes = 256L << 20;

    private readonly Dictionary<string, LinkedListNode<(string Key, float[] Field)>> _byKey = [];
    private readonly LinkedList<(string Key, float[] Field)> _order = new();
    private long _bytes;

    /// <summary>How many fields it holds.</summary>
    public int Count => _byKey.Count;

    internal float[]? Get(string key)
    {
        if (!_byKey.TryGetValue(key, out var node))
        {
            return null;
        }
        _order.Remove(node);
        _order.AddLast(node);
        return node.Value.Field;
    }

    internal void Put(string key, float[] field)
    {
        long bytes = 4L * field.Length;
        if (_byKey.ContainsKey(key) || bytes > MostBytes)
        {
            return;
        }
        while (_bytes + bytes > MostBytes && _order.First is { } oldest)
        {
            _order.Remove(oldest);
            _byKey.Remove(oldest.Value.Key);
            _bytes -= 4L * oldest.Value.Field.Length;
        }
        _byKey[key] = _order.AddLast((key, field));
        _bytes += bytes;
    }

    /// <summary>Drops every field.</summary>
    public void Clear()
    {
        _byKey.Clear();
        _order.Clear();
        _bytes = 0;
    }
}
