namespace Groundwork.Desktop;

/// <summary>Landforms worked into the ground: coasts cut back by the sea, sand dunes.</summary>
public static partial class ZoneGenerator
{
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
