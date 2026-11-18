using System.Numerics;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The Measure tool, like the web editor's (editor/measure.js): a tape between two points of the ground. The first
/// click sets A, the line follows the mouse, the second click fixes B; a third click starts again. Points are world
/// positions (x east, height, z north).
/// </summary>
public sealed class MeasureTool
{
    /// <summary>
    /// The first point, or null before the first click.
    /// </summary>
    public Vector3? A { get; private set; }
    /// <summary>
    /// The second point: follows the mouse until fixed; null until the mouse moves after A.
    /// </summary>
    public Vector3? B { get; private set; }
    /// <summary>
    /// Whether B is fixed by the second click.
    /// </summary>
    public bool Fixed { get; private set; }
    /// <summary>
    /// Raised whenever A, B or Fixed changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// A click on the ground: sets A (starting again after a fixed tape) or fixes B.
    /// </summary>
    /// <param name="at">The clicked world position.</param>
    public void Down(Vector3 at)
    {
        if (A == null || Fixed)
        {
            A = at;
            B = null;
            Fixed = false;
        }
        else
        {
            B = at;
            Fixed = true;
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// The mouse moved: B follows it until fixed.
    /// </summary>
    public void Move(Vector3 at)
    {
        if (A != null && !Fixed)
        {
            B = at;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Empties the tape.
    /// </summary>
    public void Clear()
    {
        A = B = null;
        Fixed = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// What the tape measures, in metres unless said.
    /// </summary>
    /// <param name="Distance">Flat distance, ignoring height.</param>
    /// <param name="AlongSlope">Straight distance from A to B, height included.</param>
    /// <param name="HeightA">A's height.</param>
    /// <param name="HeightB">B's height.</param>
    /// <param name="Rise">B's height minus A's.</param>
    /// <param name="SlopePercent">Rise over flat distance, in percent; null when the points are (almost) one above the
    /// other.</param>
    /// <param name="SlopeDegrees">The slope's angle, 0..90°, whichever way it goes.</param>
    /// <param name="Lowest">Lowest ground along the line, A and B included.</param>
    /// <param name="Highest">Highest ground along the line, A and B included.</param>
    /// <param name="WaterDepth">How far the lower point is under the water; null when both are above it.</param>
    public sealed record Result(float Distance, float AlongSlope, float HeightA, float HeightB, float Rise, float? SlopePercent, float SlopeDegrees, float Lowest, float Highest, float? WaterDepth)
    {
        /// <summary>The time on foot from the first point to the second, seconds (Walk metres a second on the flat).</summary>
        public float WalkSeconds { get; init; }

        /// <summary>The time running, seconds (Run metres a second on the flat).</summary>
        public float RunSeconds { get; init; }

        /// <summary>The time riding, seconds (Ride metres a second on the flat; a horse swims as slowly as a walker).</summary>
        public float RideSeconds { get; init; }
    }

    /// <summary>A walker's, a game character's running and a horse's cantering speed on the flat, metres a second.</summary>
    public const float Walk = 1.4f, Run = 5f, Ride = 8f;

    /// <summary>Words in a layer's name and how fast its ground is to cross (1: as grass; the first that fits).</summary>
    private static readonly (string[] Words, float Pace)[] Grounds =
    [
        (["road", "path", "cobble", "paved", "pave", "street", "track"], 1.1f),
        (["marsh", "swamp", "bog", "mud", "mire"], 0.5f),
        (["snow", "ice"], 0.6f),
        (["sand", "dune", "beach"], 0.75f),
        (["scree", "rubble", "boulder", "rock", "stone", "cliff", "crag"], 0.8f),
        (["forest", "wood", "jungle", "thicket", "bush", "brush"], 0.85f),
    ];

    /// <summary>How fast a layer is to cross by its name (1 when no word fits: grass, dirt, a name of the user's own).</summary>
    public static float GroundPace(string layer)
    {
        string name = layer.ToLowerInvariant();
        foreach (var (words, pace) in Grounds)
        {
            if (words.Any(name.Contains))
            {
                return pace;
            }
        }
        return 1;
    }

    /// <summary>
    /// How much a slope (rise over run, uphill positive) slows walking, 1 on the flat: Tobler's hiking function, fastest
    /// a little downhill; never under a tenth.
    /// </summary>
    public static float Pace(float slope) => MathF.Max(0.1f, MathF.Exp(-3.5f * MathF.Abs(slope + 0.05f)) / MathF.Exp(-3.5f * 0.05f));

    /// <summary>
    /// What the tape measures, on the ground as it is now: heightAt gives the ground's height at a world point (both
    /// ends', which the ground under them may have changed since they were clicked, and the lowest and highest points
    /// along it, sampled about every metre).
    /// </summary>
    /// <param name="heightAt">Ground height at a world (x, z).</param>
    /// <param name="water">The water level, in metres.</param>
    /// <returns>The measurements, or null until both points are set.</returns>
    /// <param name="groundPace">How fast the ground at a world (x, z) is to cross (1 as grass); null: all as grass.</param>
    public Result? Measure(Func<float, float, float> heightAt, float water, Func<float, float, float>? groundPace = null)
    {
        if (A is not { } a || B is not { } b)
        {
            return null;
        }
        float ya = heightAt(a.X, a.Z), yb = heightAt(b.X, b.Z);
        float flat = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), rise = yb - ya;
        float lo = MathF.Min(ya, yb), hi = MathF.Max(ya, yb);
        int n = Math.Max(2, (int)MathF.Ceiling(flat));
        // the time along it, a metre or so at a time: slowed by each stretch's slope, swum under water (a metre a second)
        float walk = 0, run = 0, ride = 0, prev = ya, step = flat / n;
        for (int i = 0; i <= n; i++)
        {
            float h = heightAt(a.X + (b.X - a.X) * i / n, a.Z + (b.Z - a.Z) * i / n);
            lo = MathF.Min(lo, h);
            hi = MathF.Max(hi, h);
            if (i > 0 && step > 0)
            {
                float len = MathF.Sqrt((step * step) + ((h - prev) * (h - prev)));
                if (h < water)
                {
                    (walk, run, ride) = (walk + len, run + len, ride + len);
                }
                else
                {
                    float pace = Pace((h - prev) / step) * (groundPace?.Invoke(a.X + (b.X - a.X) * (i - 0.5f) / n, a.Z + (b.Z - a.Z) * (i - 0.5f) / n) ?? 1);
                    (walk, run, ride) = (walk + (len / (Walk * pace)), run + (len / (Run * pace)), ride + (len / (Ride * pace)));
                }
            }
            prev = h;
        }
        float low = MathF.Min(ya, yb);
        return new Result(flat, MathF.Sqrt(flat * flat + rise * rise), ya, yb, rise, flat > 0.01f ? rise / flat * 100 : null,
            MathF.Atan2(MathF.Abs(rise), flat) * 180 / MathF.PI, lo, hi, low < water ? water - low : null)
        { WalkSeconds = walk, RunSeconds = run, RideSeconds = ride };
    }

    /// <summary>
    /// What to show, in the language in use: the hint and the measured values (label, value).
    /// </summary>
    public (string Hint, List<(string, string)> Rows) Describe(Func<float, float, float> heightAt, float water, Func<float, float, float>? groundPace = null)
    {
        if (A == null)
        {
            return (Lang.T("measure.hint.first"), []);
        }
        if (Measure(heightAt, water, groundPace) is not { } r)
        {
            return (Lang.T("measure.hint.second"), []);
        }
        return (Fixed ? Lang.T("measure.hint.again") : Lang.T("measure.hint.fix"),
        [
            (Lang.T("measure.label.distance"), Lang.T("measure.value.metres", r.Distance)),
            (Lang.T("measure.label.alongSlope"), Lang.T("measure.value.metres", r.AlongSlope)),
            (Lang.T("measure.label.heights"), Lang.T("measure.value.heights", r.HeightA, r.HeightB, r.Rise >= 0 ? "+" : "", r.Rise)),
            (Lang.T("common.slope"), r.SlopePercent is float p
                ? Lang.T("measure.value.slope", p, r.SlopeDegrees)
                : Lang.T("measure.value.upright", r.SlopeDegrees)),
            (Lang.T("measure.label.lowHigh"), Lang.T("measure.value.lowHigh", r.Lowest, r.Highest)),
            (Lang.T("measure.label.water"), r.WaterDepth is float d ? Lang.T("measure.value.metres", d) : Lang.T("measure.value.aboveSea")),
            (Lang.T("measure.label.time"), Lang.T("measure.value.time", Clock(r.WalkSeconds), Clock(r.RunSeconds))),
            (Lang.T("measure.label.ride"), Clock(r.RideSeconds)),
        ]);
    }

    /// <summary>Seconds as m:ss (h:mm:ss from an hour).</summary>
    private static string Clock(float s) => TimeSpan.FromSeconds(Math.Round(Math.Max(0, s))).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss", System.Globalization.CultureInfo.InvariantCulture);
}
