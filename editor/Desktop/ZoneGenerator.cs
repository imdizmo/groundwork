using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// A whole zone generated from a few numbers: rolling ground between two heights with hills so far apart, a mountain
/// rim round it, lakes of given sizes in its low ground, woods covering a share of it, and the map's four layers painted
/// by rule (rock on steep ground, dirt under the woods, sand on the shores, grass elsewhere). Every seed is a new zone.
/// </summary>
public static partial class ZoneGenerator
{
    /// <summary>
    /// What to make: the rolling ground's lowest and highest heights and how far apart its hills are (m); the rim's
    /// height above the rolling ground and its width (0: none); the lakes' sizes (hectares); the share of the ground
    /// that is wooded (0..1) and its trees per hectare; trees per hectare outside the woods; small objects (bushes,
    /// plants, rocks) per hectare; how much bigger the trees are than their kinds' own sizes (a game's trees are often
    /// several times a real tree's: the canopy, not the count, is what makes a wood); the sand band round the lakes (m,
    /// 0: none); the seed. Then the shapes some zones have: shelves every Terraces metres over TerraceShare of the ground
    /// (mesas, highland steps); canyons Canyons metres deep, CanyonWidth wide, about CanyonSpacing apart; a coast on one
    /// side (west, east, north or south; Coast metres wide, down to CoastDepth under the sea, or with CoastDry a drop into
    /// nothing, the world's edge); Rivers running downhill to a lake, the coast or the edge; Floaters, big rocks hanging in
    /// the air; snow above SnowLine, on the painted layer SnowChannel (0: the first, as the shores; 1: the second);
    /// OpenDirt, the share of the open ground patched with the second painted layer; rock painted from RockSlope degrees;
    /// a marsh: the ground lower than Marsh metres flattened into wet flats, MarshShare of it pools under the sea's level;
    /// Rough: bumps of so many metres a few tens of metres across, over the rolling ground (no ground is quite flat);
    /// Camps: so many camps of CampSize buildings (the catalog's Buildings) round levelled ground, Props among them, joined
    /// by Roads (levelled strips painted with the second layer, or RoadChannel's: 0 the first, 1 the second) when asked;
    /// Giants: the share of the trees that are giants, about GiantScale times their kind's size (one here and there, not
    /// whole groves: a game's few great trees make much of its canopy); cliffs: ground steeper than CliffSlope degrees faced
    /// with the catalog's Cliffs (0: none), pieces of rock wall overlapping along it; fjords: inlets cut from the coast into
    /// the high ground, Fjords metres under the sea at its mouth, FjordWidth wide, about FjordSpacing apart (0: none); crags:
    /// the faces steeper than RockSlope that break from the slope round them (and cliffs) carved Crags metres deep with
    /// crags, buttresses and gullies, ribs and chutes running down them (0: smooth);
    /// strata: bands of rock Strata metres thick cut into the faces (0: none); erosion: rain over the whole map, Erosion times
    /// the brush's fill (0: none); scree: ground steeper than Talus degrees slid down to rest at it (0: none), what the
    /// weather laid down painted earth and what it cut rock; Ridges: that share of the hills sharp-crested (0..1); Expand:
    /// the high ground broadened so many metres into plateaus, or the low ground with a negative one (0: none); Crests:
    /// rock painted on the ground standing over its surroundings, so strongly (0..1); Valleys: streams cutting valleys
    /// down from the high ground and laying what they cut lower down, so strongly (0: none); Streams: earth painted
    /// where the water gathers, so strongly (0..1); SnowSlip: snow sliding off ground steeper than so many degrees and settling in the hollows (0: none).
    /// Meander: the rivers winding from side to side across their valleys, up to three river widths each way (0..1; 0: they
    /// follow the fall of the ground). Glaciers: the valleys fed from high ground carved into U-shaped troughs about so
    /// many metres across (0: none). SeaWear: how far the sea has cut the land back, metres (beaches, a bench and sea cliffs;
    /// 0: none). Dunes: sand dunes on flat open land, so many metres high (0: none), lying across the wind blowing
    /// towards DuneWind degrees. Steps: the order the ground is shaped in (see <see cref="ShapeStep"/>; null: the usual
    /// order, <see cref="DefaultSteps"/>).
    /// </summary>
    public sealed record Spec(float Low = 20, float High = 70, float HillSize = 300, float RimHeight = 200, float RimWidth = 220,
        float[]? LakeHectares = null, float Woods = 0.35f, float WoodDensity = 17, float OpenTrees = 0.3f, float Small = 20, float TreeScale = 1, float Shore = 6, int Seed = 1,
        float Terraces = 0, float TerraceShare = 1, float Canyons = 0, float CanyonWidth = 40, float CanyonSpacing = 500,
        float Coast = 0, string CoastSide = "west", float CoastDepth = 15, bool CoastDry = false, int Rivers = 0, float RiverWidth = 8,
        int Floaters = 0, float SnowLine = 0, float OpenDirt = 0.12f, int SnowChannel = 0, float RockSlope = 30, float Marsh = 0, float MarshShare = 0.3f, float Rough = 0, int Camps = 0, int CampSize = 5, bool Roads = false, int RoadChannel = 1, float Giants = 0, float GiantScale = 2.5f, float CliffSlope = 0,
        float Fjords = 0, float FjordWidth = 300, float FjordSpacing = 1400, float Crags = 0,
        float Strata = 0, float Erosion = 0, float Talus = 0, float Ridges = 0, float Expand = 0, float Crests = 0,
        float Valleys = 0, float Streams = 0, float SnowSlip = 0, float Meander = 0, float Glaciers = 0, float SeaWear = 0, float Dunes = 0, float DuneWind = 0, ShapeStep[]? Steps = null);

    /// <summary>A lake made: its middle and outline (Unreal X, Y metres), its surface height and depth.</summary>
    public sealed record Lake(Vector2 Middle, Vector2[] Outline, float Level, float Depth);

    /// <summary>What was made, on a map of cols by rows metres whose first pixel is at (x0, y0) in Unreal metres.</summary>
    public sealed record Zone(float[] Heights, byte[] Mask, List<(string Model, Vector3 At, float Yaw, float Scale)> Objects, List<Lake> Lakes, float[] Wooded)
    {
        /// <summary>The rivers, each a stretch whose surface falls evenly from Top to End (a river is several stretches end to end).</summary>
        public List<River> Rivers { get; init; } = [];

        /// <summary>The roads, each its points (Unreal X, Y metres).</summary>
        public List<List<Vector2>> Roads { get; init; } = [];

        /// <summary>What the water and weather moved, metres a point (laid down, or cut when below 0); null: no wear asked.</summary>
        public float[]? Worn { get; init; }
    }

    /// <summary>A stretch of river: its points (Unreal X, Y metres, half-width, depth, current) and its surface at its ends.</summary>
    public sealed record River(List<MapWater.Point> Points, float Top, float End);

    /// <summary>The kinds the zone is planted with, by the catalog's groups.</summary>
    public sealed record Planting(IReadOnlyList<MapTools.Kind> Trees, IReadOnlyList<MapTools.Kind> Bushes, IReadOnlyList<MapTools.Kind> Rocks, IReadOnlyList<MapTools.Kind> Plants)
    {
        /// <summary>The buildings camps are made of.</summary>
        public IReadOnlyList<MapTools.Kind> Buildings { get; init; } = [];

        /// <summary>The small things set about a camp (crates, barrels, carts).</summary>
        public IReadOnlyList<MapTools.Kind> Props { get; init; } = [];

        /// <summary>The floating islands rocks in the air are made of (FloatingIsland); none: the big rocks.</summary>
        public IReadOnlyList<MapTools.Kind> Islands { get; init; } = [];

        /// <summary>The cliff pieces steep ground is faced with (CliffPiece).</summary>
        public IReadOnlyList<MapTools.Kind> Cliffs { get; init; } = [];

        /// <summary>A catalog's kinds sorted into its Trees, Bushes, Rocks and Plants groups.</summary>
        public static Planting From(IEnumerable<MapTools.Kind> kinds)
        {
            var all = kinds.ToList();
            List<MapTools.Kind> Of(string group) => [.. all.Where(k => string.Equals(k.Category, group, StringComparison.OrdinalIgnoreCase))];
            return new(Of("Trees"), Of("Bushes"), Of("Rocks"), Of("Plants")) { Buildings = Of("Buildings"), Props = Of("Props"), Islands = Of(FloatingIsland.Category), Cliffs = Of(CliffPiece.Category) };
        }
    }

    /// <summary>
    /// Generates a zone (the layers' channels: Sand r, Dirt g, Rock b, as a new map's). Given its ground
    /// (<paramref name="given"/>: a heightmap made elsewhere, cols by rows metres), it is made over that ground instead
    /// of its own hills, like a sheet laid on it: Low and High stretch it (as <see cref="Measured"/> leaves them, not at
    /// all), the rest of the numbers as ever.
    /// </summary>
    public static Zone Generate(Spec spec, int cols, int rows, float x0, float y0, Planting planting, float[]? given = null)
    {
        var rng = new Random(spec.Seed);
        float[] h = new float[cols * rows];
        float Edge(int c, int r) => spec.Coast > 0 ? FarEdge(spec, c, r, cols, rows) : MathF.Min(MathF.Min(c, cols - 1 - c), MathF.Min(r, rows - 1 - r));
        // how far out of the rim a point is: under 1 in it (its width changing along the edge), 0 at the edge; no rim: 2
        float RimAt(int c, int r)
        {
            if (spec.RimHeight <= 0 || spec.RimWidth <= 0)
            {
                return 2;
            }
            float x = c + x0, y = r + y0;
            float wander = 1 + (0.6f * Fbm(x / (spec.RimWidth * 2.5f), y / (spec.RimWidth * 2.5f), spec.Seed + 7, 3));
            return Edge(c, r) / (spec.RimWidth * wander);
        }
        // the rolling ground: noise of the hills' size and smaller, stretched so that it reaches from Low to High (its
        // lowest and highest hundredth), more low ground than high (each pixel on its own: the rows in parallel)
        if (given == null)
        {
            var ridges = new Noise(spec.Seed + 19);
            Parallel.For(0, rows, r =>
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = (c + x0) / spec.HillSize, y = (r + y0) / spec.HillSize, v = Fbm(x, y, spec.Seed, 5);
                    // Ridges: that share of the hills sharp-crested (both about -1..1)
                    h[(r * cols) + c] = spec.Ridges > 0 ? v + (spec.Ridges * (RidgedMultifractal(ridges, x, y) - v)) : v;
                }
            });
        }
        var (lo, hi) = Spread(given ?? h);
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                float x = c + x0, y = r + y0;
                float under;
                if (given != null)
                {
                    // the ground given, its lowest and highest hundredth moved to Low and High (as measured: where they are)
                    under = hi - lo < 1e-3f ? given[(r * cols) + c] + (spec.Low - lo) : spec.Low + ((given[(r * cols) + c] - lo) * (spec.High - spec.Low) / (hi - lo));
                }
                else
                {
                    float t = Math.Clamp((h[(r * cols) + c] - lo) / MathF.Max(1e-4f, hi - lo), 0, 1);
                    under = spec.Low + ((spec.High - spec.Low) * MathF.Pow(t, 1.3f));
                }
                float ground = Shaped(spec, under, x, y);
                // the rim: a wall of ridged mountains round the edge, its foot wandering in and out
                float rim = 0;
                float d = RimAt(c, r);
                if (d < 1)
                {
                    // its height changes along the edge: high peaks in places, low hills or a gap in others
                    float tall = 0.5f + (0.5f * Smooth(-0.35f, 0.45f, Fbm(x / 900, y / 900, spec.Seed + 3, 2)));
                    float rise = MathF.Pow(Smooth(1, 0.25f, d), 1.25f);   // foothills first, then steep
                    float ridges = 0.6f + (0.4f * Ridged(x / 110, y / 110, spec.Seed + 11, 4));
                    rim = spec.RimHeight * tall * rise * ridges;
                }
                h[(r * cols) + c] = Marshed(spec, Fjorded(spec, Coasted(spec, ground + rim, c, r, cols, rows, x, y), c, r, cols, rows, x, y), x, y);
            }
        });
        var (worn, dunes) = Shape(spec, h, cols, rows, x0, y0);
        var lakes = PlaceLakes(spec, h, cols, rows, x0, y0, rng);
        var rivers = MakeRivers(spec, h, cols, rows, x0, y0, lakes, rng);
        bool[] wet = Wet(spec, h, cols, rows, x0, y0, rivers);
        float[] lakeIn = LakeField(lakes, cols, rows, x0, y0, MathF.Max(spec.Shore, 4) + 2);
        // camps and the roads between them: levelled ground, kept clear of the planting, the roads painted
        var camps = PlaceCamps(spec, h, cols, rows, lakeIn, wet, rng);
        float[] road = new float[cols * rows];
        var roads = spec.Roads ? MakeRoads(spec, h, cols, rows, x0, y0, camps, lakeIn, wet, road) : [];
        bool[] busy = Busy(cols, rows, camps, road, wet);
        // the cliffs before the woods and the paint: they step the ground (painted rock, nothing growing on the step)
        var cliffs = new List<(string, Vector3, float, float)>();
        Cliffs(spec, h, cols, rows, x0, y0, planting.Cliffs, rng, cliffs, (c, r) => !busy[(r * cols) + c] && lakeIn[(r * cols) + c] <= -2 && RimAt(c, r) > 0.8f);   // the rim's mountains past its foothills: no steps
        float[] wooded = Woods(spec, h, cols, rows, x0, y0, lakeIn, busy);
        byte[] mask = Paint(spec, h, cols, rows, x0, y0, lakeIn, wooded, wet, worn, dunes);
        PaintRoads(mask, road, camps, cols, rows, spec.RoadChannel == 0 ? 0 : 1);
        var objects = Plant(spec, h, cols, rows, x0, y0, lakeIn, wooded, planting, rng, busy);
        objects.AddRange(cliffs);
        Furnish(spec, h, cols, rows, x0, y0, camps, planting, rng, objects);
        Float(spec, h, cols, rows, x0, y0, planting, rng, objects);
        return new Zone(h, mask, objects, lakes, wooded) { Rivers = rivers, Roads = roads, Worn = worn };
    }

    /// <summary>The lowest and highest hundredth of some heights (sampled).</summary>
    private static (float Lo, float Hi) Spread(float[] h)
    {
        float[] sample = [.. h.Where((_, i) => i % 97 == 0).Order()];
        return (sample[sample.Length / 100], sample[sample.Length * 99 / 100]);
    }

    /// <summary>
    /// A style's numbers for ground made elsewhere (cols by rows metres): its lowest and highest ground and how far apart
    /// its hills are, measured; and nothing that would change its shape (no rim, coast, terraces, canyons, fjords,
    /// crags, strata, erosion, scree, expand, valleys, roughness, marsh, cliffs, lakes, rivers or camps: each can be asked for again), so the zone lies on it as it is.
    /// </summary>
    public static Spec Measured(Spec style, float[] ground, int cols, int rows)
    {
        var (lo, hi) = Spread(ground);
        // a wave's height spread over its steepness is about a quarter of its length (√2 × 4 × its spread over its slope)
        double sum = 0, sumSq = 0, slope = 0;
        int n = 0;
        for (int r = 0; r < rows - 1; r += 3)
        {
            for (int c = 0; c < cols - 1; c += 3)
            {
                float v = ground[(r * cols) + c];
                (sum, sumSq, n) = (sum + v, sumSq + ((double)v * v), n + 1);
                slope += MathF.Abs(ground[(r * cols) + c + 1] - v) + MathF.Abs(ground[((r + 1) * cols) + c] - v);
            }
        }
        double spread = n > 0 ? Math.Sqrt(Math.Max(0, (sumSq / n) - ((sum / n) * (sum / n)))) : 0, steep = n > 0 ? slope / (2 * n) : 0;
        float hills = steep > 1e-4 ? (float)Math.Clamp(4 * Math.Sqrt(2) * spread / steep, 50, 2000) : 2000;
        return style with
        {
            Low = lo,
            High = hi,
            HillSize = MathF.Round(hills / 25) * 25,
            RimHeight = 0,
            Coast = 0,
            CoastDry = false,
            Terraces = 0,
            Canyons = 0,
            Rough = 0,
            Marsh = 0,
            CliffSlope = 0,
            Fjords = 0,
            Crags = 0,
            Strata = 0,
            Erosion = 0,
            Talus = 0,
            Expand = 0,
            Valleys = 0,
            LakeHectares = [],
            Rivers = 0,
            Camps = 0,
        };
    }

    /// <summary>The lakes: each in the lowest open ground found away from the rim and the others, its bed carved.</summary>
    private static List<Lake> PlaceLakes(Spec spec, float[] h, int cols, int rows, float x0, float y0, Random rng)
    {
        var lakes = new List<Lake>();
        foreach (float ha in (spec.LakeHectares ?? []).OrderDescending())
        {
            float radius = MathF.Sqrt(ha * 10000 / MathF.PI), margin = (spec.RimHeight > 0 ? spec.RimWidth * 0.6f : 0) + radius + 30;   // at the rim's foot, not on it
            (Vector2 At, float Low)? best = null;
            for (int k = 0; k < 400; k++)
            {
                var at = new Vector2(margin + (rng.NextSingle() * (cols - (2 * margin))), margin + (rng.NextSingle() * (rows - (2 * margin))));
                if (cols <= 2 * margin || rows <= 2 * margin || lakes.Any(l => Vector2.Distance(l.Middle - new Vector2(x0, y0), at) < radius + Radius(l) + 60))
                {
                    continue;
                }
                float low = 0;
                for (int s = 0; s < 9; s++)
                {
                    float a = MathF.Tau * s / 9, d = s == 0 ? 0 : radius * 0.7f;
                    low += At(h, cols, rows, at.X + (d * MathF.Cos(a)), at.Y + (d * MathF.Sin(a)));
                }
                if (best == null || low / 9 < best.Value.Low)
                {
                    best = (at, low / 9);
                }
            }
            if (best is not { } b)
            {
                continue;
            }
            var outline = new Vector2[28];
            for (int i = 0; i < outline.Length; i++)
            {
                float a = MathF.Tau * i / outline.Length, wobble = 1 + (0.28f * Fbm((MathF.Cos(a) * 1.7f) + (lakes.Count * 13), MathF.Sin(a) * 1.7f, spec.Seed + 19, 3));
                outline[i] = b.At + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * wobble);
            }
            lakes.Add(CarveLake(h, cols, rows, x0, y0, b.At, outline, radius));
        }
        return lakes;
    }

    /// <summary>A lake's bed carved round its outline (pixels): its surface just under the lowest shore, so the water stays in.</summary>
    internal static Lake CarveLake(float[] h, int cols, int rows, float x0, float y0, Vector2 middle, Vector2[] outline, float radius)
    {
        // the bed down from the surface, shallow at the edge
        // the surface just under the lowest of the shore and the ground a few metres out (at each point of the outline and
        // between them): on a slope the ground just past a point, or between two, can lie lower than the point
        float level = float.MaxValue, depth = MathF.Min(8, 2 + (radius / 40));
        for (int i = 0; i < outline.Length; i++)
        {
            foreach (var p in new[] { outline[i], (outline[i] + outline[(i + 1) % outline.Length]) / 2 })
            {
                var outward = Vector2.Normalize(p - middle);
                foreach (float d in new[] { 0f, 3, 6 })
                {
                    level = MathF.Min(level, At(h, cols, rows, p.X + (outward.X * d), p.Y + (outward.Y * d)));
                }
            }
        }
        level -= 0.4f;
        // as far out as the banks may reach (the ground round a lake can stand far above its water)
        float reach = (radius * 1.4f) + MathF.Min(100, radius * 2);
        int c0 = Math.Max(0, (int)(middle.X - reach)), c1 = Math.Min(cols - 1, (int)(middle.X + reach));
        int r0 = Math.Max(0, (int)(middle.Y - reach)), r1 = Math.Min(rows - 1, (int)(middle.Y + reach));
        Parallel.For(r0, r1 + 1, r =>
        {
            for (int c = c0; c <= c1; c++)
            {
                float inside = InsideBy(outline, new Vector2(c, r));
                int p = (r * cols) + c;
                if (inside >= 0)
                {
                    h[p] = MathF.Min(h[p], level - (depth * Math.Clamp(inside / 15, 0.12f, 1)));
                }
                else
                {
                    // the banks slope down to the water (about 9°) near it, the ground as it was further out (nearer round a pond)
                    float bank = MathF.Min(h[p], level + 0.3f + (-inside * 0.15f));
                    h[p] = bank + ((h[p] - bank) * Smooth(MathF.Min(25, radius * 0.5f), MathF.Min(90, radius * 1.6f), -inside));
                }
            }
        });
        return new Lake(middle + new Vector2(x0, y0), [.. outline.Select(p => p + new Vector2(x0, y0))], level, depth);
    }

    private static float Radius(Lake l) => l.Outline.Max(p => Vector2.Distance(p, l.Middle));

    /// <summary>How wooded each point is (0..1): noise patches covering the asked share of the ground the woods may grow on.</summary>
    private static float[] Woods(Spec spec, float[] h, int cols, int rows, float x0, float y0, float[] lakeIn, bool[] wet)
    {
        float[] n = new float[cols * rows];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                n[(r * cols) + c] = Fbm((c + x0) / 160, (r + y0) / 160, spec.Seed + 31, 4);
            }
        });
        var growable = new List<float>();
        for (int r = 0; r < rows; r += 7)
        {
            for (int c = 0; c < cols; c += 7)
            {
                if (Grows(h, cols, rows, c, r, lakeIn, wet))
                {
                    growable.Add(n[(r * cols) + c]);
                }
            }
        }
        if (growable.Count == 0 || spec.Woods <= 0)
        {
            return new float[cols * rows];
        }
        growable.Sort();
        float cut = growable[Math.Clamp((int)((1 - spec.Woods) * growable.Count), 0, growable.Count - 1)];
        float[] w = new float[cols * rows];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                w[p] = Grows(h, cols, rows, c, r, lakeIn, wet) ? Smooth(cut - 0.03f, cut + 0.03f, n[p]) : 0;
            }
        });
        return w;
    }

    /// <summary>
    /// The cliffs: steep ground faced with cliff pieces. Each at the foot of a slope steeper than CliffSlope (found by
    /// walking down it from a steep point until it eases), facing down it, as tall as the slope's drop to where it eases
    /// above (its top just under the ground there), reaching back into the hill; close along the slope, overlapping. The
    /// ground inside each piece is filled up to the slope's top, so that the slope becomes a step: the hill's top runs out
    /// to the rock's face and the face drops from it (the piece's top under the grass).
    /// </summary>
    private static void Cliffs(Spec spec, float[] h, int cols, int rows, float x0, float y0, IReadOnlyList<MapTools.Kind> pieces, Random rng, List<(string, Vector3, float, float)> placed, Func<int, int, bool> free)
    {
        if (spec.CliffSlope <= 0 || pieces.Count == 0)
        {
            return;
        }
        float ease = spec.CliffSlope * 0.6f;
        // each judged on the ground as it was (a piece's fill makes a shelf, and the next one up would take it for a
        // step: pieces stacked up a mountainside); the fills go into h
        float[] g = (float[])h.Clone();
        Vector2 Down(Vector2 p)
        {
            var grad = new Vector2(At(g, cols, rows, p.X + 1, p.Y) - At(g, cols, rows, p.X - 1, p.Y), At(g, cols, rows, p.X, p.Y + 1) - At(g, cols, rows, p.X, p.Y - 1));
            return grad.LengthSquared() > 1e-8f ? -Vector2.Normalize(grad) : Vector2.Zero;
        }
        bool Inside(Vector2 p) => p.X >= 1 && p.Y >= 1 && p.X < cols - 2 && p.Y < rows - 2;
        // where the slope eases, walking one way from a point (at most 30 m), and whether it did
        (Vector2 At, bool Eased) Walk(Vector2 p, float way)
        {
            bool eased = false;
            for (int k = 0; k < 30 && Inside(p); k++)
            {
                var d = Down(p) * way;
                if (d == Vector2.Zero || Slope(g, cols, rows, (int)p.X, (int)p.Y) < ease)
                {
                    eased = true;
                    break;
                }
                p += d;
            }
            return (Vector2.Clamp(p, Vector2.One, new Vector2(cols - 2, rows - 2)), eased);
        }
        var middles = new List<(Vector2 At, float Width)>();
        var made = new List<(Vector2 At, float Yaw, float S, float Back)>();
        // whether a point lies in a piece's footprint (its width, from just before its face back to where its slope tops out)
        static bool Under((Vector2 At, float Yaw, float S, float Back) piece, Vector2 p)
        {
            float a = piece.Yaw * MathF.PI / 180;
            var front = new Vector2(MathF.Sin(a), -MathF.Cos(a));
            var d = p - piece.At;
            float lz = Vector2.Dot(d, front), lx = Vector2.Dot(d, new Vector2(-front.Y, front.X));
            return MathF.Abs(lx) <= CliffPiece.Width * piece.S / 2 && lz > -piece.Back && lz < 2 * piece.S;
        }
        // the ground inside a piece (behind its face, back to where the slope tops out: no further, or it would spill
        // over a ridge) raised to the slope's top
        void Fill(Vector2 at, float yaw, float s, int variant, float topHeight, float back)
        {
            var face = CliffPiece.Face(Math.Max(0, variant));
            // behind the face's deepest point (its top leans back), or the ground's step would stand in front of the rock;
            // found once a piece at 25 places across it
            float[] backs = [.. Enumerable.Range(0, 25).Select(i => Enumerable.Range(0, 25).Min(j => face(((i / 24f) - 0.5f) * CliffPiece.Width, j * CliffPiece.Height / 24)) - 1)];
            float Back(float lx) => backs[Math.Clamp((int)MathF.Round(((lx / CliffPiece.Width) + 0.5f) * 24), 0, 24)];
            float a = yaw * MathF.PI / 180;
            var front = new Vector2(MathF.Sin(a), -MathF.Cos(a));   // the face's way in pixels (editor z = -row)
            var side = new Vector2(-front.Y, front.X);
            float reach = (CliffPiece.Width * s / 2) + (CliffPiece.Depth * s);
            int c0 = Math.Max(0, (int)(at.X - reach)), c1 = Math.Min(cols - 1, (int)(at.X + reach)), r0 = Math.Max(0, (int)(at.Y - reach)), r1 = Math.Min(rows - 1, (int)(at.Y + reach));
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    var d = new Vector2(c, r) - at;
                    float lx = Vector2.Dot(d, side) / s, lz = Vector2.Dot(d, front) / s;
                    if (MathF.Abs(lx) <= CliffPiece.Width / 2 && lz * s > -back && lz < Back(lx))
                    {
                        h[(r * cols) + c] = MathF.Max(h[(r * cols) + c], topHeight);
                    }
                }
            }
        }
        // candidates every 3 m, jittered, in a shuffled order: only on cliff lines (not every steep bump)
        bool[] lines = CliffLines(spec, g, cols, rows, free);
        var candidates = new List<Vector2>();
        for (int r = 2; r < rows - 2; r += 3)
        {
            for (int c = 2; c < cols - 2; c += 3)
            {
                if (lines[((r / 3) * ((cols + 2) / 3)) + (c / 3)])
                {
                    candidates.Add(new Vector2(c + rng.NextSingle(), r + rng.NextSingle()));
                }
            }
        }
        foreach (var p in candidates.OrderBy(_ => rng.Next()))
        {
            var (foot, _) = Walk(p, 1);
            var (top, shelf) = Walk(p, -1);
            if (!shelf)
            {
                continue;   // still climbing 30 m up: a mountainside, no step to face (painted rock)
            }
            float drop = At(g, cols, rows, top.X, top.Y) - At(g, cols, rows, foot.X, foot.Y), run = Vector2.Distance(foot, top);
            var middle = (foot + top) / 2;
            if (drop < 6 || middles.Any(m => Vector2.Distance(m.At, middle) < m.Width * 0.3f))
            {
                continue;
            }
            // the slope's top: the lowest along the piece's width there (so that its top is under the ground all along);
            // the piece as tall as from there down to the foot, its top 0.4 m under the ground and its foot 1.5 m in it
            // (the ground behind it is filled, so it need not reach back)
            var d = Down(p);
            var across = new Vector2(-d.Y, d.X) * CliffPiece.Width * Math.Clamp((drop + 1.9f) / CliffPiece.Height, 0.3f, 6) * 0.4f;
            float topHeight = MathF.Min(At(g, cols, rows, top.X, top.Y), MathF.Min(At(g, cols, rows, Math.Clamp(top.X + across.X, 0, cols - 1), Math.Clamp(top.Y + across.Y, 0, rows - 1)), At(g, cols, rows, Math.Clamp(top.X - across.X, 0, cols - 1), Math.Clamp(top.Y - across.Y, 0, rows - 1))));
            drop = topHeight - At(g, cols, rows, foot.X, foot.Y);
            if (drop < 6 || drop + 1.9f > CliffPiece.Height * 6)
            {
                continue;
            }
            float s = MathF.Max(0.3f, (drop + 1.9f) / CliffPiece.Height);
            // a step: the ground behind its top about level, not falling away (a ridge: the rock would stand up out of
            // it) nor climbing on (a mountainside: pieces stacked up it)
            // (halfway back and at its back: the piece reaches back so far, and would show where the ground fell away)
            float Behind(float share)
            {
                var at = Vector2.Clamp(top - (d * CliffPiece.Depth * s * share), Vector2.Zero, new Vector2(cols - 1, rows - 1));
                return At(g, cols, rows, at.X, at.Y) - At(g, cols, rows, top.X, top.Y);
            }
            float rise = Behind(0.6f);
            if (MathF.Min(Behind(0.5f), Behind(1)) < -1.5f || rise > MathF.Max(3, drop * 0.3f))
            {
                continue;
            }
            var at = foot + (d * 0.5f);
            float yaw = (MathF.Atan2(d.X, -d.Y) * 180 / MathF.PI) + ((rng.NextSingle() - 0.5f) * 30);   // the face (+z) down the slope (editor z = -row), a little turned
            // not standing in another piece, nor another in it (one would bury the other's face)
            var piece = (at, yaw, s, run + 1.5f);
            if (made.Any(m => Vector2.DistanceSquared(m.At, at) < MathF.Pow((CliffPiece.Width * MathF.Max(m.S, s)) + m.Back + piece.Item4, 2) && (Under(m, at) || Under(piece, m.At))))
            {
                continue;
            }
            made.Add(piece);
            int k = rng.Next(pieces.Count);
            // its top just under the ground where the slope tops out (the ground's grass to the edge), its foot sunk
            placed.Add((pieces[k].Model, new Vector3(at.X + x0, topHeight - 0.4f - (CliffPiece.Height * s), -(at.Y + y0)), yaw, s));
            Fill(at, yaw, s, CliffPiece.VariantOf(pieces[k].Model), topHeight, run + 1.5f);
            middles.Add((middle, CliffPiece.Width * s));
        }
    }

    /// <summary>
    /// The cliff lines, on a 3 m grid ((cols + 2) / 3 across): cells steeper than CliffSlope on the ground blurred over
    /// 21 m (rough bumps are no cliffs), joined into bands that face within 45° of where each started (a cliff faces one
    /// way along it; the steep ring round a bump faces every way, and breaks into short arcs), the bands 50 m long or more.
    /// </summary>
    private static bool[] CliffLines(Spec spec, float[] h, int cols, int rows, Func<int, int, bool> free)
    {
        const int Cell = 3, Blur = 10, Long = 50;
        float turn = MathF.Cos(45 * MathF.PI / 180);
        float[] soft = BoxBlur(h, cols, rows, Blur);
        int gc = (cols + Cell - 1) / Cell, gr = (rows + Cell - 1) / Cell;
        bool[] steep = new bool[gc * gr], kept = new bool[gc * gr], seen = new bool[gc * gr];
        var facing = new Vector2[gc * gr];
        Parallel.For(0, gr, j =>
        {
            for (int i = 0; i < gc; i++)
            {
                int c = Math.Min(cols - 1, i * Cell), r = Math.Min(rows - 1, j * Cell);
                steep[(j * gc) + i] = Slope(soft, cols, rows, c, r) > spec.CliffSlope && free(c, r);
                var g = new Vector2(At(soft, cols, rows, c + 1, r) - At(soft, cols, rows, c - 1, r), At(soft, cols, rows, c, r + 1) - At(soft, cols, rows, c, r - 1));
                facing[(j * gc) + i] = g.LengthSquared() > 1e-8f ? Vector2.Normalize(g) : Vector2.Zero;
            }
        });
        var band = new List<int>();
        var queue = new Queue<int>();
        for (int start = 0; start < steep.Length; start++)
        {
            if (!steep[start] || seen[start])
            {
                continue;
            }
            // the band through it (8 neighbours), and how far it runs (its box's diagonal)
            band.Clear();
            queue.Enqueue(start);
            seen[start] = true;
            int i0 = int.MaxValue, i1 = int.MinValue, j0 = int.MaxValue, j1 = int.MinValue;
            while (queue.TryDequeue(out int k))
            {
                band.Add(k);
                int i = k % gc, j = k / gc;
                (i0, i1, j0, j1) = (Math.Min(i0, i), Math.Max(i1, i), Math.Min(j0, j), Math.Max(j1, j));
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di, nj = j + dj, n = (nj * gc) + ni;
                        if (ni >= 0 && nj >= 0 && ni < gc && nj < gr && steep[n] && !seen[n] && Vector2.Dot(facing[n], facing[start]) > turn)
                        {
                            seen[n] = true;
                            queue.Enqueue(n);
                        }
                    }
                }
            }
            if (MathF.Sqrt(((i1 - i0) * (i1 - i0)) + ((j1 - j0) * (j1 - j0))) * Cell >= Long)
            {
                band.ForEach(k => kept[k] = true);
            }
        }
        return kept;
    }

    /// <summary>The heights averaged over a square <paramref name="radius"/> pixels each way (rows, then columns).</summary>
    internal static float[] BoxBlur(float[] h, int cols, int rows, int radius)
    {
        float[] across = new float[h.Length], both = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                float sum = 0;
                int c0 = Math.Max(0, c - radius), c1 = Math.Min(cols - 1, c + radius);
                for (int k = c0; k <= c1; k++)
                {
                    sum += h[(r * cols) + k];
                }
                across[(r * cols) + c] = sum / (c1 - c0 + 1);
            }
        });
        Parallel.For(0, cols, c =>
        {
            for (int r = 0; r < rows; r++)
            {
                float sum = 0;
                int r0 = Math.Max(0, r - radius), r1 = Math.Min(rows - 1, r + radius);
                for (int k = r0; k <= r1; k++)
                {
                    sum += across[(k * cols) + c];
                }
                both[(r * cols) + c] = sum / (r1 - r0 + 1);
            }
        });
        return both;
    }

    /// <summary>Whether trees may stand at a point: not a cliff (woods climb steep hillsides), not in a lake.</summary>
    private static bool Grows(float[] h, int cols, int rows, int c, int r, float[] lakeIn, bool[] wet) =>
        !wet[(r * cols) + c] && Slope(h, cols, rows, c, r) < 45 && lakeIn[(r * cols) + c] <= -4;

    /// <summary>The layers: rock on steep ground, dirt under the woods (and a little in the open), sand on the shores.</summary>
    private static byte[] Paint(Spec spec, float[] h, int cols, int rows, float x0, float y0, float[] lakeIn, float[] wooded, bool[] wet, float[]? worn, float[]? dunes)
    {
        byte[] mask = new byte[cols * rows * 4];
        float open = Quantile(spec, cols, rows, x0, y0, 1 - spec.OpenDirt);
        // crests: the ground standing over its surroundings (a few metres' average over 30 m's), hollows under them
        bool relief = spec.Crests > 0 || spec.SnowSlip > 0;
        float[]? near = relief ? BoxBlur(h, cols, rows, 2) : null, far = relief ? BoxBlur(h, cols, rows, 15) : null;
        float[]? streams = spec.Streams > 0 ? Streams(h, cols, rows) : null;
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float slope = Slope(h, cols, rows, c, r), x = c + x0, y = r + y0;
                float rock = Smooth(spec.RockSlope, spec.RockSlope + 15, slope) * (1 - (0.6f * wooded[p]));   // steep grass too, and wooded slopes mostly earth
                // what the weather moved: the channels it cut bare rock, what it laid down (fans, valley fill, scree) earth
                float cut = worn == null ? 0 : Smooth(1.5f, 6, -worn[p]), laid = worn == null ? 0 : Smooth(1, 4, worn[p]);
                float stand = near == null || far == null ? 0 : near[p] - far[p], crest = spec.Crests * Smooth(0.5f, 3, stand);
                rock = MathF.Max(rock, MathF.Max(0.7f * cut, crest) * (1 - wooded[p]));
                float patches = Smooth(open - 0.04f, open + 0.04f, Fbm(x / 45, y / 45, spec.Seed + 41, 3));
                float stream = streams == null ? 0 : spec.Streams * streams[p];
                float dirt = MathF.Max(MathF.Max(MathF.Max(wooded[p] * 0.4f, patches * 0.9f), laid), stream) * (1 - rock);   // a forest floor, grass showing through
                // the first painted layer on the shores of the sea and the rivers (with a shore), snow above the snow line
                float sand = spec.Shore > 0 && (wet[p] || (!spec.CoastDry && h[p] < 2.5f)) ? 1 : 0;
                float snow = spec.SnowLine > 0 ? Smooth(spec.SnowLine - 12, spec.SnowLine + 12, h[p] + (15 * Fbm(x / 60, y / 60, spec.Seed + 61, 3))) * (1 - (0.5f * rock)) : 0;
                if (spec.SnowSlip > 0 && spec.SnowLine > 0)
                {
                    // slid off the steep ground, gathered in the hollows down to 25 m under the line
                    float hollow = Smooth(0.5f, 3, -stand);
                    snow = MathF.Max(snow, hollow * Smooth(spec.SnowLine - 37, spec.SnowLine - 13, h[p])) * (1 - Smooth(spec.SnowSlip - 5, spec.SnowSlip + 5, slope));
                }
                // a band along the lakes' shores, out to Shore metres and a little way into the water
                float d = -lakeIn[p];
                if (spec.Shore > 0 && d > -4 && d < spec.Shore)
                {
                    sand = MathF.Max(sand, 1 - Smooth(spec.Shore * 0.5f, spec.Shore, d));
                }
                sand = MathF.Max(sand, Smooth(0.2f, 0.6f, dunes?[p] ?? 0));   // the dunes' sand
                sand *= 1 - rock;
                if (spec.SnowChannel == 1)
                {
                    dirt = MathF.Max(dirt, snow);
                }
                else
                {
                    sand = MathF.Max(sand, snow);
                }
                mask[p * 4] = Byte(sand);
                mask[(p * 4) + 1] = Byte(dirt * (1 - sand));
                mask[(p * 4) + 2] = Byte(rock * (1 - snow));
                mask[(p * 4) + 3] = 255;
            }
        });
        return mask;
    }

    /// <summary>The trees (dense in the woods, a few in the open) and the small things (bushes and plants, rocks on steep ground).</summary>
    private static List<(string, Vector3, float, float)> Plant(Spec spec, float[] h, int cols, int rows, float x0, float y0, float[] lakeIn, float[] wooded, Planting kinds, Random rng, bool[] wet)
    {
        var placed = new List<(string, Vector3, float, float)>();
        float ha = cols * rows / 10000f;
        void Scatter(IReadOnlyList<MapTools.Kind> from, float perHa, Func<int, int, float> chance, float times = 1)
        {
            if (from.Count == 0 || perHa <= 0)
            {
                return;
            }
            int tries = (int)(perHa * ha);
            for (int k = 0; k < tries; k++)
            {
                float fc = rng.NextSingle() * (cols - 1), fr = rng.NextSingle() * (rows - 1);
                int c = (int)fc, r = (int)fr;
                if (rng.NextSingle() >= chance(c, r))
                {
                    continue;
                }
                // stands of one kind (as woods grow), a few of the others among them
                var kind = from.Count > 1 && rng.NextSingle() > 0.2f
                    ? from[Math.Clamp((int)(((Fbm(fc / 220, fr / 220, spec.Seed + 53 + from.Count, 2) * 0.5f) + 0.5f) * from.Count), 0, from.Count - 1)]
                    : from[rng.Next(from.Count)];
                float s = (kind.MinScale + (rng.NextSingle() * (kind.MaxScale - kind.MinScale))) * times;
                if (ReferenceEquals(from, kinds.Trees) && spec.Giants > 0 && rng.NextSingle() < spec.Giants)
                {
                    s *= spec.GiantScale * (0.85f + (0.3f * rng.NextSingle()));
                }
                // editor axes: x = X, z = -Y
                placed.Add((kind.Model, new Vector3(fc + x0, At(h, cols, rows, fc, fr), -(fr + y0)), rng.NextSingle() * 360, s));
            }
        }
        bool Dry(int c, int r) => !wet[(r * cols) + c] && lakeIn[(r * cols) + c] <= -2;
        Scatter(kinds.Trees, spec.WoodDensity, (c, r) => wooded[(r * cols) + c], spec.TreeScale);
        Scatter(kinds.Trees, spec.OpenTrees, (c, r) => wooded[(r * cols) + c] < 0.5f && Slope(h, cols, rows, c, r) < 25 && Dry(c, r) ? 1 : 0, spec.TreeScale);
        Scatter(kinds.Bushes, spec.Small * 0.35f, (c, r) => Dry(c, r) && Slope(h, cols, rows, c, r) < 30 ? 0.3f + (0.7f * wooded[(r * cols) + c]) : 0);
        Scatter(kinds.Plants, spec.Small * 0.45f, (c, r) => Dry(c, r) && Slope(h, cols, rows, c, r) < 25 ? 1 - (0.6f * wooded[(r * cols) + c]) : 0);
        Scatter(kinds.Rocks, spec.Small * 0.2f, (c, r) => Dry(c, r) ? 0.15f + (0.85f * Smooth(15, 35, Slope(h, cols, rows, c, r))) : 0);
        return placed;
    }

    /// <summary>How far inside the nearest lake each point is (metres; negative outside), worked out only within reach of a lake (further: far outside).</summary>
    private static float[] LakeField(List<Lake> lakes, int cols, int rows, float x0, float y0, float reach)
    {
        float[] f = new float[cols * rows];
        Array.Fill(f, -1e9f);
        foreach (var l in lakes)
        {
            int c0 = Math.Max(0, (int)(l.Outline.Min(p => p.X) - x0 - reach)), c1 = Math.Min(cols - 1, (int)(l.Outline.Max(p => p.X) - x0 + reach));
            int r0 = Math.Max(0, (int)(l.Outline.Min(p => p.Y) - y0 - reach)), r1 = Math.Min(rows - 1, (int)(l.Outline.Max(p => p.Y) - y0 + reach));
            Parallel.For(r0, r1 + 1, r =>
            {
                for (int c = c0; c <= c1; c++)
                {
                    int p = (r * cols) + c;
                    f[p] = MathF.Max(f[p], InsideBy(l.Outline, new Vector2(c + x0, r + y0)));
                }
            });
        }
        return f;
    }

    /// <summary>The ground's slope at a point, degrees.</summary>
    internal static float Slope(float[] h, int cols, int rows, int c, int r)
    {
        int cl = Math.Max(0, c - 1), cr = Math.Min(cols - 1, c + 1), ru = Math.Max(0, r - 1), rd = Math.Min(rows - 1, r + 1);
        float dx = (h[(r * cols) + cr] - h[(r * cols) + cl]) / Math.Max(1, cr - cl), dy = (h[(rd * cols) + c] - h[(ru * cols) + c]) / Math.Max(1, rd - ru);
        return MathF.Atan(MathF.Sqrt((dx * dx) + (dy * dy))) * 180 / MathF.PI;
    }

    /// <summary>The height between the pixels.</summary>
    internal static float At(float[] h, int cols, int rows, float c, float r)
    {
        c = Math.Clamp(c, 0, cols - 1.001f);
        r = Math.Clamp(r, 0, rows - 1.001f);
        int ci = (int)c, ri = (int)r;
        float tc = c - ci, tr = r - ri;
        float a = h[(ri * cols) + ci], b = h[(ri * cols) + ci + 1], d = h[((ri + 1) * cols) + ci], e = h[((ri + 1) * cols) + ci + 1];
        return ((a * (1 - tc)) + (b * tc)) * (1 - tr) + (((d * (1 - tc)) + (e * tc)) * tr);
    }

    /// <summary>How far inside an outline a point is (metres; negative outside), from its nearest edge.</summary>
    internal static float InsideBy(Vector2[] outline, Vector2 p) => MapWater.InsideBy(outline, p);

    /// <summary>Smooth value noise in octaves, about -1..1.</summary>
    internal static float Fbm(float x, float y, int seed, int octaves)
    {
        float v = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            v += amp * Noise(x, y, seed + (i * 101));
            norm += amp;
            (x, y, amp) = ((x * 2.03f) + 17.1f, (y * 2.03f) - 9.7f, amp * 0.5f);
        }
        return v / norm;
    }

    /// <summary>
    /// Musgrave's ridged multifractal over gradient noise, about -1..1: each octave's crest (one less the noise's size,
    /// squared) weighted by the crest of the octave under it, so finer ridges run along the bigger ones and the valleys
    /// between stay smooth (Ebert et al., Texturing and Modeling, the ridged multifractal terrain).
    /// </summary>
    private static float RidgedMultifractal(Noise noise, float x, float y)
    {
        const float Gain = 2;
        float sum = 0, weight = 1, amp = 1, norm = 0;
        for (int i = 0; i < 6; i++)
        {
            float crest = 1 - MathF.Abs(noise.Noise2(x, y));
            crest *= crest * weight;
            weight = Math.Clamp(crest * Gain, 0, 1);
            (sum, norm) = (sum + (crest * amp), norm + amp);
            (x, y, amp) = ((x * 2.1f) + 5.3f, (y * 2.1f) - 3.1f, amp * 0.5f);
        }
        return (2 * sum / norm) - 1;
    }

    /// <summary>Ridged noise 0..1: sharp crests where the noise crosses zero (mountain ridges).</summary>
    private static float Ridged(float x, float y, int seed, int octaves)
    {
        float v = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1 - MathF.Abs(Noise(x, y, seed + (i * 53)));
            v += amp * n * n;
            norm += amp;
            (x, y, amp) = (x * 2.1f, y * 2.1f, amp * 0.5f);
        }
        return v / norm;
    }

    /// <summary>
    /// Gradient noise (Perlin's: a slope of its own at each lattice point, blended with his quintic fade), scaled to the
    /// spread and steepness value noise had (about -1.4..1.4, mostly within ±0.45): value noise's lattice showed as squares
    /// on big hills.
    /// </summary>
    /// <summary>Sixteen ways round, as (x, y) pairs: <see cref="Noise"/>'s slopes.</summary>
    private static readonly float[] Gradients = [.. Enumerable.Range(0, 32).Select(i => i % 2 == 0 ? MathF.Cos(i / 2 * MathF.PI / 8) : MathF.Sin(i / 2 * MathF.PI / 8))];

    private static float Noise(float x, float y, int seed)
    {
        (x, y) = (x * 0.58f, y * 0.58f);   // as steep on average as value noise was: the same hills at the same HillSize
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        float Corner(int cx, int cy, float dx, float dy)
        {
            int g = (int)((Hash(cx, cy, seed) + 1) * 8) & 15;
            return (Gradients[g * 2] * dx) + (Gradients[(g * 2) + 1] * dy);
        }
        static float Fade(float t) => t * t * t * ((t * ((t * 6) - 15)) + 10);
        float ux = Fade(fx), uy = Fade(fy), a = Corner(xi, yi, fx, fy), b = Corner(xi + 1, yi, fx - 1, fy);
        float c = Corner(xi, yi + 1, fx, fy - 1), d = Corner(xi + 1, yi + 1, fx - 1, fy - 1);
        return 2 * (a + ((b - a) * ux) + ((c - a) * uy) + ((a - b - c + d) * ux * uy));
    }

    private static float Hash(int x, int y, int seed)
    {
        uint h = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
        h = (h ^ (h >> 13)) * 1274126177;
        return (((h ^ (h >> 16)) & 0xFFFF) / 32767.5f) - 1f;
    }

    private static float Smooth(float e0, float e1, float v)
    {
        float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
