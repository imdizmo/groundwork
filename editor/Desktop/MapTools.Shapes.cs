using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

// The Shape and Mountain tools (from the Valheim World Editor's, MIT): a whole shape put into the ground with one click.
public static partial class MapTools
{
    /// <summary>A see-through mesh over the ground: what <paramref name="add"/> would raise (or dig) round a point.</summary>
    private static Shape Surface(IAreaView area, Vector3 c, float reach, Func<float, float, float> add, Vector4 colour)
    {
        const int N = 48;
        var v = new Vector3[(N + 1) * (N + 1)];
        bool[] on = new bool[v.Length];
        for (int j = 0; j <= N; j++)
        {
            for (int i = 0; i <= N; i++)
            {
                float x = ((i / (float)N * 2) - 1) * reach, z = ((j / (float)N * 2) - 1) * reach, a = add(x, z);
                float gx = c.X + x, gz = c.Z + z;
                v[(j * (N + 1)) + i] = new Vector3(gx, area.GroundHeight(gx, gz) + a + 0.2f, gz);
                on[(j * (N + 1)) + i] = MathF.Abs(a) > 0.01f;
            }
        }
        var t = new List<int>();
        for (int j = 0; j < N; j++)
        {
            for (int i = 0; i < N; i++)
            {
                int k = (j * (N + 1)) + i;
                if (on[k] || on[k + 1] || on[k + N + 1] || on[k + N + 2])
                {
                    t.AddRange([k, k + 1, k + N + 1, k + 1, k + N + 2, k + N + 1]);
                }
            }
        }
        return new Shape(v, [.. t], colour) { BothSides = true, WorldSpace = true };
    }

    /// <summary>Shape: a mound, cone, mesa, crater, moat, bowl or ridged hill, or any formula, put in with one click.</summary>
    public sealed class ShapeTool(MainWindow w) : Base(w)
    {
        /// <summary>The presets, each a formula of x, z (metres east and north of the click), d (the distance), r and h.</summary>
        public static readonly (string Name, string Formula)[] Presets =
        [
            ("Mound", "h * smooth(1 - d / r)"),
            ("Cone", "h * max(0, 1 - d / r)"),
            ("Mesa", "h * smooth((r - d) / (0.35 * r))"),
            ("Crater", "d < r ? -h * max(0, 1 - (d / (0.62 * r)) ^ 2) + 0.35 * h * bell((d - 0.72 * r) / (0.16 * r)) * smooth((r - d) / (0.1 * r)) : 0"),
            ("Moat", "-h * bell((d - 0.75 * r) / (0.12 * r))"),
            ("Bowl", "-h * smooth(1 - (d / r) ^ 2)"),
            ("Ridged hill", "h * smooth(1 - d / r) * (0.75 + 0.25 * abs(sin(x / 3 + n(x / 9, z / 9) * 2)))"),
        ];

        private static readonly string[] Names = ["x", "z", "d", "r", "h"];
        private (string Src, Func<Formula.Env, double>? F, string? Error) _formula = ("", null, null);
        private bool _setting;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("shape", "Shape", "Pick a shape (or write a formula), set its radius and height, and click the ground where its middle goes. Each click is one step in History.") { Icon = "M3 19c3-8 6-11 9-11s6 3 9 11z" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ChoiceControl("preset", "Shape", [.. Presets.Select(p => p.Name)]),
            new SliderControl("radius", "Radius", 2, 300, 16) { Step = 1, Unit = "m" },
            new SliderControl("height", "Height", 0.5f, 150, 5) { Step = 0.5f, Unit = "m" },
            new TextBoxControl("formula", "Formula", "metres to add at x, z, d, r, h"),
            new TextControl("error", ""),
        ];

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => Fill(ctx);

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id == "preset")
            {
                Fill(ctx);
            }
            else if (id == "formula" && !_setting)
            {
                Compile(ctx, ctx.Typed("formula"));
            }
        }

        /// <summary>The formula box filled from the chosen preset.</summary>
        private void Fill(IToolContext ctx)
        {
            string src = Presets[Math.Clamp(ctx.Choice("preset"), 0, Presets.Length - 1)].Formula;
            _setting = true;
            ctx.SetText("formula", src);
            _setting = false;
            Compile(ctx, src);
        }

        private void Compile(IToolContext ctx, string src)
        {
            try
            {
                _formula = (src, Formula.Compile(src, Names), null);
            }
            catch (FormatException e)
            {
                _formula = (src, null, e.Message);
            }
            ctx.SetText("error", _formula.Error is { } why ? $"Formula: {why}" : "");
        }

        /// <summary>The formula's metres at (x, z) from the middle, for a radius and height (0 outside it or where it gives no number).</summary>
        private static Func<float, float, float> Of(Func<Formula.Env, double> f, float r, float h, Noise noise)
        {
            var env = new Formula.Env { Noise = (x, z) => noise.Fbm((float)x, (float)z) / 1.6 };
            return (x, z) =>
            {
                float d = MathF.Sqrt((x * x) + (z * z));
                if (d > r)
                {
                    return 0;
                }
                (env.Vars["x"], env.Vars["z"], env.Vars["d"], env.Vars["r"], env.Vars["h"]) = (x, z, d, r, h);
                double v;
                try
                {
                    v = f(env);
                }
                catch (ArithmeticException)
                {
                    return 0;
                }
                return double.IsFinite(v) ? (float)v : 0;
            };
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 at || Session is not { } s)
            {
                ctx.Preview([]);
                return;
            }
            float r = ctx.Number("radius"), h = ctx.Number("height");
            var colour = new Vector4(1, 0.8f, 0.3f, 0.9f);
            ctx.Preview(_formula.F is { } pf ? [Surface(ctx.Area, at, r, Of(pf, r, h, s.Brush.Noise), colour with { W = 0.35f }), Ring(ctx.Area, at, r, colour)] : [Ring(ctx.Area, at, r, new Vector4(1, 0.4f, 0.2f, 0.9f))]);
            if (input.Action != PointerAction.Press)
            {
                return;
            }
            if (_formula.F is not { } f)
            {
                ctx.Status($"The formula has a mistake: {_formula.Error}");
                return;
            }
            string name = Presets.FirstOrDefault(p => p.Formula == _formula.Src).Name ?? "formula";
            var g = s.Ground;
            var (touched, bad) = s.Shape(at.X - g.OriginX, at.Z - g.OriginZ, f, r, h, $"Shape: {name}");
            ctx.Status(touched == 0 ? "The shape changed nothing here (the formula gives 0, or the Mask leaves this ground out)."
                : $"Shape: {name}, {touched} points" + (bad > 0 ? $"; {bad} points where the formula gives no number were left as they were" : ""));
        }
    }

    /// <summary>Mountain: a peak, ridge, range, mesa, volcano or hills, made from a seed, put in with one click.</summary>
    public sealed class MountainTool(MainWindow w) : Base(w)
    {
        private readonly Random _random = new();
        private int _seed = Environment.TickCount;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("mountain", "Mountain", "Pick a kind, set its size, and click the ground where its middle goes; New mountain rolls another of the kind. Each click is one step in History, and the next one comes out different.") { Icon = "M2 20l7-12 4 6 3-4 6 10z" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ChoiceControl("preset", "Kind", [.. Mountain.Presets.Select(p => p.Name)]),
            new TextControl("help", ""),
            new SliderControl("height", "Height", 5, 600, 80) { Step = 1, Unit = "m" },
            new SliderControl("radius", "Radius", 30, 1500, 180) { Step = 5, Unit = "m" },
            new SliderControl("rough", "Rough", 0, 1, 0.6f) { Step = 0.05f },
            new SliderControl("turn", "Turn", 0, 179, 0) { Step = 1, Unit = "°" },
            new ButtonControl("roll", "New mountain"),
        ];

        private static Mountain.Preset Preset(IToolContext ctx) => Mountain.Presets[Math.Clamp(ctx.Choice("preset"), 0, Mountain.Presets.Length - 1)];

        private MountainSpec Spec(IToolContext ctx) =>
            new(Preset(ctx).Kind, ctx.Number("height"), ctx.Number("radius"), ctx.Number("rough"), ctx.Number("turn"), _seed);

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => ctx.SetText("help", Preset(ctx).Help);

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id is "preset" or "roll")
            {
                // a new one of the kind: its size picked within the kind's ranges
                var m = Mountain.Roll(Preset(ctx), _random);
                _seed = m.Seed;
                ctx.SetNumber("height", m.Height);
                ctx.SetNumber("radius", m.Radius);
                ctx.SetNumber("rough", m.Rough);
                ctx.SetNumber("turn", m.Turn);
                ctx.SetText("help", Preset(ctx).Help);
            }
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 at || Session is not { } s)
            {
                ctx.Preview([]);
                return;
            }
            var m = Spec(ctx);
            var shape = Mountain.Shape(m);
            var colour = new Vector4(1, 0.8f, 0.3f, 0.9f);
            ctx.Preview([Surface(ctx.Area, at, m.Reach, shape, colour with { W = 0.35f }), Ring(ctx.Area, at, m.Radius, colour)]);
            if (input.Action != PointerAction.Press)
            {
                return;
            }
            var g = s.Ground;
            float cx = at.X - g.OriginX, cz = at.Z - g.OriginZ;
            var mask = s.MaskNow();
            int touched = 0;
            _ = s.Commit($"Mountain: {Preset(ctx).Name}", gr =>
            {
                var raised = EditSession.RaiseGround(gr, cx, cz, m.Reach, shape, mask);
                touched = raised.Touched.Count;
                return raised;
            }, [], []);
            _seed = _random.Next();   // the next one different
            ctx.Status(touched == 0 ? "The mountain changed nothing here (the Mask leaves this ground out)." : $"Mountain: {Preset(ctx).Name}, {m.Height:0} m high, {m.Radius:0} m radius, {touched} points");
        }
    }
}
