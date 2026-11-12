namespace Groundwork.Desktop;

/// <summary>The rock faces' grain: crags, and ribs and chutes running down them, on every face steeper than the rock.</summary>
public static partial class ZoneGenerator
{
    /// <summary>
    /// Crags: every face steeper than RockSlope (the ground blurred over 7 m: faces, not bumps on them) carved Crags metres
    /// deep with crags every way (a few metres, 18 m or so across), ribs and chutes running down the fall line (ridged
    /// noise stretched four times down it, which is across the noise's lines: of eight set ways, the two nearest blended),
    /// three times as deep, and gullies between buttresses (60 m or so across) four times as deep, the chutes and gullies
    /// cut into the face and never standing out of it. The lip at a face's top is kept (nothing cut within a few metres
    /// under it) and so is its foot (nothing cut under the ground 25 m down the face); nothing on land is cut under the
    /// sea's edge, and nothing under the sea raised out of it.
    /// </summary>
    private static void Cragged(Spec spec, float[] h, int cols, int rows, float x0, float y0)
    {
        if (spec.Crags <= 0)
        {
            return;
        }
        float[] soft = BoxBlur(h, cols, rows, 3), carved = new float[h.Length];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                float ground = h[p];
                int cl = Math.Max(0, c - 1), cr = Math.Min(cols - 1, c + 1), ru = Math.Max(0, r - 1), rd = Math.Min(rows - 1, r + 1);
                float gx = (soft[(r * cols) + cr] - soft[(r * cols) + cl]) / Math.Max(1, cr - cl), gy = (soft[(rd * cols) + c] - soft[(ru * cols) + c]) / Math.Max(1, rd - ru);
                float g = MathF.Sqrt((gx * gx) + (gy * gy));
                float face = Smooth(spec.RockSlope - 3, spec.RockSlope + 6, MathF.Atan(g) * 180 / MathF.PI);
                if (face <= 0)
                {
                    carved[p] = ground;
                    continue;
                }
                float x = c + x0, y = r + y0;
                // the lip: the ground 8 m up the face stands well above here, else this is the face's top
                float lip = Smooth(1, 6, At(soft, cols, rows, c + (8 * gx / g), r + (8 * gy / g)) - soft[p]);
                float theta = MathF.Atan2(gy, gx), k = ((theta * 8 / MathF.PI % 8) + 8) % 8;
                int k0 = (int)k;
                float Grooves(int o)
                {
                    float a = o % 8 * MathF.PI / 8, gw = 25, stretch = 4;
                    var (cs, sn) = (MathF.Cos(a), MathF.Sin(a));
                    return Ridged(((-x * sn) + (y * cs)) / gw, ((x * cs) + (y * sn)) / (gw * stretch), spec.Seed + 131 + (o % 8 * 7), 2);
                }
                float grooves = Grooves(k0) + ((Grooves(k0 + 1) - Grooves(k0)) * (k - k0));
                // gullies between buttresses (60 m or so across) four times as deep as the crags, cut in like the chutes
                float gullies = 4 * lip * MathF.Min(0, Fbm(x / 60, y / 60, spec.Seed + 135, 2) / 0.4f);
                float v = ground + (spec.Crags * face * ((Fbm(x / 18, y / 18, spec.Seed + 129, 3) / 0.4f) + (3 * lip * MathF.Min(0, grooves - 1)) + gullies));
                // never under the face's own foot (the ground 25 m down it): no trench dug under a low wall
                v = MathF.Max(v, MathF.Min(ground, At(soft, cols, rows, c - (25 * gx / g), r - (25 * gy / g)) + 0.5f));
                carved[p] = ground > 1 ? MathF.Max(v, MathF.Min(ground, 1)) : MathF.Min(v, ground);
            }
        });
        Array.Copy(carved, h, h.Length);
    }
}
