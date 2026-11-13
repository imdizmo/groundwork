namespace Groundwork.Desktop;

/// <summary>The ground worn before anything is laid on it: bands of rock in the faces, valleys, rain, scree, streams.</summary>
public static partial class ZoneGenerator
{
    /// <summary>
    /// Strata: the faces steeper than 12° under RockSlope cut back in bands of rock Strata metres thick, laid on a plane
    /// tilted a few degrees one way (by the seed) and bent by noise half a km across. Each band is hard (a ledge on top, a
    /// sheer riser) or soft (an even slope), by its own number. Bands are only cut into the face, never built out of it;
    /// flat ground stays as it was, and no land is cut under the sea's edge.
    /// </summary>
    private static void Stratified(Spec spec, float[] h, int cols, int rows, float x0, float y0)
    {
        if (spec.Strata <= 0)
        {
            return;
        }
        var rng = new Random(spec.Seed + 151);
        float heading = rng.NextSingle() * MathF.Tau, dip = MathF.Tan((2 + (8 * rng.NextSingle())) * MathF.PI / 180);
        float tx = MathF.Cos(heading) * dip, ty = MathF.Sin(heading) * dip;
        float[] soft = BoxBlur(h, cols, rows, 3);
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float ground = h[p], face = Smooth(spec.RockSlope - 12, spec.RockSlope, Slope(soft, cols, rows, c, r));
                if (face <= 0 || ground <= 1)
                {
                    continue;
                }
                float x = c + x0, y = r + y0;
                float t = (ground + (x * tx) + (y * ty) + (spec.Strata * 0.6f * Fbm(x / 500, y / 500, spec.Seed + 153, 2))) / spec.Strata;
                float f = t - MathF.Floor(t), k = Hash((int)MathF.Floor(t), 0, spec.Seed + 155) > 0 ? 6 : 2;
                float cut = (MathF.Pow(f, k) - f) * spec.Strata;   // ≤ 0: the band's ledge cut back to its riser
                h[p] = MathF.Max(ground + (face * cut), MathF.Min(ground, 1));
            }
        });
    }

    /// <summary>
    /// Erosion: rain over the whole map (Erosion.Hydraulic's drops, Erosion times as many as the brush's fill would let
    /// fall), worked on the ground sampled every 4 m so the water cuts valleys and gullies rather than pebbles, the sea
    /// left alone.
    /// </summary>
    private static void Eroded(Spec spec, float[] h, int cols, int rows)
    {
        if (spec.Erosion <= 0)
        {
            return;
        }
        Worn(h, cols, rows, 4, (g, wt, w, d) => Erosion.Hydraulic(g, wt, w, d, (int)(w * d * spec.Erosion * 0.4f), 1, new Random(spec.Seed + 157)));
    }

    /// <summary>
    /// Scree: ground steeper than Talus degrees slides down until it rests at that angle (Erosion.Thermal on the ground
    /// sampled every 16 m, then 8, 4 and 2), piling at the foot of every face; the sea left alone.
    /// </summary>
    private static void Weathered(Spec spec, float[] h, int cols, int rows)
    {
        if (spec.Talus <= 0)
        {
            return;
        }
        // ponytail: slides only spread from a slope's top and foot, about √passes cells, so whole slopes settle on coarse
        // samples first and finer ones after (coarse to fine); add a step if hills over ~1.5 km stay too steep
        float talus = Erosion.Talus(spec.Talus);
        foreach (var (step, passes) in new[] { (16, 100), (8, 50), (4, 30), (2, 12) })
        {
            Worn(h, cols, rows, step, (g, wt, w, d) => Erosion.Thermal(g, wt, w, d, talus * step, 1, passes));
        }
    }

    /// <summary>
    /// Valleys: the rain gathered into streams that cut the ground (stream power: a stream cuts as the root of the ground it
    /// drains, Braun and Willett's implicit solver) on the ground sampled every 16 m, in 30 passes of Valleys strength, so
    /// whole valleys open below the high ground, branching up into it; what they cut is carried down and laid where the
    /// fall eases (Davy and Lague's transport-limited streams): fans and flat valley floors; carried across hollows,
    /// whose beds stay for lakes.
    /// </summary>
    private static void Valleyed(Spec spec, float[] h, int cols, int rows)
    {
        if (spec.Valleys <= 0)
        {
            return;
        }
        Worn(h, cols, rows, 16, (g, wt, w, d) =>
        {
            float[] load = new float[w * d], laid = new float[w * d];
            for (int pass = 0; pass < 30; pass++)
            {
                var (order, to, area, filled) = Drainage(g, wt, w, d, 16);
                Array.Clear(load);
                Array.Clear(laid);
                foreach (int o in order)
                {
                    int t = to[o];
                    if (t != o && wt[o] > 0)
                    {
                        // ponytail: no cut on the bare slope above the first 4 samples' gathering (a stream starts there)
                        float f = spec.Valleys * 0.006f * MathF.Max(0, MathF.Sqrt(area[o]) - 32) / Run(o, t);
                        float cut = g[o] - MathF.Min(g[o], (g[o] + (f * g[t])) / (1 + f));   // the receiver is cut first: it lies earlier in the order
                        g[o] -= cut;
                        load[o] = cut;
                    }
                }
                // what was cut carried down (from the tops), each stream holding as much as its water and fall let it and
                // laying down the rest where the fall eases: fans at the feet of the slopes, flat floors in the valleys
                // and hollows; what reaches the sea or the edge is gone
                for (int i = order.Length - 1; i >= 0; i--)
                {
                    int o = order[i], t = to[o];
                    if (t == o || wt[o] <= 0 || g[o] < filled[o] - 0.01f)
                    {
                        load[t] += t == o ? 0 : load[o];   // carried across a hollow, not dumped in it: its bed stays for a lake
                        continue;
                    }
                    float fall = MathF.Max(0, (g[o] - g[t]) / Run(o, t)), holds = 1.5f * MathF.Sqrt(area[o]) * fall;
                    laid[o] = MathF.Min(MathF.Max(0, load[o] - holds), 2);   // ponytail: at most 2 m a pass on one sample, so no spikes
                    load[t] += load[o] - laid[o];
                }
                // spread across the floor (80 m), as a stream wanders over what it lays: laid on its own line alone, each
                // of the parallel lines water takes over gentle ground would build a ridge of its own
                laid = BoxBlur(laid, w, d, 2);
                // and the soil creeping (Culling's hillslope diffusion, a little each pass): the knife-cut channels
                // eased into valleys, and no sample left to stand over or sink under its neighbours pass after pass
                float[] was = (float[])g.Clone();
                Parallel.For(1, d - 1, z =>
                {
                    for (int x = 1; x < w - 1; x++)
                    {
                        int i = (z * w) + x;
                        if (wt[i] > 0)
                        {
                            g[i] += laid[i] + (0.08f * (was[i - 1] + was[i + 1] + was[i - w] + was[i + w] - (4 * was[i])));
                        }
                    }
                });
            }
            float Run(int o, int t) => Math.Abs(t - o) == 1 || Math.Abs(t - o) == w ? 16 : 22.6f;
        });
    }

    /// <summary>
    /// Where the water on a grid (w by d, step metres apart) runs: each point's receiver (itself at an outlet: the edge
    /// and the sea, weight 0), the points in order from the outlets up (every receiver before the points it drains), and
    /// the ground each drains (m²), and the ground with its hollows filled to their lips. Routed by priority flood
    /// (Barnes et al.): out of every hollow over its lowest lip.
    /// </summary>
    internal static (int[] Order, int[] To, float[] Area, float[] Filled) Drainage(float[] g, float[] wt, int w, int d, float step)
    {
        int n = w * d, k = 0;
        int[] order = new int[n], to = new int[n];
        bool[] seen = new bool[n];
        float[] filled = (float[])g.Clone();
        var queue = new PriorityQueue<int, float>();
        for (int i = 0; i < n; i++)
        {
            int x = i % w, z = i / w;
            if (wt[i] <= 0 || x == 0 || z == 0 || x == w - 1 || z == d - 1)
            {
                (to[i], seen[i]) = (i, true);
                queue.Enqueue(i, g[i]);
            }
        }
        while (queue.TryDequeue(out int i, out float level))
        {
            order[k++] = i;
            int x = i % w, z = i / w;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz, j = (nz * w) + nx;
                    if (nx >= 0 && nz >= 0 && nx < w && nz < d && !seen[j])
                    {
                        (filled[j], seen[j]) = (MathF.Max(g[j], level + 1e-3f), true);
                        queue.Enqueue(j, filled[j]);
                    }
                }
            }
        }
        // each to its steepest neighbour down the filled ground (the flood's own order would run water off at 45°);
        // that neighbour is lower, so it comes earlier in the order
        Parallel.For(0, n, i =>
        {
            int x = i % w, z = i / w;
            if (to[i] == i && (wt[i] <= 0 || x == 0 || z == 0 || x == w - 1 || z == d - 1))
            {
                return;
            }
            float best = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz, j = (nz * w) + nx;
                    if (nx >= 0 && nz >= 0 && nx < w && nz < d && j != i)
                    {
                        float drop = (filled[i] - filled[j]) / (dx != 0 && dz != 0 ? 1.414f : 1);
                        if (drop > best)
                        {
                            (best, to[i]) = (drop, j);
                        }
                    }
                }
            }
        });
        // the ground each drains: its water shared among every lower neighbour, the steeper the more (Holmgren's
        // multiple flow, power 4), so a smooth slope sheds it evenly and it gathers only where the ground does; one
        // receiver each, as the cut needs, would run it down parallel lines a sample apart and cut each its own channel
        float[] area = new float[n], share = new float[9];
        Array.Fill(area, step * step);
        for (int i = n - 1; i >= 0; i--)
        {
            int o = order[i], x = o % w, z = o / w;
            if (to[o] == o)
            {
                continue;
            }
            float sum = 0;
            for (int m = 0; m < 9; m++)
            {
                int nx = x + (m % 3) - 1, nz = z + (m / 3) - 1, j = (nz * w) + nx;
                float drop = nx >= 0 && nz >= 0 && nx < w && nz < d && j != o ? (filled[o] - filled[j]) / (m is 0 or 2 or 6 or 8 ? 1.414f : 1) : 0;
                share[m] = drop > 0 ? drop * drop * drop * drop : 0;
                sum += share[m];
            }
            for (int m = 0; m < 9; m++)
            {
                if (share[m] > 0)
                {
                    area[o + (((m / 3) - 1) * w) + (m % 3) - 1] += area[o] * share[m] / sum;
                }
            }
        }
        return (order, to, area, filled);
    }

    /// <summary>
    /// How much water runs through each metre, as 0..1: the ground it drains (on the ground sampled every 4 m) from half
    /// a hectare (0) to 0.2 km² (1), on a log scale, routed over the ground smoothed (no zigzags round every
    /// bump), blended between the samples and softened a few metres.
    /// </summary>
    private static float[] Streams(float[] h, int cols, int rows)
    {
        const int Step = 4;
        int w = ((cols - 2 + Step) / Step) + 1, d = ((rows - 2 + Step) / Step) + 1;
        float[] soft = BoxBlur(h, cols, rows, 6), g = new float[w * d], wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = soft[(Math.Min(rows - 1, z * Step) * cols) + Math.Min(cols - 1, x * Step)];
                (g[(z * w) + x], wt[(z * w) + x]) = (v, v > 1 ? 1 : 0);
            }
        }
        float[] area = Drainage(g, wt, w, d, Step).Area;
        for (int i = 0; i < area.Length; i++)
        {
            area[i] = Math.Clamp(MathF.Log10(area[i] / 5e3f) / 1.6f, 0, 1);
        }
        float[] flow = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                flow[(r * cols) + c] = At(area, w, d, c / (float)Step, r / (float)Step);
            }
        });
        flow = BoxBlur(flow, cols, rows, 4);   // a stream's line several metres wide, not a hair
        Parallel.For(0, flow.Length, p => flow[p] = MathF.Min(1, flow[p] * 2));
        return flow;
    }

    /// <summary>
    /// Expand: the high ground broadened by Expand metres, each point raised to the highest within that reach (peaks
    /// become plateaus, ridges tablelands), or with a negative Expand the low ground (valleys and basins widened to flat
    /// floors); softened over a fifth of the reach so the edges slope. The sea is left alone and no land goes under it.
    /// </summary>
    private static void Expanded(Spec spec, float[] h, int cols, int rows)
    {
        int reach = (int)MathF.Round(MathF.Abs(spec.Expand));
        if (reach < 1)
        {
            return;
        }
        float sign = spec.Expand > 0 ? 1 : -1;
        // ponytail: a square reach (rows, then columns), not a round one; a round one costs the reach times more
        float[] across = new float[h.Length], both = new float[h.Length];
        Parallel.For(0, rows, r => Extreme(h, across, r * cols, 1, cols, reach, sign));
        Parallel.For(0, cols, c => Extreme(across, both, c, cols, rows, reach, sign));
        both = BoxBlur(both, cols, rows, Math.Max(1, reach / 5));
        Parallel.For(0, h.Length, p =>
        {
            if (h[p] > 1)
            {
                h[p] = MathF.Max(both[p], 1);
            }
        });
    }

    /// <summary>
    /// The highest (sign 1) or lowest (sign -1) of n values from start, every stride apart, within reach of each: a running
    /// window that keeps its candidates falling, so each value enters and leaves it once.
    /// </summary>
    private static void Extreme(float[] from, float[] to, int start, int stride, int n, int reach, float sign)
    {
        int[] window = new int[n];
        int head = 0, tail = 0, next = 0;
        for (int i = 0; i < n; i++)
        {
            for (; next < n && next <= i + reach; next++)
            {
                float v = sign * from[start + (next * stride)];
                while (tail > head && sign * from[start + (window[tail - 1] * stride)] <= v)
                {
                    tail--;
                }
                window[tail++] = next;
            }
            while (window[head] < i - reach)
            {
                head++;
            }
            to[start + (i * stride)] = from[start + (window[head] * stride)];
        }
    }

    /// <summary>
    /// Wears the ground sampled every <paramref name="step"/> metres (each sample the ground averaged round it) and adds
    /// what the wear changed back onto every metre, blended between the samples: the wear works at the scale of valleys
    /// and in a step² share of the time. Land samples may change (weight 1), the sea's may not, and only land takes the
    /// change, never cut under the sea's edge.
    /// </summary>
    private static void Worn(float[] h, int cols, int rows, int step, Action<float[], float[], int, int> wear)
    {
        int w = ((cols - 2 + step) / step) + 1, d = ((rows - 2 + step) / step) + 1;
        float[] soft = BoxBlur(h, cols, rows, step / 2), g = new float[w * d], wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = soft[(Math.Min(rows - 1, z * step) * cols) + Math.Min(cols - 1, x * step)];
                (g[(z * w) + x], wt[(z * w) + x]) = (v, v > 1 ? 1 : 0);
            }
        }
        float[] before = (float[])g.Clone();
        wear(g, wt, w, d);
        for (int i = 0; i < g.Length; i++)
        {
            g[i] -= before[i];   // now what the wear changed
        }
        // blended between the samples, then softened over a sample's width: straight blends alone crease the ground at
        // every sample line, and light shows the creases as stripes
        float[] change = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                change[(r * cols) + c] = At(g, w, d, c / (float)step, r / (float)step);
            }
        });
        change = BoxBlur(change, cols, rows, step / 2);
        Parallel.For(0, h.Length, p =>
        {
            if (h[p] > 1)
            {
                h[p] = MathF.Max(h[p] + change[p], 1);   // land only, never cut under the sea's edge
            }
        });
    }
}
