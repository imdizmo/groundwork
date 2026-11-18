using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>The shapes some zones have beyond rolling hills: shelves, canyons, fjords, a coast or the world's edge, rivers, rocks in the air, snow.</summary>
public static partial class ZoneGenerator
{
    /// <summary>The rolling ground stepped into shelves (where the terraces reach) and cut by canyons.</summary>
    private static float Shaped(Spec spec, float ground, float x, float y)
    {
        if (spec.Rough > 0)
        {
            ground += spec.Rough * 2.5f * Fbm(x / 30, y / 30, spec.Seed + 101, 3);   // Fbm stays mostly within ±0.4
        }
        if (spec.Terraces > 0)
        {
            // a shelf every Terraces metres, flat for most of the step, then a steep riser; the steps wander a little
            float wander = 0.25f * Fbm(x / 140, y / 140, spec.Seed + 71, 3);
            float v = (ground / spec.Terraces) + wander, k = MathF.Floor(v), f = v - k;
            float stepped = (k + Smooth(0.72f, 0.95f, f) - wander) * spec.Terraces;
            float where = spec.TerraceShare >= 1 ? 1 : Smooth(-0.05f, 0.05f, Fbm(x / 500, y / 500, spec.Seed + 73, 2) - Quantile01(1 - spec.TerraceShare));
            ground += (stepped - ground) * where;
        }
        if (spec.Canyons > 0)
        {
            // where the noise crosses zero: winding lines about CanyonSpacing apart, flat-floored, steep-walled
            float n = MathF.Abs(Fbm(x / spec.CanyonSpacing, y / spec.CanyonSpacing, spec.Seed + 77, 3));
            float half = spec.CanyonWidth / spec.CanyonSpacing * 0.22f;
            ground -= spec.Canyons * Smooth(half * 2.2f, half, n);
        }
        return ground;
    }

    /// <summary>About the value of the noise a share of the ground lies under (Fbm is roughly even about 0, mostly within ±0.5).</summary>
    private static float Quantile01(float share) => (Math.Clamp(share, 0, 1) - 0.5f) * 0.9f;

    /// <summary>A noise level that the share asked of the map's open-ground patch noise lies under (sampled).</summary>
    private static float Quantile(Spec spec, int cols, int rows, float x0, float y0, float share)
    {
        var sample = new List<float>();
        for (int r = 0; r < rows; r += 9)
        {
            for (int c = 0; c < cols; c += 9)
            {
                sample.Add(Fbm((c + x0) / 45, (r + y0) / 45, spec.Seed + 41, 3));
            }
        }
        sample.Sort();
        return share >= 1 ? float.MaxValue : sample[Math.Clamp((int)(share * sample.Count), 0, sample.Count - 1)];
    }

    /// <summary>How far a point is from the coast's edge, wandering in and out (metres).</summary>
    private static float FromCoast(Spec spec, int c, int r, int cols, int rows, float x, float y)
    {
        float edges = MathF.Min(MathF.Min(c, cols - 1 - c), MathF.Min(r, rows - 1 - r));
        float d = spec.CoastSide switch
        {
            "east" => cols - 1 - c,
            "north" => r,
            "south" => rows - 1 - r,
            // an island: the sea all round
            "all round" => (1 - MathF.Sqrt(MathF.Pow((c - (cols / 2f)) / (cols / 2f), 2) + MathF.Pow((r - (rows / 2f)) / (rows / 2f), 2))) * MathF.Min(cols, rows) / 2,
            // a continent's corner: the sea to the west and the south
            "south-west" => MathF.Min(c, rows - 1 - r),
            // an archipelago: land where a broad, slow field is high, and the sea at every edge
            "islands" => MathF.Min(edges, spec.Coast * 4 * (Fbm(x / (MathF.Max(cols, rows) / 6f), y / (MathF.Max(cols, rows) / 6f), spec.Seed + 91, 4) + 0.04f)),
            _ => c,
        };
        // bays and headlands a few hundred metres across, however wide the coast is
        float across = MathF.Min(spec.Coast * 2.5f, 700), deep = MathF.Min(spec.Coast * 0.35f, 220);
        d += deep * Fbm(x / across, y / across, spec.Seed + 83, 3) / 0.4f;
        // an island's bays wander, but its land never reaches the map's edge
        return spec.CoastSide is "all round" or "islands" ? MathF.Min(d, edges * 2) : d;
    }

    /// <summary>The distance to the nearest edge that is not the coast's (the rim stands only there).</summary>
    private static float FarEdge(Spec spec, int c, int r, int cols, int rows)
    {
        float d = float.MaxValue;
        if (spec.CoastSide is "all round" or "islands")
        {
            return d;
        }
        if (spec.CoastSide is not ("west" or "south-west"))
        {
            d = MathF.Min(d, c);
        }
        if (spec.CoastSide != "east")
        {
            d = MathF.Min(d, cols - 1 - c);
        }
        if (spec.CoastSide != "north")
        {
            d = MathF.Min(d, r);
        }
        if (spec.CoastSide is not ("south" or "south-west"))
        {
            d = MathF.Min(d, rows - 1 - r);
        }
        return d;
    }

    /// <summary>The ground going down to the sea floor towards the coast's edge, or over a cliff into nothing (CoastDry).</summary>
    private static float Coasted(Spec spec, float ground, int c, int r, int cols, int rows, float x, float y)
    {
        if (spec.Coast <= 0)
        {
            return ground;
        }
        float d = FromCoast(spec, c, r, cols, rows, x, y);
        if (spec.CoastDry)
        {
            // the world's edge: the ground runs on, then breaks off in a cliff (a band of 15 to 60 m)
            float edge = spec.Coast * 0.3f, band = Math.Clamp(spec.Coast * 0.02f, 15, 60);
            float t = Smooth(edge + band, edge, d);
            return ground + ((-400 - ground) * t);
        }
        // a beach: the ground eases down to the water, then the sea floor drops away
        float down = Smooth(spec.Coast, 0, d);
        return ground + ((-spec.CoastDepth - ground) * MathF.Pow(down, 0.8f));
    }

    /// <summary>
    /// Fjords: winding, branching inlets cut into the high ground from the coast (or every edge, with none): broken walls
    /// of rock (ribs and chutes running down the faces, crags, buttresses and gullies) down to a flat floor Fjords metres under
    /// the sea at the mouth, rising inland (up to the highest ground at the far side, so each ends where its floor meets
    /// the ground) as it narrows to half its width.
    /// </summary>
    private static float Fjorded(Spec spec, float ground, int c, int r, int cols, int rows, float x, float y)
    {
        if (spec.Fjords <= 0)
        {
            return ground;
        }
        float t = spec.Coast > 0
            ? Math.Clamp(FromCoast(spec, c, r, cols, rows, x, y) / (spec.CoastSide is "north" or "south" ? rows : cols), 0, 1)
            : Math.Clamp(MathF.Min(MathF.Min(c, cols - 1 - c), MathF.Min(r, rows - 1 - r)) * 2 / MathF.Min(cols, rows), 0, 1);
        float floor = -spec.Fjords + ((spec.Fjords + spec.High) * t * t);
        float half = spec.FjordWidth / spec.FjordSpacing * 0.22f * (1 - (0.5f * t));
        // where the noise crosses zero, as the canyons; its edge pushed in and out a hundred metres or so: buttresses and
        // gullies along the walls, which reach out five times the floor's half-width beyond it
        float Across(float px, float py) => MathF.Abs(Fbm(px / spec.FjordSpacing, py / spec.FjordSpacing, spec.Seed + 113, 3)) + (half * 1.4f * Fbm(px / 90, py / 90, spec.Seed + 117, 3));
        float n = Across(x, y);
        // the wall above the sea (its share of the drop) over the outer three fifths of its width, a sharp lip at the top;
        // the rest straight down under the water, where it is not seen. In places it eases out at its foot onto a strip of
        // shore a couple of metres above the sea; elsewhere it goes on steeply into the water
        float shore = Smooth(-0.05f, 0.1f, Fbm(x / 500, y / 500, spec.Seed + 127, 2)), lift = 2.5f * shore;
        float drop = MathF.Max(0, ground - floor), above = drop > 0 ? Math.Clamp((ground - lift) / drop, 0, 1) : 0;
        float u = Math.Clamp(((half * 6) - n) / (half * 5), 0, 1), w = MathF.Min(1, u / 0.6f);
        float shaped = (w * (1.5f - (0.5f * w))) + (shore * w * (0.5f - (0.5f * w)));
        float cut = u < 0.6f ? above * shaped : above + ((1 - above) * (u - 0.6f) / 0.4f);
        float wall = ground - (drop * cut);
        if (cut is <= 0 or >= 1)
        {
            return wall;
        }
        // the face's grain: crags every way (a few metres, 20 m or so across), and ribs and chutes running down it, a few
        // tens of metres apart, cut into it, never standing out of it (ridged noise stretched four times down the fall
        // line, which is across the noise's lines: of eight set ways, the two nearest blended)
        float ny = Across(x, y + 2) - n, nx = Across(x + 2, y) - n;
        float theta = MathF.Atan2(ny, nx), k = ((theta * 8 / MathF.PI % 8) + 8) % 8;
        // only on the steep faces (the wall's own slope, from how fast the cut changes across it): none on its gentle foot
        float steep = drop * (u < 0.6f ? above * (1.5f - w + (shore * (0.5f - w))) / 0.6f : (1 - above) / 0.4f) / (5 * half) * MathF.Sqrt((nx * nx) + (ny * ny)) / 2;
        int k0 = (int)k;
        float Grooves(int o)
        {
            float a = o % 8 * MathF.PI / 8, gw = 25, stretch = 4;
            var (cs, sn) = (MathF.Cos(a), MathF.Sin(a));
            return Ridged(((-x * sn) + (y * cs)) / gw, ((x * cs) + (y * sn)) / (gw * stretch), spec.Seed + 125 + (o % 8 * 7), 2);
        }
        float grooves = Grooves(k0) + ((Grooves(k0 + 1) - Grooves(k0)) * (k - k0));
        float face = Smooth(0.6f, 1.2f, steep) * Smooth(1, 0.94f, cut);
        float carved = wall + (face * ((7 * Fbm(x / 18, y / 18, spec.Seed + 123, 3) / 0.4f) + (40 * (grooves - 1))));
        // no chute cut below the shore or the sea's edge (no trench between the wall and the land at its foot), and
        // nothing raised out of the water under it
        return wall >= lift ? MathF.Max(carved, MathF.Min(wall, lift + 0.5f)) : MathF.Min(carved, wall);
    }

    /// <summary>
    /// With fjords, the ground under the sea's level that the open sea (from the map's edge) does not reach raised to a
    /// damp flat just over it: a winding arm's floor rises inland and can dip again where the arm turns back to the coast,
    /// and would leave a pool cut off at its head. Not with a marsh, whose pools are meant.
    /// </summary>
    private static void Unpooled(Spec spec, float[] h, int cols, int rows)
    {
        if (spec.Fjords <= 0 || spec.Marsh > 0)
        {
            return;
        }
        bool[] open = new bool[h.Length];
        var stack = new Stack<int>();
        for (int i = 0; i < h.Length; i++)
        {
            int c = i % cols, r = i / cols;
            if (h[i] < 0 && (c == 0 || r == 0 || c == cols - 1 || r == rows - 1))
            {
                open[i] = true;
                stack.Push(i);
            }
        }
        while (stack.TryPop(out int i))
        {
            int c = i % cols;
            foreach (int j in new[] { c > 0 ? i - 1 : -1, c < cols - 1 ? i + 1 : -1, i - cols, i + cols })
            {
                if (j >= 0 && j < h.Length && !open[j] && h[j] < 0)
                {
                    open[j] = true;
                    stack.Push(j);
                }
            }
        }
        Parallel.For(0, h.Length, i => h[i] = h[i] < 0 && !open[i] ? 0.3f : h[i]);
    }

    /// <summary>Low ground (under Marsh metres) flattened towards wet flats, pools of it dipping under the sea's level (0).</summary>
    private static float Marshed(Spec spec, float ground, float x, float y)
    {
        if (spec.Marsh <= 0 || ground >= spec.Marsh || ground < -2)
        {
            return ground;
        }
        float low = Smooth(spec.Marsh, spec.Marsh * 0.35f, ground);
        float n = Fbm(x / 70, y / 70, spec.Seed + 97, 4), q = Quantile01(1 - spec.MarshShare);
        float flat = 0.6f + (0.8f * Smooth(-0.3f, 0.3f, n));   // the flats: a metre or so above the water
        float pool = Smooth(q - 0.03f, q + 0.03f, n);
        ground += (flat - ground) * low * 0.8f;
        return ground + ((-1.2f - ground) * pool * low);
    }

    /// <summary>
    /// Rivers: each from high open ground, the way water runs from there (<see cref="Flow"/>): downhill round the hills
    /// until it reaches a lake, the sea, the edge or an earlier river. Its surface falls along it, never rising, just
    /// under the ground; it is kept as stretches whose surfaces fall evenly (as the map's rivers are), each as long as
    /// that stays within a few centimetres of the ground's fall, its bed carved under them and its banks eased down to it.
    /// </summary>
    private static List<River> MakeRivers(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes, Random rng)
    {
        var all = new List<River>();
        if (spec.Rivers <= 0)
        {
            return all;
        }
        var made = new List<(List<Vector2> Path, float[] Surface)>();
        var (down, ends, run, clean, w) = Flow(spec, h, cols, rows, x0, y0, lakes);
        float margin = spec.RimHeight > 0 ? spec.RimWidth * 0.5f : 20;
        for (int n = 0; n < spec.Rivers; n++)
        {
            // the source: of a few open places, not too steep, away from the edge and the other rivers, the one whose water
            // runs furthest before it pools (further still counted for one reaching its end without pooling)
            Vector2? start = null;
            float best = float.MinValue;
            for (int k = 0; k < 400; k++)
            {
                var at = new Vector2(margin + (rng.NextSingle() * (cols - (2 * margin))), margin + (rng.NextSingle() * (rows - (2 * margin))));
                if (cols <= 2 * margin || rows <= 2 * margin)
                {
                    break;
                }
                int cell = ((int)(at.Y / FlowStep) * w) + (int)(at.X / FlowStep);
                float score = run[cell] * (clean[cell] ? 1.5f : 1);
                if (At(h, cols, rows, at.X, at.Y) > 1 && score > best && down[cell] >= 0 && Slope(h, cols, rows, (int)at.X, (int)at.Y) < 20 && !made.Any(r => r.Path.Any(p => Vector2.Distance(p, at) < 200)))
                {
                    (start, best) = (at, score);
                }
            }
            if (start is not Vector2 s0)
            {
                continue;
            }
            // down the flow to where it ends, eased round its corners, a point every 12 m
            var cells = new List<Vector2>();
            int from = ((int)(s0.Y / FlowStep) * w) + (int)(s0.X / FlowStep);
            float endLevel = ends[from];
            for (int cell = from; cell >= 0; cell = down[cell])
            {
                cells.Add(new Vector2(cell % w, cell / w) * FlowStep);
            }
            for (int pass = 0; pass < 4; pass++)
            {
                cells = [cells[0], .. Enumerable.Range(1, Math.Max(0, cells.Count - 2)).Select(i => (cells[i - 1] + (2 * cells[i]) + cells[i + 1]) / 4), cells[^1]];
            }
            var path = new List<Vector2> { s0 };
            for (int i = 1; i < cells.Count; i++)
            {
                for (float gone = Vector2.Distance(path[^1], cells[i]); gone >= 12; gone = Vector2.Distance(path[^1], cells[i]))
                {
                    path.Add(path[^1] + (Vector2.Normalize(cells[i] - path[^1]) * 12));
                }
            }
            path.Add(Vector2.Clamp(cells[^1], Vector2.Zero, new Vector2(cols - 1, rows - 1)));
            if (spec.Meander > 0)
            {
                path = Meandered(path, spec.Meander * spec.RiverWidth * 3, spec.RiverWidth * 14, spec.Seed + (31 * n), cols, rows);
            }
            // it joins an earlier river where it meets it
            for (int i = 1; i < path.Count; i++)
            {
                var meets = made.SelectMany(r => r.Path.Select((p, j) => (At: p, Level: r.Surface[j]))).Where(q => Vector2.Distance(q.At, path[i]) < spec.RiverWidth * 1.5f).Take(1).ToList();
                if (meets.Count > 0)
                {
                    path.RemoveRange(i + 1, path.Count - i - 1);
                    (path[i], endLevel) = (meets[0].At, meets[0].Level);
                    break;
                }
            }
            if (path.Count < 6)
            {
                continue;
            }
            // the surface: under the ground along it, never rising, not below where it ends. Where the way on climbs out of
            // a hollow by more than a few metres, the water pools instead: the river ends there in a pond.
            float[] surface = new float[path.Count];
            int lowest = 0;
            for (int i = 0; i < path.Count; i++)
            {
                float g = At(h, cols, rows, path[i].X, path[i].Y) - 0.8f;
                if (i > 0 && g > surface[i - 1] + 3)
                {
                    path.RemoveRange(lowest + 1, path.Count - lowest - 1);
                    if (path.Count >= 6)
                    {
                        float r = MathF.Max(10, spec.RiverWidth * 2.5f);
                        Vector2[] ring = [.. Enumerable.Range(0, 20).Select(k => path[^1] + (new Vector2(MathF.Cos(MathF.Tau * k / 20), MathF.Sin(MathF.Tau * k / 20)) * r * (1 + (0.2f * Fbm(k * 0.3f, n, spec.Seed + 23, 2)))))];
                        var pond = CarveLake(h, cols, rows, x0, y0, path[^1], ring, r);
                        lakes.Add(pond);
                        endLevel = pond.Level;
                    }
                    break;
                }
                surface[i] = i == 0 ? g : MathF.Min(surface[i - 1] - 0.01f, g);
                lowest = surface[i] <= surface[lowest] ? i : lowest;
            }
            if (path.Count < 6)
            {
                continue;
            }
            surface = surface[..path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                surface[i] = MathF.Max(surface[i], endLevel);
            }
            // stretches falling evenly: each as long (up to a dozen points) as the even fall keeps the water no more than half a
            // metre over the surface (still under the ground) and the bed no more than 1.5 m deeper
            float half = spec.RiverWidth / 2;
            var stretches = WaterFinder.Stretches(surface);
            float[] even = new float[path.Count];
            foreach (var (a, b) in stretches)
            {
                for (int i = a; i <= b; i++)
                {
                    even[i] = surface[a] + ((surface[b] - surface[a]) * (i - a) / Math.Max(1, b - a));
                }
            }
            Carve(h, cols, rows, path, even, half, MathF.Max(1.2f, half * 0.35f));
            made.Add((path, even));
            foreach (var (a, b) in stretches)
            {
                float hw(int i) => half * (0.8f + (0.4f * (i / (float)(path.Count - 1))));   // wider downstream
                all.Add(new River([.. Enumerable.Range(a, b - a + 1).Select(i => new MapWater.Point(path[i].X + x0, path[i].Y + y0, hw(i), MathF.Max(1.2f, half * 0.35f), 1.2f))], even[a], even[b]));
            }
        }
        return all;
    }

    /// <summary>
    /// A river's line winding from side to side: each point moved across it by up to <paramref name="swing"/> metres, in
    /// bends about <paramref name="bend"/> metres long whose size and length wander (noise), straight at both ends so it
    /// still starts and ends where it did; then a point every 12 m along it again.
    /// </summary>
    private static List<Vector2> Meandered(List<Vector2> path, float swing, float bend, int seed, int cols, int rows)
    {
        // a point every 3 m first, so the bends are drawn smoothly
        var fine = Along(path, 3);
        float total = (fine.Count - 1) * 3, gone = 0, phase = 0;
        var moved = new List<Vector2>(fine.Count);
        for (int i = 0; i < fine.Count; i++)
        {
            var dir = fine[Math.Min(fine.Count - 1, i + 1)] - fine[Math.Max(0, i - 1)];
            var across = dir.LengthSquared() > 0 ? Vector2.Normalize(new Vector2(-dir.Y, dir.X)) : Vector2.Zero;
            float ends = Smooth(0, 60, gone) * Smooth(total, total - 60, gone);   // straight for the first and last 60 m
            float size = 0.6f + (0.4f * Fbm(gone / (bend * 3), 0, seed, 2));
            moved.Add(Vector2.Clamp(fine[i] + (across * swing * size * ends * MathF.Sin(phase)), Vector2.Zero, new Vector2(cols - 1, rows - 1)));
            phase += MathF.Tau * 3 / (bend * (1 + (0.3f * Fbm(gone / bend, 7, seed + 1, 2))));
            gone += 3;
        }
        return Along(moved, 12);
    }

    /// <summary>A line with a point every <paramref name="step"/> metres along it, from its first point to its last.</summary>
    private static List<Vector2> Along(List<Vector2> line, float step)
    {
        var points = new List<Vector2> { line[0] };
        float carry = 0;
        for (int i = 1; i < line.Count; i++)
        {
            var (a, b) = (line[i - 1], line[i]);
            float len = Vector2.Distance(a, b);
            for (float t = step - carry; t <= len; t += step)
            {
                points.Add(Vector2.Lerp(a, b, t / len));
            }
            carry = len <= 0 ? carry : (carry + len) % step;
        }
        if (Vector2.Distance(points[^1], line[^1]) > step * 0.3f)
        {
            points.Add(line[^1]);
        }
        return points;
    }

    /// <summary>The flow's cells, in pixels.</summary>
    private const int FlowStep = 4;

    /// <summary>
    /// Where water runs from each place: a flood from where rivers end (the lakes and the sea, else the map's edge), lowest
    /// first, over cells of <see cref="FlowStep"/> pixels; each cell keeps the one it was reached from (downstream; -1 where
    /// rivers end), the level the river ends at, how far it runs before it pools (pixels: to a hollow more than 3 m under
    /// the lip out of it, where <see cref="MakeRivers"/> ends it in a pond) and whether it gets to its end without pooling.
    /// Followed down, a path runs downhill round the hills, and out of a hollow only over its lowest lip.
    /// </summary>
    private static (int[] Down, float[] End, float[] Run, bool[] Clean, int W) Flow(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<Lake> lakes)
    {
        int w = ((cols - 1) / FlowStep) + 1, ht = ((rows - 1) / FlowStep) + 1;
        int[] down = new int[w * ht];
        Array.Fill(down, -2);
        float[] end = new float[w * ht], run = new float[w * ht];
        bool[] clean = new bool[w * ht];
        var queue = new PriorityQueue<int, (float Level, float Ground)>();
        float G(int i) => h[(Math.Min(rows - 1, i / w * FlowStep) * cols) + Math.Min(cols - 1, i % w * FlowStep)];
        void Outlet(int i, float level, float endLevel)
        {
            if (down[i] == -2)
            {
                (down[i], end[i], clean[i]) = (-1, endLevel, true);
                queue.Enqueue(i, (level, G(i)));
            }
        }
        foreach (var l in lakes)
        {
            int c0 = Math.Max(0, (int)((l.Outline.Min(p => p.X) - x0) / FlowStep)), c1 = Math.Min(w - 1, (int)((l.Outline.Max(p => p.X) - x0) / FlowStep) + 1);
            int r0 = Math.Max(0, (int)((l.Outline.Min(p => p.Y) - y0) / FlowStep)), r1 = Math.Min(ht - 1, (int)((l.Outline.Max(p => p.Y) - y0) / FlowStep) + 1);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    if (InsideBy(l.Outline, new Vector2((c * FlowStep) + x0, (r * FlowStep) + y0)) > 2)
                    {
                        Outlet((r * w) + c, l.Level, l.Level);
                    }
                }
            }
        }
        // the sea: any ground under its level (0) unless the edge is a drop, a coast or not
        for (int i = 0; !spec.CoastDry && i < w * ht; i++)
        {
            if (G(i) < -0.5f)
            {
                Outlet(i, G(i), 0);
            }
        }
        for (int i = 0; queue.Count == 0 && i < w * ht; i++)
        {
            if (i % w == 0 || i % w == w - 1 || i / w == 0 || i / w == ht - 1)
            {
                Outlet(i, G(i), float.MinValue);
            }
        }
        while (queue.TryDequeue(out int i, out var at))
        {
            int c = i % w, r = i / w;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int nc = c + dc, nr = r + dr, n = (nr * w) + nc;
                    if (nc < 0 || nr < 0 || nc >= w || nr >= ht || down[n] != -2)
                    {
                        continue;
                    }
                    float level = MathF.Max(at.Level, G(n));
                    bool pools = level - G(n) > 3;
                    (down[n], end[n], run[n], clean[n]) = (i, end[i], pools ? 0 : run[i] + (FlowStep * MathF.Sqrt((dc * dc) + (dr * dr))), clean[i] && !pools);
                    queue.Enqueue(n, (level, G(n)));
                }
            }
        }
        return (down, end, run, clean, w);
    }

    /// <summary>A river's bed cut under its surface (deepest in the middle), its banks eased down to just above the water.</summary>
    private static void Carve(float[] h, int cols, int rows, List<Vector2> path, float[] surface, float half, float depth)
    {
        // each pixel near the river: its distance to the nearest stretch of it, and the surface there (only the pixels
        // round each stretch are visited)
        float reach = (half * 3) + 30;
        var near = new Dictionary<int, (float D, float S)>();
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            int c0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), c1 = Math.Min(cols - 1, (int)(MathF.Max(a.X, b.X) + reach));
            int r0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), r1 = Math.Min(rows - 1, (int)(MathF.Max(a.Y, b.Y) + reach));
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    float d = MapWater.SegmentDistance(new Vector2(c, r), a, b, out float t);
                    int p = (r * cols) + c;
                    if (d <= reach && (!near.TryGetValue(p, out var was) || d < was.D))
                    {
                        near[p] = (d, surface[i] + ((surface[i + 1] - surface[i]) * t));
                    }
                }
            }
        }
        foreach (var (p, (d, s)) in near)
        {
            if (d <= half * 1.15f)
            {
                float u = MathF.Min(1, d / (half * 1.15f));
                h[p] = MathF.Min(h[p], s - (depth * (1 - (u * u))) - 0.15f);
            }
            else
            {
                // the bank: just above the water at the edge, rising gently, then the ground as it was
                float bank = s + 0.35f + ((d - (half * 1.15f)) * 0.2f);
                if (h[p] > bank)
                {
                    h[p] = bank + ((h[p] - bank) * Smooth(half * 1.2f, reach, d));
                }
                else if (d < half * 1.6f)
                {
                    h[p] = bank;   // a low bank lifted just enough to hold the water
                }
            }
        }
    }

    /// <summary>Where the ground is under water (the sea), in a river's channel or past a dry edge: nothing grows or stands there.</summary>
    private static bool[] Wet(Spec spec, float[] h, int cols, int rows, float x0, float y0, List<River> rivers)
    {
        bool[] wet = new bool[cols * rows];
        if (!spec.CoastDry)
        {
            // under the sea (a coast's, a marsh's pools, or ground given that goes below it): nothing grows there
            for (int p = 0; p < wet.Length; p++)
            {
                wet[p] = h[p] < 0.3f;
            }
        }
        else
        {
            for (int p = 0; p < wet.Length; p++)
            {
                wet[p] = h[p] < -150;   // the cliff face and past it, at the world's edge: nothing stands there
            }
        }
        foreach (var river in rivers)
        {
            foreach (var (a, b) in river.Points.Zip(river.Points.Skip(1)))
            {
                float reach = a.HalfWidth + 2;
                for (float t = 0; t <= 1; t += 0.1f)
                {
                    var m = Vector2.Lerp(new(a.X - x0, a.Y - y0), new(b.X - x0, b.Y - y0), t);
                    for (int r = Math.Max(0, (int)(m.Y - reach)); r <= Math.Min(rows - 1, (int)(m.Y + reach)); r++)
                    {
                        for (int c = Math.Max(0, (int)(m.X - reach)); c <= Math.Min(cols - 1, (int)(m.X + reach)); c++)
                        {
                            if (Vector2.Distance(new(c, r), m) <= reach)
                            {
                                wet[(r * cols) + c] = true;
                            }
                        }
                    }
                }
            }
        }
        return wet;
    }

    /// <summary>
    /// Rocks in the air, 50 to 200 m over the ground: floating islands where the map has them (their tops planted with its
    /// trees, bushes and rocks), else the largest of the Rocks kinds, scaled up.
    /// </summary>
    private static void Float(Spec spec, float[] h, int cols, int rows, float x0, float y0, Planting kinds, Random rng, List<(string, Vector3, float, float)> objects)
    {
        if (kinds.Islands.Count > 0)
        {
            Islands(spec, h, cols, rows, x0, y0, kinds, rng, objects);
            return;
        }
        var big = kinds.Rocks.Where(k => k.Model.Contains("Big", StringComparison.OrdinalIgnoreCase) || k.Label.Contains("Big", StringComparison.OrdinalIgnoreCase)).ToList();
        var from = big.Count > 0 ? big : [.. kinds.Rocks];
        if (from.Count == 0)
        {
            return;
        }
        for (int i = 0; i < spec.Floaters; i++)
        {
            float c = 40 + (rng.NextSingle() * (cols - 80)), r = 40 + (rng.NextSingle() * (rows - 80));
            float up = 50 + (rng.NextSingle() * 150);
            objects.Add((from[rng.Next(from.Count)].Model, new Vector3(c + x0, At(h, cols, rows, c, r) + up, -(r + y0)), rng.NextSingle() * 360, 8 + (rng.NextSingle() * 22)));
        }
    }

    /// <summary>A point of a model turned by its yaw (degrees), as the editor turns it.</summary>
    internal static Vector2 Turn(float x, float z, float yaw)
    {
        float a = yaw * MathF.PI / 180, cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new((x * cos) + (z * sin), (z * cos) - (x * sin));
    }

    /// <summary>Floating islands, mostly small and a few large, apart from each other, their tops planted.</summary>
    private static void Islands(Spec spec, float[] h, int cols, int rows, float x0, float y0, Planting kinds, Random rng, List<(string, Vector3, float, float)> objects)
    {
        var placed = new List<(Vector2 At, float Reach)>();
        for (int i = 0; i < spec.Floaters; i++)
        {
            var kind = kinds.Islands[rng.Next(kinds.Islands.Count)];
            int variant = FloatingIsland.VariantOf(kind.Model);
            float scale = 0.5f + (2.2f * rng.NextSingle() * rng.NextSingle()), reach = FloatingIsland.Radius * scale * 1.3f;
            Vector2 at = default;
            bool free = false;
            for (int tries = 0; tries < 30 && !free; tries++)
            {
                at = new(reach + (rng.NextSingle() * (cols - (2 * reach))), reach + (rng.NextSingle() * (rows - (2 * reach))));
                free = placed.All(p => Vector2.Distance(p.At, at) > p.Reach + reach);
            }
            if (!free || variant < 0)
            {
                continue;
            }
            placed.Add((at, reach));
            float yaw = rng.NextSingle() * 360, y = At(h, cols, rows, at.X, at.Y) + 50 + (rng.NextSingle() * 150);
            objects.Add((kind.Model, new Vector3(at.X + x0, y, -(at.Y + y0)), yaw, scale));
            // its top: trees, bushes and a rock or two, as many as its size holds
            float area = MathF.PI * MathF.Pow(FloatingIsland.Radius * scale, 2);
            foreach (var (from, each) in new[] { (kinds.Trees, 350f), (kinds.Bushes, 120f), (kinds.Rocks.Where(k => !k.Model.Contains("Big", StringComparison.OrdinalIgnoreCase)).ToList(), 600f) })
            {
                for (int n = (int)MathF.Round(area / each * (0.6f + (0.8f * rng.NextSingle()))); n > 0 && from.Count > 0; n--)
                {
                    float ang = rng.NextSingle() * MathF.Tau, t = 0.75f * MathF.Sqrt(rng.NextSingle());
                    float lx = MathF.Cos(ang) * t * FloatingIsland.Edge(variant, ang), lz = MathF.Sin(ang) * t * FloatingIsland.Edge(variant, ang);
                    if (FloatingIsland.TopAt(variant, lx, lz) is not { } top)
                    {
                        continue;
                    }
                    var k = from[rng.Next(from.Count)];
                    var w = Turn(lx * scale, lz * scale, yaw);
                    objects.Add((k.Model, new Vector3(at.X + x0 + w.X, y + (top * scale) - 0.3f, -(at.Y + y0) + w.Y), rng.NextSingle() * 360, k.MinScale + (rng.NextSingle() * (k.MaxScale - k.MinScale))));
                }
            }
        }
    }
}
