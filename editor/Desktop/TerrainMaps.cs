namespace Groundwork.Desktop;

/// <summary>
/// Grey maps read off a map's ground, for painting and for materials elsewhere (an Unreal landscape's layers, a shader's
/// masks): where water runs and gathers, how steep the ground is, which way it faces, its ridges and hollows, and what the
/// generator's water and weather cut and laid down. Every value is 0..1.
/// </summary>
public static class TerrainMaps
{
    /// <summary>
    /// The maps of some ground, one at a time (each made only when it is asked for, so one is held at a time): flow,
    /// wetness, slope, convexity, facing north and east, snow, and wear and deposits when the generator's wear is given.
    /// </summary>
    /// <param name="h">Heights, rows of cols, a metre apart.</param>
    /// <param name="cols">Points a row.</param>
    /// <param name="rows">Rows.</param>
    /// <param name="sea">The sea level: water runs off into ground under it.</param>
    /// <param name="worn">What the generator's wear moved, metres a point (MapProject.ReadWorn); null: no wear maps.</param>
    /// <param name="snowLine">Where snow starts lying, metres; null: the land's highest quarter.</param>
    public static IEnumerable<(string Name, float[] Values)> Of(float[] h, int cols, int rows, float sea, float[]? worn, float? snowLine = null)
    {
        // ponytail: flow and wetness each route the water (twice the drainage, a few hundred ms on a big map) so that
        // One makes either alone for the Mask; share it if the export gets slow
        foreach (string name in (string[])["flow", "wetness"])
        {
            yield return (name, One(name, h, cols, rows, sea)!);
        }
        yield return ("slope", Each(cols, rows, p => ZoneGenerator.Slope(h, cols, rows, p % cols, p / cols) / 90));
        foreach (string name in (string[])["convexity", "facing_north", "facing_east", "snow"])
        {
            yield return (name, One(name, h, cols, rows, sea, snowLine)!);
        }
        if (worn != null)
        {
            // what the water and weather cut (valleys, gullies) and laid down (fans, valley floors, scree), 24 m and more
            // white: the generator's valleys move about that much
            yield return ("wear", Each(cols, rows, p => -worn[p] / 24));
            yield return ("deposits", Each(cols, rows, p => worn[p] / 24));
        }
    }

    /// <summary>The maps the Mask can limit by (slope it has already), in the Mask's order.</summary>
    public static readonly string[] MaskMaps = ["flow", "wetness", "convexity", "facing_north", "facing_east", "snow"];

    /// <summary>
    /// One map of some ground, made on its own (null: no such map): what Of makes under that name, without the others.
    /// </summary>
    public static float[]? One(string name, float[] h, int cols, int rows, float sea, float? snowLine = null)
    {
        switch (name)
        {
            case "flow" or "wetness":
                // flow: the ground a point drains, from half a hectare (0) to 0.2 km² (1), on a log scale; none in a
                // hollow, where the water stands rather than runs. Wetness: the topographic wetness index,
                // ln(area / tan slope), as log10 from 2 (a dry slope) to 7 (a wet floor); a hollow (a lake's bed) wet through
                var (logArea, pond) = LogArea(h, cols, rows, sea);
                return name == "flow"
                    ? Each(cols, rows, p => (1 - pond[p]) * (logArea[p] - 3.7f) / 1.6f)
                    : Each(cols, rows, p => MathF.Max(pond[p], (logArea[p] - MathF.Log10(MathF.Max(MathF.Tan(ZoneGenerator.Slope(h, cols, rows, p % cols, p / cols) * MathF.PI / 180), 0.01f)) - 2) / 5));
            case "convexity":
                // the ground over (ridges, white) or under (hollows, black) its average over 30 m, ±6 m; flat is grey
                float[] near = ZoneGenerator.BoxBlur(h, cols, rows, 2), far = ZoneGenerator.BoxBlur(h, cols, rows, 15);
                return Each(cols, rows, p => 0.5f + ((near[p] - far[p]) / 12));
            case "facing_north" or "facing_east":
                // which way the slope runs down: white facing the map's first row (north) or its last column (east)
                return Each(cols, rows, p => Facing(h, cols, rows, p, north: name == "facing_north"));
            case "snow":
                // snow: 3 m deep and more white
                float[] snow = Snow(h, cols, rows, snowLine ?? SnowLine(h, sea));
                return Each(cols, rows, p => snow[p] / 3);
            default:
                return null;
        }
    }

    /// <summary>The height of the land's highest quarter (sampled): where snow starts when no snow line is given.</summary>
    private static float SnowLine(float[] h, float sea)
    {
        float[] land = [.. h.Where((v, i) => i % 61 == 0 && v > sea).Order()];
        return land.Length == 0 ? sea + 100 : land[land.Length * 3 / 4];
    }

    /// <summary>
    /// Snow depth, metres: from nothing at the snow line to 2 m 60 m above it; more on ground facing away from the sun
    /// (which stands to the south, past the map's last row) and less facing it; blown off the ridges into the hollows;
    /// less and less sticking from 30° to none at 50°; then what lies slid down where snow and ground together are
    /// steeper than 38°, piling at the foot of the slope; smoothed over a few metres.
    /// </summary>
    internal static float[] Snow(float[] h, int cols, int rows, float line)
    {
        float[] near = ZoneGenerator.BoxBlur(h, cols, rows, 2), far = ZoneGenerator.BoxBlur(h, cols, rows, 15), snow = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float fall = 2 * Math.Clamp((h[p] - line) / 60, 0, 1);
                float shade = 0.6f + (0.8f * Facing(h, cols, rows, p, north: true));   // 0.6 facing the sun, 1.4 away
                float wind = Math.Clamp(1 - ((near[p] - far[p]) / 4), 0.2f, 1.8f);   // ridges bare, hollows filled
                float holds = 1 - Math.Clamp((ZoneGenerator.Slope(h, cols, rows, c, r) - 30) / 20, 0, 1);   // none sticks past 50°
                snow[p] = fall * shade * wind * holds;
            }
        });
        // ponytail: slides only spread about √passes points a pass run; enough for drifts at the foot of faces, not for
        // avalanches running a kilometre (add coarse-to-fine passes, as the generator's scree, if they are wanted)
        float talus = MathF.Tan(38 * MathF.PI / 180);
        float[] moved = new float[h.Length];
        for (int pass = 0; pass < 24; pass++)
        {
            Array.Clear(moved);
            Parallel.For(1, rows - 1, r =>
            {
                Span<float> excess = stackalloc float[9];
                for (int c = 1; c < cols - 1; c++)
                {
                    int i = (r * cols) + c;
                    if (snow[i] <= 0)
                    {
                        continue;
                    }
                    // the drops to the neighbours beyond the resting slope: half the largest (at most the snow there)
                    // slides, shared among them by their drops
                    float top = h[i] + snow[i], most = 0, sum = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        int j = i + (((k / 3) - 1) * cols) + (k % 3) - 1;
                        excess[k] = k == 4 ? 0 : MathF.Max(0, top - h[j] - snow[j] - (talus * (k is 0 or 2 or 6 or 8 ? 1.414f : 1)));
                        (most, sum) = (MathF.Max(most, excess[k]), sum + excess[k]);
                    }
                    if (sum > 0)
                    {
                        float m = MathF.Min(snow[i], most * 0.5f);
                        Interlocked(ref moved[i], -m);
                        for (int k = 0; k < 9; k++)
                        {
                            if (excess[k] > 0)
                            {
                                Interlocked(ref moved[i + (((k / 3) - 1) * cols) + (k % 3) - 1], m * excess[k] / sum);
                            }
                        }
                    }
                }
            });
            for (int i = 0; i < snow.Length; i++)
            {
                snow[i] += moved[i];
            }
        }
        return ZoneGenerator.BoxBlur(snow, cols, rows, 2);   // drifts are smooth: no snow a metre thick beside bare ground
    }

    /// <summary>Adds to a float shared between threads.</summary>
    private static void Interlocked(ref float at, float by)
    {
        float was, now;
        do
        {
            was = at;
            now = was + by;
        }
        while (System.Threading.Interlocked.CompareExchange(ref at, now, was) != was);
    }

    /// <summary>A map of f at every point, clamped to 0..1.</summary>
    private static float[] Each(int cols, int rows, Func<int, float> f)
    {
        float[] v = new float[cols * rows];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                v[p] = Math.Clamp(f(p), 0, 1);
            }
        });
        return v;
    }

    /// <summary>
    /// log10 of the ground (m²) each point drains: water routed down the ground sampled every 4 m (smoothed first, so it
    /// does not zigzag round every bump; hollows filled so it runs on through them), blended between the samples and
    /// softened a few metres; and how much each point lies in a hollow (half a metre and more under its lip: 1).
    /// </summary>
    private static (float[] LogArea, float[] Pond) LogArea(float[] h, int cols, int rows, float sea)
    {
        const int Step = 4;
        int w = ((cols - 2 + Step) / Step) + 1, d = ((rows - 2 + Step) / Step) + 1;
        float[] soft = ZoneGenerator.BoxBlur(h, cols, rows, 6), g = new float[w * d], wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = soft[(Math.Min(rows - 1, z * Step) * cols) + Math.Min(cols - 1, x * Step)];
                (g[(z * w) + x], wt[(z * w) + x]) = (v, v > sea ? 1 : 0);
            }
        }
        var (_, _, area, filled) = ZoneGenerator.Drainage(g, wt, w, d, Step);
        for (int i = 0; i < area.Length; i++)
        {
            area[i] = MathF.Log10(MathF.Max(area[i], 1));
            filled[i] = Math.Clamp((filled[i] - g[i]) * 2, 0, 1);
        }
        float[] log = new float[cols * rows], pond = new float[cols * rows];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                log[(r * cols) + c] = ZoneGenerator.At(area, w, d, c / (float)Step, r / (float)Step);
                pond[(r * cols) + c] = ZoneGenerator.At(filled, w, d, c / (float)Step, r / (float)Step);
            }
        });
        return (ZoneGenerator.BoxBlur(log, cols, rows, 2), pond);
    }

    /// <summary>How much the ground at p runs down towards the first row (north) or the last column (east): 0.5 ± 0.5.</summary>
    private static float Facing(float[] h, int cols, int rows, int p, bool north)
    {
        int c = p % cols, r = p / cols;
        int cl = Math.Max(0, c - 1), cr = Math.Min(cols - 1, c + 1), ru = Math.Max(0, r - 1), rd = Math.Min(rows - 1, r + 1);
        float dx = (h[(r * cols) + cr] - h[(r * cols) + cl]) / Math.Max(1, cr - cl), dy = (h[(rd * cols) + c] - h[(ru * cols) + c]) / Math.Max(1, rd - ru);
        float len = MathF.Sqrt((dx * dx) + (dy * dy));
        if (len < 1e-6f)
        {
            return 0.5f;
        }
        // downhill is -(dx, dy); towards the first row is -y, towards the last column +x
        float toward = north ? dy / len : -dx / len, steep = MathF.Min(1, len / MathF.Tan(10 * MathF.PI / 180));
        return 0.5f + (0.5f * toward * steep);
    }
}
