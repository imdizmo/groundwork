namespace Groundwork.Desktop;

/// <summary>
/// The steps the ground is shaped in, after its hills, rim and coast are laid and before the lakes, rivers, paint and
/// planting: an ordered list, each step switched on or off and run at a share of its slider's strength, so the same
/// erosion can come before the strata or after, run twice, or not at all.
/// </summary>
public static partial class ZoneGenerator
{
    /// <summary>One shaping step: its kind (<see cref="StepKinds"/>), whether it runs, and how strongly (times its slider).</summary>
    /// <param name="Kind">The step's kind.</param>
    /// <param name="On">Whether it runs.</param>
    /// <param name="Times">Its slider's strength times this (1: as set; ignored by the steps with no strength).</param>
    public sealed record ShapeStep(string Kind, bool On = true, float Times = 1);

    /// <summary>The kinds of step, each with its name and the slider it follows, in the usual order.</summary>
    public static readonly (string Kind, string Name, string Slider)[] StepKinds =
    [
        ("expand", "Expand", "Expand"),
        ("valleys", "Valleys", "Valleys"),
        ("glaciers", "Glaciers", "Glaciers"),
        ("strata", "Strata", "Strata"),
        ("erosion", "Erosion", "Erosion"),
        ("scree", "Scree", "Scree angle"),
        ("sea", "Sea cut back", "Sea cut back"),
        ("dunes", "Dunes", "Dunes"),
        ("crags", "Crags", "Crags"),
    ];

    /// <summary>The usual order: every kind once, on, at its slider's strength.</summary>
    public static ShapeStep[] DefaultSteps => [.. StepKinds.Select(k => new ShapeStep(k.Kind))];

    /// <summary>The kinds whose change is the water's and weather's wear (kept for the paint and the wear maps).</summary>
    private static readonly HashSet<string> Wearing = ["valleys", "glaciers", "strata", "erosion", "scree", "sea"];

    /// <summary>
    /// Shapes the ground by the spec's steps (the usual order when it has none), from step <paramref name="from"/> on
    /// (the ground as the earlier ones left it, kept in the cache). Returns what the wearing steps moved (metres laid down,
    /// or cut when below 0; null when none ran) and how much each point is dune (null: no dunes).
    /// </summary>
    private static (float[]? Worn, float[]? Dunes) Shape(Spec spec, float[] h, int cols, int rows, float x0, float y0, int from = 0, ShapeCache? cache = null, float[]? given = null)
    {
        float[]? worn = null, dunes = null, before = null;
        var steps = spec.Steps ?? DefaultSteps;
        if (from > 0 && cache?.Get(ShapeCache.KeyOf(spec, steps, from, cols, rows, x0, y0, given)) is { } kept)
        {
            (worn, dunes) = ((float[]?)kept.Worn?.Clone(), (float[]?)kept.Dunes?.Clone());
        }
        for (int i = from; i < steps.Length; i++)
        {
            var step = steps[i];
            if (!step.On)
            {
                cache?.Put(ShapeCache.KeyOf(spec, steps, i + 1, cols, rows, x0, y0, given), h, worn, dunes);
                continue;
            }
            var s = Scaled(spec, step);
            bool wearing = Wearing.Contains(step.Kind);
            if (wearing)
            {
                before ??= new float[h.Length];
                Array.Copy(h, before, h.Length);
            }
            if (RunStep(s, step.Kind, h, cols, rows, x0, y0) is { } sand)
            {
                dunes ??= new float[h.Length];
                Parallel.For(0, h.Length, p => dunes[p] = MathF.Max(dunes[p], sand[p]));
            }
            if (wearing && Changed(before!, h))
            {
                worn ??= new float[h.Length];
                Parallel.For(0, h.Length, p => worn[p] += h[p] - before![p]);
            }
            cache?.Put(ShapeCache.KeyOf(spec, steps, i + 1, cols, rows, x0, y0, given), h, worn, dunes);
        }
        return (worn, dunes);
    }

    /// <summary>
    /// Runs one shaping step of a kind on some ground, at its spec's sliders (the Ground graph's nodes run them too).
    /// Returns how much each point is dune, for a dunes step that laid any; null otherwise.
    /// </summary>
    internal static float[]? RunStep(Spec s, string kind, float[] h, int cols, int rows, float x0, float y0)
    {
        switch (kind)
        {
            case "expand":
                Expanded(s, h, cols, rows);
                break;
            case "valleys":
                Valleyed(s, h, cols, rows);
                break;
            case "glaciers":
                Glaciated(s, h, cols, rows);
                break;
            case "strata":
                Stratified(s, h, cols, rows, x0, y0);
                break;
            case "erosion":
                Eroded(s, h, cols, rows);
                break;
            case "scree":
                Weathered(s, h, cols, rows);
                break;
            case "sea":
                SeaWorn(s, h, cols, rows, x0, y0);
                break;
            case "dunes":
                return Duned(s, h, cols, rows, x0, y0);
            case "crags":
                Cragged(s, h, cols, rows, x0, y0);
                break;
            default:
                break;   // a kind this version does not know (a newer map's): skipped
        }
        return null;
    }

    /// <summary>Whether any height differs.</summary>
    private static bool Changed(float[] a, float[] b) => !a.AsSpan().SequenceEqual(b);

    /// <summary>The spec a step runs with: its own slider times the step's Times (the steps with no strength as they are).</summary>
    private static Spec Scaled(Spec spec, ShapeStep step)
    {
        float t = MathF.Max(0, step.Times);
        if (MathF.Abs(t - 1) < 1e-6f)
        {
            return spec;
        }
        return step.Kind switch
        {
            "expand" => spec with { Expand = spec.Expand * t },
            "valleys" => spec with { Valleys = spec.Valleys * t },
            "glaciers" => spec with { Glaciers = spec.Glaciers * t },
            "strata" => spec with { Strata = spec.Strata * t },
            "erosion" => spec with { Erosion = spec.Erosion * t },
            "sea" => spec with { SeaWear = spec.SeaWear * t },
            "dunes" => spec with { Dunes = spec.Dunes * t },
            "crags" => spec with { Crags = spec.Crags * t },
            _ => spec,
        };
    }
}
