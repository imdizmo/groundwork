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
    /// wetness, slope, convexity, facing north and east, and wear and deposits when the generator's wear is given.
    /// </summary>
    /// <param name="h">Heights, rows of cols, a metre apart.</param>
    /// <param name="cols">Points a row.</param>
    /// <param name="rows">Rows.</param>
    /// <param name="sea">The sea level: water runs off into ground under it.</param>
    /// <param name="worn">What the generator's wear moved, metres a point (MapProject.ReadWorn); null: no wear maps.</param>
    public static IEnumerable<(string Name, float[] Values)> Of(float[] h, int cols, int rows, float sea, float[]? worn)
    {
        var (logArea, pond) = LogArea(h, cols, rows, sea);
        // flow: the ground a point drains, from half a hectare (0) to 0.2 km² (1), on a log scale; none in a hollow, where
        // the water stands rather than runs
        yield return ("flow", Each(cols, rows, p => (1 - pond[p]) * (logArea[p] - 3.7f) / 1.6f));
        // wetness: the topographic wetness index, ln(area / tan slope), as log10 from 2 (a dry slope) to 7 (a wet floor);
        // a hollow (a lake's bed) wet through
        yield return ("wetness", Each(cols, rows, p =>
        {
            float tan = MathF.Tan(ZoneGenerator.Slope(h, cols, rows, p % cols, p / cols) * MathF.PI / 180);
            return MathF.Max(pond[p], (logArea[p] - MathF.Log10(MathF.Max(tan, 0.01f)) - 2) / 5);
        }));
        (logArea, pond) = ([], []);
        yield return ("slope", Each(cols, rows, p => ZoneGenerator.Slope(h, cols, rows, p % cols, p / cols) / 90));
        // convexity: the ground over (ridges, white) or under (hollows, black) its average over 30 m, ±6 m; flat is grey
        float[] near = ZoneGenerator.BoxBlur(h, cols, rows, 2), far = ZoneGenerator.BoxBlur(h, cols, rows, 15);
        yield return ("convexity", Each(cols, rows, p => 0.5f + ((near[p] - far[p]) / 12)));
        (near, far) = ([], []);
        // facing: which way the slope runs down, white facing the map's first row (north) or its last column (east), black
        // the other way; flat ground grey, the full range from 10°
        yield return ("facing_north", Each(cols, rows, p => Facing(h, cols, rows, p, north: true)));
        yield return ("facing_east", Each(cols, rows, p => Facing(h, cols, rows, p, north: false)));
        if (worn != null)
        {
            // what the water and weather cut (valleys, gullies) and laid down (fans, valley floors, scree), 24 m and more
            // white: the generator's valleys move about that much
            yield return ("wear", Each(cols, rows, p => -worn[p] / 24));
            yield return ("deposits", Each(cols, rows, p => worn[p] / 24));
        }
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
