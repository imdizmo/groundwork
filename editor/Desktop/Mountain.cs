namespace Groundwork.Desktop;

/// <summary>The Mountain tool's kinds of shape; each has a preset in <c>Mountain.Presets</c>.</summary>
public enum MountainKind
{
    /// <summary>One mountain rising to a peak.</summary>
    Peak,
    /// <summary>A long crest; Turn sets its direction.</summary>
    Ridge,
    /// <summary>Several peaks along a line; Turn sets its direction.</summary>
    Range,
    /// <summary>A flat top with steep sides.</summary>
    Mesa,
    /// <summary>A cone with a crater at the top.</summary>
    Volcano,
    /// <summary>A few gentle hills together.</summary>
    Hills,
}

/// <summary>One mountain: what <c>Mountain.Shape</c> builds its shape from.</summary>
/// <param name="Kind">Which shape.</param>
/// <param name="Height">About how many metres it adds at its highest (detail adds or takes a little).</param>
/// <param name="Radius">Metres from the middle to the foot, before the outline is warped.</param>
/// <param name="Rough">How much natural detail (swells, ridges, gullies), 0..1.</param>
/// <param name="Turn">Direction of a ridge or range, in degrees.</param>
/// <param name="Seed">Seeds the noise and the bumps, so the same spec makes the same mountain.</param>
public sealed record MountainSpec(MountainKind Kind, float Height, float Radius, float Rough, float Turn, int Seed)
{
    /// <summary>How far from the middle the mountain reaches (its outline is warped up to this).</summary>
    public float Reach => Radius * 1.15f;
}

/// <summary>
/// The Mountain tool's shapes (from the Valheim World Editor's, MIT): how many metres a mountain adds to the ground at
/// each point round its middle, made from a seed so the same numbers make the same mountain. Natural detail comes from
/// fractal noise: the outline is warped, the slopes get ridges and gullies.
/// </summary>
public static class Mountain
{
    /// <summary>A preset: a kind with the ranges a new seed picks its size from.</summary>
    public sealed record Preset(string Name, MountainKind Kind, (float Min, float Max) Height, (float Min, float Max) Radius, (float Min, float Max) Rough, string Help);

    /// <summary>The presets the Mountain tool offers, in its order.</summary>
    public static readonly Preset[] Presets =
    [
        new("Lone peak", MountainKind.Peak, (50, 110), (120, 240), (0.4f, 0.8f), "One mountain rising to a peak."),
        new("Ridge", MountainKind.Ridge, (35, 80), (140, 260), (0.4f, 0.8f), "A long crest, turned by Turn."),
        new("Mountain range", MountainKind.Range, (40, 90), (180, 280), (0.4f, 0.8f), "Several peaks along a line, turned by Turn."),
        new("Mesa", MountainKind.Mesa, (20, 50), (70, 150), (0.2f, 0.6f), "A flat top with steep sides."),
        new("Volcano", MountainKind.Volcano, (45, 100), (130, 240), (0.3f, 0.7f), "A cone with a crater at the top."),
        new("Rolling hills", MountainKind.Hills, (12, 30), (100, 220), (0.3f, 0.7f), "A few gentle hills together."),
    ];

    /// <summary>A new mountain of the preset: its size picked within the preset's ranges, with a random turn and seed.</summary>
    public static MountainSpec Roll(Preset p, Random r) => new(p.Kind, Pick(r, p.Height), Pick(r, p.Radius), MathF.Round(Pick(r, p.Rough) * 20) / 20, r.Next(0, 180), r.Next());

    private static float Pick(Random r, (float Min, float Max) range) => MathF.Round(range.Min + ((float)r.NextDouble() * (range.Max - range.Min)), 2);

    /// <summary>The shape's maker for a spec: metres to add at (x, z) metres east and north of the middle; 0 beyond its outline.</summary>
    public static Func<float, float, float> Shape(MountainSpec m)
    {
        var noise = new Noise(m.Seed);
        var rnd = new Random(m.Seed);
        float a = m.Turn * MathF.PI / 180, ca = MathF.Cos(a), sa = MathF.Sin(a), r = m.Radius, h = m.Height;
        // hills and ranges: bumps of their own (along the line for a range), picked once from the seed
        var bumps = new List<(float X, float Z, float R, float H)>();
        if (m.Kind == MountainKind.Hills)
        {
            int n = 3 + rnd.Next(4);
            for (int i = 0; i < n; i++)
            {
                float ang = (float)(rnd.NextDouble() * MathF.Tau), dist = (float)Math.Sqrt(rnd.NextDouble()) * 0.55f;
                bumps.Add((MathF.Cos(ang) * dist, MathF.Sin(ang) * dist, 0.4f + ((float)rnd.NextDouble() * 0.2f), 0.45f + ((float)rnd.NextDouble() * 0.55f)));
            }
        }
        else if (m.Kind == MountainKind.Range)
        {
            int n = 3 + rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                float t = -0.62f + (1.24f * i / (n - 1)) + (((float)rnd.NextDouble() - 0.5f) * 0.12f);
                bumps.Add((t, ((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.34f + ((float)rnd.NextDouble() * 0.12f), 0.55f + ((float)rnd.NextDouble() * 0.45f)));
            }
        }
        static float Smooth(float t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - (2 * t));
        // a natural profile: steeper up high, easing out at the foot (a gaussian, brought to 0 at the edge)
        static float Bell(float d) => d >= 1 ? 0 : (MathF.Exp(-3.2f * d * d) - 0.0408f) / 0.9592f;
        // hills and ranges: their bumps joined by a smooth maximum (where two peaks meet, a saddle instead of a crease)
        static float Bumps(List<(float X, float Z, float R, float H)> bumps, float u, float v)
        {
            float acc = 0f;
            const float Soft = 0.15f;
            foreach (var (bx, bz, br, bh) in bumps)
            {
                float bu = (u - bx) / br, bv = (v - bz) / br;
                float bump = bh * Bell(MathF.Sqrt((bu * bu) + (bv * bv))), t = Math.Clamp(0.5f + (0.5f * (bump - acc) / Soft), 0, 1);
                acc = bump <= 0 ? acc : acc <= 0 ? bump : acc + ((bump - acc) * t) + (Soft * t * (1 - t));
            }
            return acc;
        }
        return (x, z) =>
        {
            // the outline warped by noise, so no mountain is a perfect circle
            float wx = x + (r * 0.18f * noise.Fbm((x / (r * 0.6f)) + 3.1f, (z / (r * 0.6f)) - 7.7f));
            float wz = z + (r * 0.18f * noise.Fbm((x / (r * 0.6f)) - 5.3f, (z / (r * 0.6f)) + 1.9f));
            // turned, in radii: u along the Turn direction, v across
            float u = ((wx * ca) + (wz * sa)) / r, v = ((-wx * sa) + (wz * ca)) / r;
            float d = MathF.Sqrt((u * u) + (v * v));
            float body = m.Kind switch
            {
                MountainKind.Peak => Bell(d),
                MountainKind.Ridge => Bell(MathF.Sqrt((u * u) + (v / 0.5f * (v / 0.5f)))),
                MountainKind.Mesa => Smooth((1 - d) / 0.35f),
                MountainKind.Volcano => Bell(d) - (0.45f * Bell(d / 0.22f)),
                _ => Bumps(bumps, u, v),
            };
            if (body <= 0)
            {
                return 0;
            }
            // detail: broad swells, then ridges with gullies between them down the slopes, both in proportion to the
            // mountain, and a little fine detail capped at a few metres
            float swell = noise.Fbm((x / (r * 0.45f)) + 11.3f, (z / (r * 0.45f)) - 4.2f);
            float ridged = 1 - MathF.Abs(noise.Fbm((x / (r * 0.3f)) - 2.6f, (z / (r * 0.3f)) + 8.4f));
            float fine = noise.Fbm((x / 18f) + 5.5f, (z / 18f) - 3.3f) * MathF.Min(0.03f * h, 3f) / h;
            float detail = m.Rough * ((0.18f * swell) + (0.22f * (ridged - 0.55f) * MathF.Sqrt(body)) + (fine / MathF.Max(0.05f, body)));
            if (m.Kind == MountainKind.Mesa)
            {
                detail *= 1 - Smooth((body - 0.85f) / 0.15f);   // a mesa keeps its top flat: detail only on its sides
            }
            float edge = 1 - Smooth((d - 0.85f) / 0.3f);   // back to nothing at the edge
            return h * MathF.Max(0, body + (detail * body)) * edge;
        };
    }
}
