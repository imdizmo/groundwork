namespace Groundwork.Desktop;

/// <summary>Landforms worked into the ground: glacial troughs, coasts cut back by the sea, sand dunes.</summary>
public static partial class ZoneGenerator
{
    /// <summary>
    /// Glaciers: valleys fed from the high ground (above the snow line, else the land's highest quarter) carved into
    /// troughs with flat floors and steep walls, a U where water cut a V. On the ground sampled every 16 m, each line of
    /// ice is traced down its valley (every valley line draining at least 5 hectares of ground that reaches above the
    /// line, down to where the ice melts 100 to 250 m under it; the longest first, a shorter one ending where it joins a
    /// longer); along each, the ground is cut down to a U about Glaciers metres across (wider the more ice comes down it),
    /// rising from the valley's floor: flat across its middle third, then up steeply. Nothing is raised.
    /// </summary>
    private static void Glaciated(Spec spec, float[] h, int cols, int rows)
    {
        if (spec.Glaciers < 32)
        {
            return;
        }
        const int Step = 16;
        int w = ((cols - 1) / Step) + 1, d = ((rows - 1) / Step) + 1;
        float[] soft = BoxBlur(h, cols, rows, Step / 2), g = new float[w * d], wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = soft[(Math.Min(rows - 1, z * Step) * cols) + Math.Min(cols - 1, x * Step)];
                (g[(z * w) + x], wt[(z * w) + x]) = (v, v > 1 ? 1 : 0);
            }
        }
        soft = [];
        float line = spec.SnowLine > 0 ? spec.SnowLine : Quantile([.. g.Where((v, i) => wt[i] > 0)], 0.75f);
        var (order, to, _, _) = Drainage(g, wt, w, d, Step);
        float[] area = OneWayArea(order, to, Step), fed = (float[])g.Clone();
        for (int k = order.Length - 1; k >= 0; k--)
        {
            fed[to[order[k]]] = MathF.Max(fed[to[order[k]]], fed[order[k]]);   // the highest ground each sample drains
        }
        bool[] ice = new bool[g.Length], below = new bool[g.Length];
        for (int i = 0; i < g.Length; i++)
        {
            ice[i] = wt[i] > 0 && area[i] >= 5e4f && fed[i] > line + 20 && g[i] > line - 250;
        }
        for (int i = 0; i < g.Length; i++)
        {
            if (ice[i] && to[i] != i)
            {
                below[to[i]] = true;   // fed by ice from above: not a head
            }
        }
        var lines = new List<List<int>>();
        for (int i = 0; i < g.Length; i++)
        {
            if (ice[i] && !below[i])
            {
                var path = new List<int> { i };
                for (int c = i; to[c] != c && ice[to[c]];)
                {
                    c = to[c];
                    path.Add(c);
                }
                lines.Add(path);
            }
        }
        bool[] taken = new bool[g.Length];
        // ponytail: one glacier's points held at a time (a few hundred thousand); where two troughs overlap, each cuts in turn
        var near = new Dictionary<int, (float D, float Floor, float Half, float Ice)>();
        float most = spec.Glaciers / 2;
        foreach (var path in lines.OrderByDescending(l => l.Count))
        {
            int end = path.FindIndex(c => taken[c]);
            var cells = end < 0 ? path : path[..(end + 1)];
            if (cells.Count < 6)
            {
                continue;
            }
            foreach (int c in cells)
            {
                taken[c] = true;
            }
            // the line eased round its corners; its floor never rising downhill; wider where more ice comes down
            near.Clear();
            var pts = cells.Select(c => new System.Numerics.Vector2(c % w * Step, c / w * Step)).ToList();
            for (int pass = 0; pass < 2; pass++)
            {
                pts = [pts[0], .. Enumerable.Range(1, pts.Count - 2).Select(i => (pts[i - 1] + (2 * pts[i]) + pts[i + 1]) / 4), pts[^1]];
            }
            float[] floor = new float[cells.Count], half = new float[cells.Count], strength = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                floor[i] = i == 0 ? g[cells[i]] : MathF.Min(floor[i - 1], g[cells[i]]);
                half[i] = most * Math.Clamp(0.5f + MathF.Sqrt(area[cells[i]] / 2e6f), 0.5f, 1);
                strength[i] = Smooth(line - 250, line - 100, floor[i]);
            }
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var (a, b) = (pts[i], pts[i + 1]);
                float reach = MathF.Max(half[i], half[i + 1]) * 1.6f;
                int c0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), c1 = Math.Min(cols - 1, (int)(MathF.Max(a.X, b.X) + reach));
                int r0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), r1 = Math.Min(rows - 1, (int)(MathF.Max(a.Y, b.Y) + reach));
                for (int r = r0; r <= r1; r++)
                {
                    for (int c = c0; c <= c1; c++)
                    {
                        float dist = MapWater.SegmentDistance(new System.Numerics.Vector2(c, r), a, b, out float t);
                        int p = (r * cols) + c;
                        if (dist <= reach && (!near.TryGetValue(p, out var was) || dist < was.D))
                        {
                            near[p] = (dist, floor[i] + ((floor[i + 1] - floor[i]) * t), half[i] + ((half[i + 1] - half[i]) * t), strength[i] + ((strength[i + 1] - strength[i]) * t));
                        }
                    }
                }
            }
            foreach (var (p, (dist, fl, hw, s)) in near)
            {
                float off = MathF.Max(0, dist - (hw / 3)) / (hw * 2 / 3), u = fl + (hw * 0.8f * off * off);
                if (u < h[p] && h[p] > 1)
                {
                    h[p] = MathF.Max(1, h[p] + ((u - h[p]) * s));
                }
            }
        }
        near.TrimExcess();
    }

    /// <summary>The value below which a share of some values lies.</summary>
    private static float Quantile(float[] v, float share)
    {
        if (v.Length == 0)
        {
            return 0;
        }
        Array.Sort(v);
        return v[Math.Clamp((int)(v.Length * share), 0, v.Length - 1)];
    }

    /// <summary>
    /// The sea's wear: land within SeaWear metres of the sea (more at the bays, less at the headlands, by noise) cut down
    /// to a bench rising gently from the water (2°, from 0.3 m), so low shores become beaches and high ground standing
    /// behind the bench is left as a sea cliff. Nothing is raised; a coast that drops away (CoastDry) has no sea to wear it.
    /// </summary>
    private static void SeaWorn(Spec spec, float[] h, int cols, int rows, float x0, float y0)
    {
        if (spec.SeaWear <= 0 || spec.CoastDry)
        {
            return;
        }
        float[] d = SeaDistance(h, cols, rows);
        float rise = MathF.Tan(2 * MathF.PI / 180);
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                if (h[p] <= 0 || d[p] > spec.SeaWear * 1.6f)
                {
                    continue;
                }
                // how far the sea cut in here: half to one and a half times SeaWear along the coast
                float width = spec.SeaWear * (1 + (0.5f * Fbm((c + x0) / (spec.SeaWear * 4), (r + y0) / (spec.SeaWear * 4), spec.Seed + 171, 3)));
                float bench = 0.3f + (d[p] * rise), cut = Smooth(width, width - 4, d[p]);   // the cliff's foot eased over 4 m
                if (h[p] > bench)
                {
                    h[p] += (bench - h[p]) * cut;
                }
            }
        });
    }

    /// <summary>Each point's distance to the sea (ground at or under its level), metres; two chamfer passes.</summary>
    private static float[] SeaDistance(float[] h, int cols, int rows)
    {
        const float Far = 1e9f, Diagonal = 1.41421f;
        float[] d = new float[h.Length];
        for (int p = 0; p < d.Length; p++)
        {
            d[p] = h[p] <= 0 ? 0 : Far;
        }
        void Relax(int p, int q, float by) => d[p] = MathF.Min(d[p], d[q] + by);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                if (c > 0)
                {
                    Relax(p, p - 1, 1);
                }
                if (r > 0)
                {
                    Relax(p, p - cols, 1);
                    if (c > 0)
                    {
                        Relax(p, p - cols - 1, Diagonal);
                    }
                    if (c < cols - 1)
                    {
                        Relax(p, p - cols + 1, Diagonal);
                    }
                }
            }
        }
        for (int r = rows - 1; r >= 0; r--)
        {
            for (int c = cols - 1; c >= 0; c--)
            {
                int p = (r * cols) + c;
                if (c < cols - 1)
                {
                    Relax(p, p + 1, 1);
                }
                if (r < rows - 1)
                {
                    Relax(p, p + cols, 1);
                    if (c < cols - 1)
                    {
                        Relax(p, p + cols + 1, Diagonal);
                    }
                    if (c > 0)
                    {
                        Relax(p, p + cols - 1, Diagonal);
                    }
                }
            }
        }
        return d;
    }

    /// <summary>
    /// Dunes: on flat, open land (under 6°, above the sea), in fields covering about half of it (by noise), ridges of
    /// sand Dunes metres high lying across the wind (blowing towards DuneWind degrees: 0 east, 90 the map's last row): a
    /// long gentle slope up on the windward side, a short steep one (near sand's 33° rest) down the lee, about fifteen
    /// times their height apart, their crests wandering and broken into separate dunes. Returns how much each point is
    /// dune (0..1), for the paint; null: no dunes.
    /// </summary>
    private static float[]? Duned(Spec spec, float[] h, int cols, int rows, float x0, float y0)
    {
        if (spec.Dunes <= 0)
        {
            return null;
        }
        float a = spec.DuneWind * MathF.PI / 180, wx = MathF.Cos(a), wy = MathF.Sin(a), apart = MathF.Max(20, spec.Dunes * 15);
        float[] soft = BoxBlur(h, cols, rows, 8), sand = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float x = c + x0, y = r + y0;
                float field = Smooth(-0.1f, 0.15f, Fbm(x / 700, y / 700, spec.Seed + 181, 3)) * Smooth(8, 4, Slope(soft, cols, rows, c, r)) * Smooth(1, 4, soft[p]);
                if (field <= 0)
                {
                    continue;
                }
                // along the wind, its lines wandering; a ridge each `apart` metres: up 90% of the way, down the last 10%
                float along = (x * wx) + (y * wy) + (apart * 0.6f * Fbm(x / (apart * 3), y / (apart * 3), spec.Seed + 183, 2));
                float t = (along / apart) - MathF.Floor(along / apart);
                float profile = t < 0.9f ? Smooth(0, 1, t / 0.9f) : Smooth(0, 1, (1 - t) / 0.1f);
                // crests rising and falling along their length: separate dunes rather than endless walls
                float across = (-x * wy) + (y * wx), crest = Smooth(-0.3f, 0.5f, Fbm(across / (apart * 2), along / (apart * 4), spec.Seed + 185, 2));
                float dune = field * crest;
                h[p] += spec.Dunes * profile * dune;
                sand[p] = dune;
            }
        });
        return sand;
    }

    /// <summary>
    /// The ground (m²) each sample drains with all its water passed to its one receiver (Drainage's own area spreads it
    /// over every lower neighbour, so a wide valley floor never gathers it into one line).
    /// </summary>
    internal static float[] OneWayArea(int[] order, int[] to, float step)
    {
        float[] area = new float[order.Length];
        Array.Fill(area, step * step);
        for (int k = order.Length - 1; k >= 0; k--)
        {
            int o = order[k];
            if (to[o] != o)
            {
                area[to[o]] += area[o];
            }
        }
        return area;
    }
}
