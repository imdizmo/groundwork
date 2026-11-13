using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// the map tools, on the rail while the map is open: Object (one object at a time from a
/// menu), Brush (scatters any kinds picked), River (rivers and lakes) and Grow (a bigger map). The kinds and
/// groups come from the model folder's catalog.json and its models; nothing here names one.
/// </summary>
public static partial class MapTools
{
    /// <summary>The add-on name the editor's own tools go under (their prefs are "mod.Groundwork/…").</summary>
    internal const string Own = "Groundwork";

    /// <summary>A kind of object: its model, name, group, size range and picture.</summary>
    public sealed record Kind(string Model, string Label, string Category, float MinScale, float MaxScale, string? Icon, bool Bed = false);

    /// <summary>A group of kinds (Trees, Plants...): the Brush's base density (per 100 m²) and spacing (m) for it.</summary>
    public sealed record Group(float Density, float Spacing);

    /// <summary>A biome: a ground colour (sand, forest, soft, grass) and kinds by weight, painted at a density.</summary>
    public sealed record Biome(string Name, string Paint, float Density, Dictionary<string, float> Kinds);

    /// <summary>
    /// The catalog's kinds, groups and biomes, then every other model of the folder as a kind of the group Other (a
    /// folder of glTF files needs no catalog).
    /// </summary>
    public static (List<Kind> Kinds, Dictionary<string, Group> Groups, List<Biome> Biomes) Catalog()
    {
        string file = CatalogFile;
        var (kinds, groups, biomes) = File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o ? Read(o) : ([], [], []);
        var listed = kinds.Select(k => k.Model).ToHashSet(StringComparer.Ordinal);
        kinds.AddRange(ModelStore.ModelKinds().Where(m => !listed.Contains(m)).Order(StringComparer.Ordinal).Select(m => new Kind(m, m, "Other", 1, 1, null)));
        return (kinds, groups, biomes);
    }

    /// <summary>A catalog's kinds, groups and biomes.</summary>
    private static (List<Kind> Kinds, Dictionary<string, Group> Groups, List<Biome> Biomes) Read(JsonObject o)
    {
        static float F(JsonNode? n, float d) => n?.GetValue<float>() ?? d;
        var kinds = (o["kinds"]?.AsArray() ?? []).Select(k => new Kind(k!["model"]!.GetValue<string>(), k["label"]?.GetValue<string>() ?? "", k["category"]?.GetValue<string>() ?? "Other",
            F(k["scale"]?[0], 1), F(k["scale"]?[1], 1), k["icon"] is { } i ? Path.Combine(ModelStore.MapModels, i.GetValue<string>()) : null, k["bed"]?.GetValue<bool>() ?? false)).ToList();
        var groups = (o["categories"]?.AsObject() ?? []).ToDictionary(kv => kv.Key, kv => new Group(F(kv.Value?["density"], 2), F(kv.Value?["spacing"], 2)));
        var biomes = (o["biomes"]?.AsObject() ?? []).Select(kv => new Biome(kv.Key, kv.Value?["paint"]?.GetValue<string>() ?? "grass", F(kv.Value?["density"], 1),
            (kv.Value?["kinds"]?.AsObject() ?? []).ToDictionary(k => k.Key, k => F(k.Value, 1)))).ToList();
        return (kinds, groups, biomes);
    }

    /// <summary>The catalog: map.json's catalog, else catalog.json in the model folder.</summary>
    internal static string CatalogFile => MapProject.Current?.Catalog ?? Path.Combine(ModelStore.MapModels, "catalog.json");

    /// <summary>
    /// A biome added to the map's catalog (so the Biome tool has it at once): the kinds at weight 1, density 1. A biome
    /// of that name is replaced. Only a catalog map.json names (in the map's folder) is written: why not, or null.
    /// </summary>
    public static string? SaveBiome(string name, string paint, IReadOnlyList<string> models)
    {
        if (MapProject.Current?.Catalog is not { } file || !File.Exists(file) || JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o)
        {
            return "This map has no catalog of its own to keep the biome in (map.json's catalog).";
        }
        var kinds = new JsonObject();
        foreach (string m in models.Distinct())
        {
            kinds[m] = 1.0;
        }
        var biomes = o["biomes"] as JsonObject ?? [];
        o["biomes"] = biomes;
        biomes[name] = new JsonObject { ["paint"] = paint, ["density"] = 1.0, ["kinds"] = kinds };
        File.WriteAllText(file, o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, IndentSize = 1 }));
        return null;
    }

    /// <summary>The tunnels drawn in the 3D view: registered once.</summary>
    private static readonly Lazy<bool> SceneRegistered = new(() =>
    {
        ModExtensions.AddBuiltInScene(new MapCaves.Scene());
        return true;
    });

    /// <summary>The tools for the map open in a window.</summary>
    public static List<ModExtensions.Tool> For(MainWindow w)
    {
        _ = SceneRegistered.Value;
        var (kinds, groups, biomes) = Catalog();
        var tools = new List<ModExtensions.Tool>
        {
            ModExtensions.BuiltIn(Own, new ObjectTool(w, kinds)),
            ModExtensions.BuiltIn(Own, new StampTool(w, Stamps())),
            ModExtensions.BuiltIn(Own, new RiverTool(w)),
            ModExtensions.BuiltIn(Own, new TunnelTool(w)),
            ModExtensions.BuiltIn(Own, new PathTool(w, [.. kinds.Where(k => k.Category != "Markers")])),
            ModExtensions.BuiltIn(Own, new AreasTool(w, AreaKinds())),
            ModExtensions.BuiltIn(Own, new GrowTool(w)),
            ModExtensions.BuiltIn(Own, new LayersTool(w, GroupNames(kinds))),
            ModExtensions.BuiltIn(Own, new ChecksTool(w)),
            ModExtensions.BuiltIn(Own, new ObjectsTool(w)),
            ModExtensions.BuiltIn(Own, new NotesTool(w)),
        };
        tools.Insert(1, ModExtensions.BuiltIn(Own, new ScatterTool(w, kinds, groups, null)));
        if (biomes.Count > 0)
        {
            tools.Insert(2, ModExtensions.BuiltIn(Own, new ScatterTool(w, kinds, groups, biomes)));
        }
        return tools;
    }

    /// <summary>What every map tool shares: the open scene, the ground under the pointer, rings and dots to show.</summary>
    public abstract class Base(MainWindow w) : IEditorTool
    {
        /// <summary>The window.</summary>
        protected MainWindow W { get; } = w;

        /// <summary>The open map's sea level.</summary>
        protected float Sea => W.Session?.Scene.Water ?? 0;

        /// <inheritdoc/>
        public abstract ToolInfo Info { get; }

        /// <inheritdoc/>
        public abstract IReadOnlyList<ToolControl> Controls { get; }

        /// <summary>The open map's session.</summary>
        protected EditSession? Session => W.Session is { Scene.IsMap: true } s ? s : null;

        /// <inheritdoc/>
        public virtual void OnChosen(IToolContext ctx)
        {
        }

        /// <inheritdoc/>
        public virtual void OnLeft(IToolContext ctx)
        {
        }

        /// <inheritdoc/>
        public abstract void OnPointer(IToolContext ctx, PointerInput input);

        /// <inheritdoc/>
        public virtual bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

        /// <inheritdoc/>
        public virtual void OnControl(IToolContext ctx, string id)
        {
        }

        /// <summary>The ground under the pointer (over an object too: the ray is followed down to the ground).</summary>
        protected static Vector3? Ground(IToolContext ctx, PointerInput input)
        {
            if (input.Target == PointerTarget.Ground)
            {
                return input.Point;
            }
            if (input.Direction == Vector3.Zero)
            {
                return null;
            }
            var area = ctx.Area;
            var d = Vector3.Normalize(input.Direction);
            for (float t = 0; t < 3000; t += 0.5f)
            {
                var p = input.Origin + (d * t);
                if (p.Y <= area.GroundHeight(p.X, p.Z))
                {
                    return p with { Y = area.GroundHeight(p.X, p.Z) };
                }
            }
            return null;
        }

        /// <summary>The ground's up direction at a point.</summary>
        protected static Vector3 Normal(IAreaView area, float x, float z) => Vector3.Normalize(new Vector3(
            area.GroundHeight(x - 0.5f, z) - area.GroundHeight(x + 0.5f, z), 1f, area.GroundHeight(x, z - 0.5f) - area.GroundHeight(x, z + 0.5f)));

        /// <summary>A ring on the ground (wider for a bigger ring, so it reads from further away).</summary>
        protected static Shape Ring(IAreaView area, Vector3 c, float r, Vector4 colour)
        {
            int n = Math.Clamp((int)(r * 4), 24, 96);
            float half = MathF.Max(0.15f, r * 0.02f);
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = MathF.Tau * i / n, x = c.X + (r * MathF.Cos(a)), z = c.Z + (r * MathF.Sin(a));
                float y = area.GroundHeight(x, z) + 0.15f;
                v.Add(new Vector3(x - (MathF.Cos(a) * half), y, z - (MathF.Sin(a) * half)));
                v.Add(new Vector3(x + (MathF.Cos(a) * half), y, z + (MathF.Sin(a) * half)));
                if (i > 0)
                {
                    int k = v.Count - 4;
                    t.AddRange([k, k + 1, k + 2, k + 1, k + 3, k + 2]);
                }
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>A see-through disc lying on the ground (following it), as a brush's area.</summary>
        protected static Shape Patch(IAreaView area, Vector3 c, float r, Vector4 colour)
        {
            const int Rings = 6, Spokes = 32;
            var v = new List<Vector3> { c with { Y = area.GroundHeight(c.X, c.Z) + 0.1f } };
            var t = new List<int>();
            for (int k = 1; k <= Rings; k++)
            {
                for (int i = 0; i < Spokes; i++)
                {
                    float a = MathF.Tau * i / Spokes, d = r * k / Rings, x = c.X + (d * MathF.Cos(a)), z = c.Z + (d * MathF.Sin(a));
                    v.Add(new Vector3(x, area.GroundHeight(x, z) + 0.1f, z));
                    int me = 1 + ((k - 1) * Spokes) + i, next = 1 + ((k - 1) * Spokes) + ((i + 1) % Spokes);
                    if (k == 1)
                    {
                        t.AddRange([0, me, next]);
                    }
                    else
                    {
                        int inMe = me - Spokes, inNext = next - Spokes;
                        t.AddRange([inMe, me, next, inMe, next, inNext]);
                    }
                }
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>Little upright markers at points.</summary>
        protected static Shape Dots(IEnumerable<Vector3> points, float size, Vector4 colour)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var p in points)
            {
                int k = v.Count;
                v.AddRange([p + new Vector3(-size, 0, 0), p + new Vector3(size, 0, 0), p + new Vector3(0, size * 3, 0), p + new Vector3(0, 0, -size), p + new Vector3(0, 0, size)]);
                t.AddRange([k, k + 1, k + 2, k + 3, k + 4, k + 2]);
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>Flat round markers at points, seen well from above.</summary>
        protected static Shape Discs(IEnumerable<Vector3> points, float r, Vector4 colour)
        {
            const int N = 16;
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var p in points)
            {
                int k = v.Count;
                v.Add(p);
                for (int i = 0; i < N; i++)
                {
                    v.Add(p + new Vector3(r * MathF.Cos(MathF.Tau * i / N), 0, r * MathF.Sin(MathF.Tau * i / N)));
                    t.AddRange([k, k + 1 + i, k + 1 + ((i + 1) % N)]);
                }
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>A flat band along a line of points (closed: back to the first), so many metres wide.</summary>
        protected static Shape Band(IReadOnlyList<Vector3> points, float width, Vector4 colour, bool closed = false)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = points.Count + (closed && points.Count > 2 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var p = points[i % points.Count];
                var a = points[Math.Max(0, i - 1) % points.Count];
                var b = points[Math.Min(n - 1, i + 1) % points.Count];
                var d = new Vector2(b.X - a.X, b.Z - a.Z);
                d = d.LengthSquared() < 1e-6f ? Vector2.UnitX : Vector2.Normalize(d);
                var side = new Vector3(-d.Y, 0, d.X) * (width / 2);
                v.Add(p - side);
                v.Add(p + side);
                if (i > 0)
                {
                    int k = v.Count - 4;
                    t.AddRange([k, k + 1, k + 2, k + 1, k + 3, k + 2]);
                }
            }
            return new Shape([.. v], [.. t], colour) { BothSides = true, WorldSpace = true };
        }

        /// <summary>The Euler angles (Unity's: y, then x, then z) that stand an object up along a direction, turned by yaw.</summary>
        protected static Vector3 Upright(Vector3 up, float yawDegrees)
        {
            float y = yawDegrees * MathF.PI / 180f, c = MathF.Cos(y), s = MathF.Sin(y);
            var n = new Vector3((up.X * c) - (up.Z * s), up.Y, (up.X * s) + (up.Z * c));   // up turned back by the yaw
            float b = MathF.Asin(Math.Clamp(-n.X, -1, 1)), a = MathF.Atan2(n.Z, n.Y);
            return new Vector3(a * 180f / MathF.PI, yawDegrees, b * 180f / MathF.PI);
        }

        /// <summary>A number 0..1 fixed for a whole-metre spot and a seed.</summary>
        internal static float Hash01(int x, int z, int seed)
        {
            uint h = (uint)((x * 374761393) + (z * 668265263) + (seed * 1442695041));
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        /// <summary>Smooth noise 0..1 with features about a unit across.</summary>
        internal static float Noise01(float x, float z, int seed)
        {
            int xi = (int)MathF.Floor(x), zi = (int)MathF.Floor(z);
            float fx = x - xi, fz = z - zi, ux = fx * fx * (3 - (2 * fx)), uz = fz * fz * (3 - (2 * fz));
            float a = Hash01(xi, zi, seed), b = Hash01(xi + 1, zi, seed), c = Hash01(xi, zi + 1, seed), d = Hash01(xi + 1, zi + 1, seed);
            return a + ((b - a) * ux) + ((c - a) * uz) + ((a - b - c + d) * ux * uz);
        }

        /// <summary>A smooth step from 0 at e0 to 1 at e1.</summary>
        internal static float Step(float e0, float e1, float v)
        {
            float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
            return t * t * (3 - (2 * t));
        }

        /// <summary>A new object of a kind.</summary>
        protected static (NewObject Object, bool Piece) New(string model, Vector3 at, Vector3 euler, float scale) =>
            (new NewObject(0, AssetIds.Of(model), at, euler, MathF.Abs(scale - 1) < 1e-3f ? 0 : scale), false);
    }

    /// <summary>Object: one object at a time, picked from a menu of the catalog's kinds.</summary>
    public sealed class ObjectTool(MainWindow w, List<Kind> kinds) : Base(w)
    {
        private float _turn;
        private readonly Random _random = new();
        private float _nextYaw = -1, _nextSize;

        /// <summary>The next one's turn and size (random ones drawn once, so the ghost shows what a click puts down).</summary>
        private (float Yaw, float Size) Next(IToolContext ctx, Kind kind)
        {
            if (_nextYaw < 0)
            {
                _nextYaw = _random.NextSingle() * 360;
                _nextSize = kind.MinScale + (_random.NextSingle() * (kind.MaxScale - kind.MinScale));
            }
            return (ctx.IsOn("randomTurn") ? _nextYaw : _turn, ctx.IsOn("randomSize") ? _nextSize : ctx.Number("size"));
        }

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("object", "Object", "Pick a kind, click the ground to put one there. R turns it 15°, Shift+R back. Select (E) moves or deletes it.") { Icon = "M7 9a5 5 0 1 0 10 0a5 5 0 1 0-10 0M12 14v7" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new PaletteControl("kind", "Kinds", [.. kinds.Select(k => (k.Category, k.Label, k.Icon))]),
            new SliderControl("size", "Size", 0.1f, 5f, 1f) { Step = 0.05f },
            new ToggleControl("randomSize", "Random size (the kind's own range)", false),
            new SliderControl("turn", "Turn", 0, 360, 0) { Step = 1, Unit = "°" },
            new ToggleControl("randomTurn", "Random turn", true),
            new ToggleControl("align", "Lean with the ground", false),
            new SliderControl("sink", "Sink into the ground", 0, 2, 0) { Step = 0.05f, Unit = "m" },
            new TextControl("status", ""),
        ];

        private Kind? Picked(IToolContext ctx) => ctx.Choice("kind") is int i && i >= 0 && i < kinds.Count ? kinds[i] : null;

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => _turn = ctx.Number("turn");

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id == "turn")
            {
                _turn = ctx.Number("turn");
            }
            _nextYaw = id == "kind" ? -1 : _nextYaw;
            ctx.SetText("status", Picked(ctx) is { } k ? $"{k.Label} ({k.Category})" : "Pick a kind.");
        }

        /// <inheritdoc/>
        public override bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
        {
            if (key is not ("R" or "OemComma" or "OemPeriod"))
            {
                return false;
            }
            bool back = key == "OemComma" || (key == "R" && mods.HasFlag(KeyModifiers.Shift));
            _turn = ((ctx.IsOn("randomTurn") && _nextYaw >= 0 ? _nextYaw : _turn) + (back ? 345 : 15)) % 360;
            _nextYaw = _turn;
            ctx.Status($"Turn {_turn:0}°");
            return true;
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 at || Picked(ctx) is not { } kind || Session is not { } s)
            {
                ctx.Preview([]);
                W.View.SetModGhosts([]);
                return;
            }
            var (yaw, size) = Next(ctx, kind);
            var up = ctx.IsOn("align") ? Normal(ctx.Area, at.X, at.Z) : Vector3.UnitY;
            var (where, turned) = (at - new Vector3(0, ctx.Number("sink"), 0), Upright(up, yaw));
            // The kind's own model, see-through, where a click puts it (a ring as well while its model loads).
            W.View.SetModGhosts([(AssetIds.Of(kind.Model), where, turned, size)]);
            ctx.Preview([Ring(ctx.Area, at, 0.6f, new Vector4(1, 0.85f, 0.3f, 0.9f))]);
            if (input.Action != PointerAction.Press)
            {
                return;
            }
            _ = s.Commit("Place " + kind.Label, null, [], [New(kind.Model, where, turned, size)]);
            _nextYaw = -1;
            ctx.Status($"Placed {kind.Label}");
        }
    }

    /// <summary>A brush: scatters its kinds round the pointer up to its density (Shift: takes them away).</summary>
    public sealed class ScatterTool(MainWindow w, List<Kind> kinds, Dictionary<string, Group> groups, List<Biome>? biomes) : Base(w)
    {
        private readonly List<(Vector3 At, float Radius)> _dabs = [];
        private Func<int, float>? _mask;
        private readonly List<Vector3> _pending = [];
        private readonly List<(string Model, Vector3 At, Vector3 Euler, float Scale)> _adds = [];
        private readonly HashSet<int> _removes = [];
        private Dictionary<(int, int), List<(Vector3 At, string Group)>> _near = [];
        private HashSet<int> _hashes = [];
        private Dictionary<int, string> _groupOf = [];
        private Vector3? _last;
        private int _stroke, _dab;
        private bool _erasing, _whole;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = biomes == null
            ? new("brush", "Brush", "Pick any kinds, then hold and drag. Each group (Trees, Bushes, Plants, Rocks...) fills to its own density and spacing (the catalog's categories) times the sliders, each kind at its own size times Size; it fills that far and no further. Patchy leaves clearings, Soft edge thins the rim; the Mask panel limits where. Shift takes the picked kinds away. Fill the whole map does it everywhere at once.") { Icon = "M5 8a2 2 0 1 0 4 0a2 2 0 1 0-4 0M14 6a2 2 0 1 0 4 0a2 2 0 1 0-4 0M10 14a2 2 0 1 0 4 0a2 2 0 1 0-4 0M4 18a2 2 0 1 0 4 0a2 2 0 1 0-4 0M16 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0" }
            : new("biome", "Biome", "Paint a biome (the catalog's biomes): its ground colour and its kinds, in one stroke. Same density, spacing, patches and mask as the Brush. Shift takes the biome's kinds away.") { Icon = "M3 20h18M6 20l4-9 3 5 2-3 3 7M15 6a2 2 0 1 0 4 0a2 2 0 1 0-4 0" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            biomes == null
                ? new PaletteControl("kinds", "Kinds", [.. kinds.Select(k => (k.Category, k.Label, k.Icon))]) { Multi = true }
                : new ChoiceControl("biome", "Biome", [.. biomes.Select(b => b.Name)]),
            .. (biomes == null ? new ToolControl[] { new ButtonControl("saveBiome", "Save the picked kinds as a biome") } : []),
            new SliderControl("radius", "Radius", 1, 250, biomes == null ? 8 : 16) { Step = 0.5f, Unit = "m" },
            new SliderControl("density", "Density ×", 0.05f, 5, 1) { Step = 0.05f },
            new SliderControl("spacing", "Spacing ×", 0, 3, 1) { Step = 0.05f },
            new ToggleControl("erase", "Take away (as Shift)", false),
            new ButtonControl("fill", "Fill the whole map"),
            new TextControl("status", ""),
            new SectionControl("secSize", "Size") { Folded = true },
            new SliderControl("minSize", "Size × from", 0.2f, 3, 0.9f) { Step = 0.05f },
            new SliderControl("maxSize", "Size × to", 0.2f, 3, 1.1f) { Step = 0.05f },
            new SectionControl("secPatches", "Patches") { Folded = biomes == null },
            new SliderControl("patchy", "Patchy", 0, 1, biomes == null ? 0 : 0.3f) { Step = 0.05f, Help = "0: even; more: clumps and clearings (fixed in place, so painting again does not fill them)." },
            new SliderControl("patchSize", "Patch size", 4, 80, 20) { Step = 1, Unit = "m" },
            new SliderControl("soft", "Soft edge", 0, 1, 0.4f) { Step = 0.05f, Help = "Thins out towards the brush's rim." },
            new SectionControl("secWhere", "Where") { Folded = true },
            new SliderControl("awayFromWater", "Keep from water", 0, 30, 0) { Step = 0.5f, Unit = "m" },
            new ToggleControl("useMask", "Follow \"Only where…\" below (height, slope, ground colour)", true),
            new SliderControl("maxSlope", "Steepest ground", 0, 90, 35) { Step = 1, Unit = "°" },
            new ToggleControl("water", "Also under water", false),
            new SectionControl("secEach", "Each object") { Folded = true },
            new SliderControl("tilt", "Random lean", 0, 30, 0) { Step = 1, Unit = "°" },
            new ToggleControl("align", "Lean with the ground", false),
            new SliderControl("seed", "Seed", 0, 999, 1) { Step = 1 },
        ];

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id == "saveBiome")
            {
                _ = W.SaveBiomeAsked([.. PickedKinds(ctx).Select(k => k.Model)]);
            }
            else if (id == "fill")
            {
                FillWhole(ctx);
            }
        }

        private Biome? Chosen(IToolContext ctx) => biomes is { Count: > 0 } ? biomes[Math.Clamp(ctx.Choice("biome"), 0, biomes.Count - 1)] : null;

        private List<Kind> PickedKinds(IToolContext ctx) => Chosen(ctx) is { } b
            ? [.. kinds.Where(k => b.Kinds.ContainsKey(k.Model))]
            : [.. ctx.Picked("kinds").Where(i => i >= 0 && i < kinds.Count).Select(i => kinds[i])];

        private float Weight(IToolContext ctx, Kind k) => Chosen(ctx)?.Kinds.GetValueOrDefault(k.Model, 1f) ?? 1f;

        /// <summary>Whether a spot passes the patches, the soft rim, the mask and the distance from water.</summary>
        private bool Allowed(IToolContext ctx, EditSession s, float x, float z, float fromMiddle, float r)
        {
            int seed = (int)ctx.Number("seed"), xi = (int)MathF.Floor(x * 2), zi = (int)MathF.Floor(z * 2);
            float soft = ctx.Number("soft");
            if (soft > 0 && !_whole && Hash01(xi, zi, seed + 11) > 1 - Step(1 - soft, 1, fromMiddle / r))
            {
                return false;
            }
            float patchy = ctx.Number("patchy");
            if (patchy > 0)
            {
                float size = ctx.Number("patchSize");
                float n = (Noise01(x / size, z / size, seed + 23) * 0.7f) + (Noise01(x / size * 3, z / size * 3, seed + 29) * 0.3f);
                if (n < patchy * 0.75f + ((Hash01(xi, zi, seed + 31) - 0.5f) * 0.1f))
                {
                    return false;
                }
            }
            var sc = s.Scene;
            int gx = Math.Clamp((int)MathF.Round(x - sc.OriginX), 0, sc.W - 1), gz = Math.Clamp((int)MathF.Round(z - sc.OriginZ), 0, sc.H - 1);
            if (_mask != null && ctx.IsOn("useMask") && _mask((gz * sc.W) + gx) < 0.5f)
            {
                return false;
            }
            float away = ctx.Number("awayFromWater");
            if (away > 0)
            {
                var area = ctx.Area;
                for (int k = 0; k < 12; k++)
                {
                    float a = MathF.Tau * k / 12, dd = away * (k % 2 == 0 ? 1 : 0.5f);
                    if (area.GroundHeight(x + (dd * MathF.Cos(a)), z + (dd * MathF.Sin(a))) < Sea + 0.05f)
                    {
                        return false;
                    }
                }
                var u = new Vector2(x, -z);
                foreach (var river in MapWater.Rivers)
                {
                    for (int i = 0; i + 1 < river.Points.Count; i++)
                    {
                        var (pa, pb) = (river.Points[i], river.Points[i + 1]);
                        if (MapWater.SegmentDistance(u, new(pa.X, pa.Y), new(pb.X, pb.Y), out _) < MathF.Max(pa.HalfWidth, pb.HalfWidth) + away)
                        {
                            return false;
                        }
                    }
                }
                if (MapWater.Lakes.Any(l => l.Outline.Count > 2 && MapWater.InsideBy(l.Outline, u) > -away))
                {
                    return false;
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            var area = ctx.Area;
            var at = Ground(ctx, input);
            float r = ctx.Number("radius");
            bool erase = ctx.IsOn("erase") || input.Modifiers.HasFlag(KeyModifiers.Shift);
            var ring = new Vector4(erase ? 1 : 0.4f, erase ? 0.35f : 0.9f, 0.4f, 0.9f);
            if (Session is not { } s)
            {
                return;
            }
            switch (input.Action)
            {
                case PointerAction.Press when at is Vector3 p:
                    Begin(s, ctx, erase);
                    Dab(s, ctx, p);
                    break;
                case PointerAction.Drag when at is Vector3 p && _last is Vector3 last:
                    if (Vector2.Distance(new(p.X, p.Z), new(last.X, last.Z)) >= Math.Max(0.5f, r * 0.4f))
                    {
                        Dab(s, ctx, p);
                    }
                    break;
                case PointerAction.Release:
                    Finish(s, ctx);
                    break;
                default:
                    break;
            }
            var shapes = new List<Shape>();
            if (at is Vector3 c)
            {
                shapes.Add(Patch(area, c, r, ring with { W = 0.2f }));
                shapes.Add(Ring(area, c, r, ring));
                if (input.Action == PointerAction.Move)
                {
                    ctx.SetText("status", Expected(ctx, r, erase));
                }
            }
            if (_pending.Count > 0)
            {
                shapes.Add(Dots(_pending, 0.2f, new Vector4(0.5f, 1f, 0.5f, 0.9f)));
            }
            ctx.Preview(shapes);
        }

        /// <summary>What one dab places at most on empty ground: so many of each group, from the kinds picked.</summary>
        private string Expected(IToolContext ctx, float r, bool erase)
        {
            var picked = PickedKinds(ctx);
            if (picked.Count == 0)
            {
                return "Pick kinds in the objects drawer (Ctrl+Space) to brush with.";
            }
            string names = string.Join(", ", picked.Take(3).Select(k => k.Label.Length > 0 ? k.Label : k.Model)) + (picked.Count > 3 ? $" +{picked.Count - 3}" : "");
            if (erase)
            {
                return $"Takes away {names} under the brush.";
            }
            float times = ctx.Number("density") * (Chosen(ctx)?.Density ?? 1f), area = MathF.PI * r * r / 100f;
            var each = picked.GroupBy(k => k.Category).Select(g => $"{MathF.Round(groups.GetValueOrDefault(g.Key, new Group(2, 2)).Density * times * area):0} {g.Key}");
            return $"{names}: up to {string.Join(", ", each)} a dab, fewer where some stand.";
        }

        private void Begin(EditSession s, IToolContext ctx, bool erase)
        {
            _erasing = erase;
            _stroke++;
            _dab = 0;
            _pending.Clear();
            _adds.Clear();
            _removes.Clear();
            _dabs.Clear();
            _mask = s.MaskNow();
            _groupOf = PickedKinds(ctx).ToDictionary(k => AssetIds.Of(k.Model), k => k.Category);
            _hashes = [.. _groupOf.Keys];
            // The brush's kinds already standing, by 4 m cell and group (Trees, Plants...): spacing and density count
            // within a group, so the dense grass of a mixed brush does not leave its trees no room.
            _near = [];
            lock (s.Scene.Things)
            {
                foreach (var t in s.Scene.Things.Where(t => !t.Gone && _hashes.Contains(t.Prefab)))
                {
                    Cell(t.Position).Add((t.Position, _groupOf[t.Prefab]));
                }
            }
        }

        private List<(Vector3 At, string Group)> Cell(Vector3 p)
        {
            var key = ((int)MathF.Floor(p.X / 4), (int)MathF.Floor(p.Z / 4));
            if (!_near.TryGetValue(key, out var list))
            {
                _near[key] = list = [];
            }
            return list;
        }

        private IEnumerable<Vector3> Around(Vector3 p, float r, string group)
        {
            int c = (int)MathF.Ceiling(r / 4);
            int cx = (int)MathF.Floor(p.X / 4), cz = (int)MathF.Floor(p.Z / 4);
            for (int x = cx - c; x <= cx + c; x++)
            {
                for (int z = cz - c; z <= cz + c; z++)
                {
                    if (_near.TryGetValue((x, z), out var list))
                    {
                        foreach (var (q, g) in list)
                        {
                            if (g == group && Vector2.Distance(new(q.X, q.Z), new(p.X, p.Z)) <= r)
                            {
                                yield return q;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The whole map filled as one stroke of dabs 16 m round on a 22 m grid (overlapping: no gaps), with the
        /// stroke's density, spacing, patches, slope, water and mask but no soft rims; one History step.
        /// </summary>
        private void FillWhole(IToolContext ctx)
        {
            if (Session is not { } s)
            {
                return;
            }
            const float R = 16, Grid = 22;
            Begin(s, ctx, false);
            _whole = true;
            var a = ctx.Area;
            for (float z = a.MinZ + (Grid / 2); z < a.MaxZ; z += Grid)
            {
                for (float x = a.MinX + (Grid / 2); x < a.MaxX; x += Grid)
                {
                    Dab(s, ctx, new Vector3(x, a.GroundHeight(x, z), z), R);
                }
            }
            _whole = false;
            Finish(s, ctx);
        }

        private void Dab(EditSession s, IToolContext ctx, Vector3 c, float? radius = null)
        {
            _last = c;
            _dab++;
            float r = radius ?? ctx.Number("radius");
            _dabs.Add((c, r));
            if (_erasing)
            {
                lock (s.Scene.Things)
                {
                    for (int i = 0; i < s.Scene.Things.Count; i++)
                    {
                        var t = s.Scene.Things[i];
                        if (!t.Gone && _hashes.Contains(t.Prefab) && Vector2.Distance(new(t.Position.X, t.Position.Z), new(c.X, c.Z)) <= r && _removes.Add(i))
                        {
                            _pending.Add(t.Position);
                        }
                    }
                }
                ctx.SetText("status", $"Taking away {_removes.Count}");
                return;
            }
            var picked = PickedKinds(ctx);
            if (picked.Count == 0)
            {
                ctx.SetText("status", "Pick at least one kind.");
                return;
            }
            var area = ctx.Area;
            var rng = new Random(HashCode.Combine((int)ctx.Number("seed"), _stroke, _dab));
            float spacingTimes = ctx.Number("spacing"), densityTimes = ctx.Number("density") * (Chosen(ctx)?.Density ?? 1f), slope = ctx.Number("maxSlope"), tilt = ctx.Number("tilt");
            float lo = MathF.Min(ctx.Number("minSize"), ctx.Number("maxSize")), hi = MathF.Max(ctx.Number("minSize"), ctx.Number("maxSize"));
            // Each group fills its own density, counting and spacing only its own kinds (the dense grass of a mixed
            // stroke leaves its trees room); its picked kinds share it evenly.
            foreach (var group in picked.GroupBy(k => k.Category))
            {
                var g = groups.GetValueOrDefault(group.Key, new Group(2, 2));
                float spacing = g.Spacing * spacingTimes, target = g.Density * densityTimes * MathF.PI * r * r / 100f;
                int want = (int)MathF.Floor(target) + (rng.NextSingle() < target - MathF.Floor(target) ? 1 : 0) - Around(c, r, group.Key).Count();
                var members = group.ToList();
                for (int tries = 0; want > 0 && tries < want * 12; tries++)
                {
                    float a = rng.NextSingle() * MathF.Tau, d = r * MathF.Sqrt(rng.NextSingle());
                    float x = c.X + (d * MathF.Cos(a)), z = c.Z + (d * MathF.Sin(a)), y = area.GroundHeight(x, z);
                    var p = new Vector3(x, y, z);
                    var n = Normal(area, x, z);
                    if ((!ctx.IsOn("water") && y < Sea + 0.05f) || MathF.Acos(Math.Clamp(n.Y, -1, 1)) * 180 / MathF.PI > slope || (spacing > 0 && Around(p, spacing, group.Key).Any())
                        || !Allowed(ctx, s, x, z, d, r))
                    {
                        continue;
                    }
                    float pick = rng.NextSingle() * members.Sum(k => Weight(ctx, k));
                    var kind = members[^1];
                    foreach (var k in members)
                    {
                        if ((pick -= Weight(ctx, k)) <= 0)
                        {
                            kind = k;
                            break;
                        }
                    }
                    var up = ctx.IsOn("align") ? n : Vector3.UnitY;
                    if (tilt > 0)
                    {
                        up = Vector3.Normalize(up + (new Vector3((rng.NextSingle() - 0.5f) * 2, 0, (rng.NextSingle() - 0.5f) * 2) * MathF.Tan(tilt * MathF.PI / 180)));
                    }
                    float own = kind.MinScale + (rng.NextSingle() * (kind.MaxScale - kind.MinScale));
                    _adds.Add((kind.Model, p, Upright(up, rng.NextSingle() * 360), own * (lo + (rng.NextSingle() * (hi - lo)))));
                    Cell(p).Add((p, group.Key));
                    _pending.Add(p);
                    want--;
                }
            }
            ctx.SetText("status", $"{_adds.Count} to place");
        }

        /// <summary>The biome's ground colour under the stroke (soft towards its rim), in the stroke's own step.</summary>
        private (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect) PaintBiome(IToolContext ctx, Ground g, Biome b)
        {
            float[] colour = Brush.PaintOf(Brush.PaintFor(MapProject.Current?.LayerNamed(b.Paint) ?? 0))!;
            var touched = new HashSet<int>();
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = 0, z1 = 0;
            foreach (var (c, r) in _dabs)
            {
                int cx = (int)MathF.Round(c.X - g.OriginX), cz = (int)MathF.Round(c.Z - g.OriginZ), rr = (int)MathF.Ceiling(r);
                for (int gz = Math.Max(1, cz - rr); gz <= Math.Min(g.H - 2, cz + rr); gz++)
                {
                    for (int gx = Math.Max(1, cx - rr); gx <= Math.Min(g.W - 2, cx + rr); gx++)
                    {
                        float d = MathF.Sqrt(((gx - cx) * (gx - cx)) + ((gz - cz) * (gz - cz)));
                        int p = (gz * g.W) + gx;
                        if (d > r || (_mask != null && ctx.IsOn("useMask") && _mask(p) < 0.5f))
                        {
                            continue;
                        }
                        Sculpt.PaintPoint(g, p, colour, 1 - Step(0.6f, 1f, d / r));
                        _ = touched.Add(p);
                        (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
                    }
                }
            }
            return ([.. touched], touched.Count > 0 ? (x0, z0, x1, z1) : (0, 0, 0, 0));
        }

        private void Finish(EditSession s, IToolContext ctx)
        {
            if (_adds.Count > 0 || _removes.Count > 0 || (!_erasing && Chosen(ctx) != null && _dabs.Count > 0))
            {
                _ = s.Commit((Chosen(ctx) is { } bm ? bm.Name : "Brush") + (_erasing ? $": took away {_removes.Count}" : $": placed {_adds.Count}"),
                    _erasing || Chosen(ctx) is not { } b ? null : g => PaintBiome(ctx, g, b), [.. _removes], [.. _adds.Select(a => New(a.Model, a.At, a.Euler, a.Scale))]);
                ctx.Status(_erasing ? $"Took away {_removes.Count}" : $"Placed {_adds.Count}");
            }
            _pending.Clear();
            _adds.Clear();
            _removes.Clear();
            _last = null;
        }
    }

    /// <summary>A stamp's object: model, offset from the stamp's middle (x, z), height above its own ground, Euler, scale.</summary>
    public sealed record StampPart(string Model, float Dx, float Dz, float Up, Vector3 Euler, float Scale);

    /// <summary>The open map's stamps folder (map.json's stamps; none: stamps/ in its folder).</summary>
    public static string StampFolder => MapProject.Current is { } m ? m.Stamps ?? Path.Combine(m.Folder, "stamps") : "";

    /// <summary>The stamps saved, by name.</summary>
    public static List<(string Name, List<StampPart> Parts)> Stamps()
    {
        var stamps = new List<(string, List<StampPart>)>();
        if (!Directory.Exists(StampFolder))
        {
            return stamps;
        }
        foreach (string f in Directory.GetFiles(StampFolder, "*.json").Order())
        {
            try
            {
                var parts = JsonNode.Parse(File.ReadAllText(f))!["objects"]!.AsArray().Select(o => new StampPart(o![0]!.GetValue<string>(), o[1]!.GetValue<float>(), o[2]!.GetValue<float>(),
                    o[3]!.GetValue<float>(), new Vector3(o[4]!.GetValue<float>(), o[5]!.GetValue<float>(), o[6]!.GetValue<float>()), o[7]!.GetValue<float>())).ToList();
                stamps.Add((Path.GetFileNameWithoutExtension(f), parts));
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException or NullReferenceException)
            {
                Log.Caught(e, "reading a stamp " + f);
            }
        }
        return stamps;
    }

    /// <summary>Things (indices) as a stamp file: offsets from their middle, heights above their own ground.</summary>
    public static int SaveStamp(WorldScene s, IReadOnlyCollection<int> things, string name)
    {
        var area = new SceneLayers.AreaView(s);
        List<WorldScene.Thing> picked;
        lock (s.Things)
        {
            picked = [.. things.Where(i => i >= 0 && i < s.Things.Count).Select(i => s.Things[i]).Where(t => !t.Gone && ModelStore.ModelName(t.Prefab) != null)];
        }
        if (picked.Count == 0)
        {
            return 0;
        }
        float cx = picked.Average(t => t.Position.X), cz = picked.Average(t => t.Position.Z);
        static float R(float v) => MathF.Round(v * 1000f) / 1000f;
        var objects = new JsonArray([.. picked.Select(t => (JsonNode)new JsonArray(ModelStore.ModelName(t.Prefab), R(t.Position.X - cx), R(t.Position.Z - cz),
            R(t.Position.Y - area.GroundHeight(t.Position.X, t.Position.Z)), R(t.Rotation.X), R(t.Rotation.Y), R(t.Rotation.Z), R(t.Scale == 0 ? 1 : t.Scale)))]);
        _ = Directory.CreateDirectory(StampFolder);
        string safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_')).Trim();
        File.WriteAllText(Path.Combine(StampFolder, (safe.Length > 0 ? safe : "Stamp") + ".json"), new JsonObject { ["objects"] = objects }.ToJsonString());
        return picked.Count;
    }

    /// <summary>Stamp: puts a saved stamp (Save stamp on the top bar, from a selection) down where clicked.</summary>
    public sealed class StampTool(MainWindow w, List<(string Name, List<StampPart> Parts)> stamps) : Base(w)
    {
        private float _turn;
        private readonly Random _random = new();

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("stamp", "Stamp", "Select objects (E) and press Save stamp on the top bar to keep them as a stamp; here, pick one and click to put it down, each object as high above the ground as it was. R turns it 15°.") { Icon = "M7 4h10a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zM9 13v3h6v-3M4 20h16" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            stamps.Count > 0 ? new ChoiceControl("stamp", "Stamp", [.. stamps.Select(s => $"{s.Name} ({s.Parts.Count})")]) : new TextControl("none", "No stamps yet: select objects with Select (E), then Save stamp on the top bar."),
            new SliderControl("turn", "Turn", 0, 360, 0) { Step = 1, Unit = "°" },
            new ToggleControl("randomTurn", "Random turn", true),
            new SliderControl("size", "Size ×", 0.3f, 3, 1) { Step = 0.05f },
            new SliderControl("sizeJitter", "Random size ±", 0, 0.5f, 0.1f) { Step = 0.01f },
        ];

        private (string Name, List<StampPart> Parts)? Chosen(IToolContext ctx) => stamps.Count > 0 ? stamps[Math.Clamp(ctx.Choice("stamp"), 0, stamps.Count - 1)] : null;

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id) => _turn = id == "turn" ? ctx.Number("turn") : _turn;

        /// <inheritdoc/>
        public override bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
        {
            if (key != "R")
            {
                return false;
            }
            _turn = (_turn + (mods.HasFlag(KeyModifiers.Shift) ? 345 : 15)) % 360;
            ctx.Status($"Turn {_turn:0}°");
            return true;
        }

        private List<(StampPart Part, Vector3 At, float Yaw, float Scale)> Layout(IToolContext ctx, Vector3 c, float turn, float size)
        {
            var area = ctx.Area;
            float a = turn * MathF.PI / 180, cos = MathF.Cos(a), sin = MathF.Sin(a);
            return [.. (Chosen(ctx)?.Parts ?? []).Select(p =>
            {
                // Unity's yaw turns +z towards +x: x' = x cos + z sin, z' = -x sin + z cos
                float x = c.X + (((p.Dx * cos) + (p.Dz * sin)) * size), z = c.Z + (((-p.Dx * sin) + (p.Dz * cos)) * size);
                return (p, new Vector3(x, area.GroundHeight(x, z) + (p.Up * size), z), turn, p.Scale * size);
            })];
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            if (Ground(ctx, input) is not Vector3 at || Session is not { } s || Chosen(ctx) is not { } stamp)
            {
                ctx.Preview([]);
                return;
            }
            var preview = Layout(ctx, at, _turn, ctx.Number("size"));
            ctx.Preview([Dots(preview.Select(p => p.At), 0.3f, new Vector4(1, 0.8f, 0.3f, 0.9f)), Ring(ctx.Area, at, 1, new Vector4(1, 0.8f, 0.3f, 0.9f))]);
            if (input.Action != PointerAction.Press)
            {
                return;
            }
            float turn = ctx.IsOn("randomTurn") ? _random.NextSingle() * 360 : _turn;
            float size = ctx.Number("size") * (1 + ((_random.NextSingle() - 0.5f) * 2 * ctx.Number("sizeJitter")));
            var placed = Layout(ctx, at, turn, size);
            _ = s.Commit($"Stamp {stamp.Name}", null, [], [.. placed.Select(p => New(p.Part.Model, p.At, p.Part.Euler with { Y = p.Part.Euler.Y + p.Yaw }, p.Scale))]);
            ctx.Status($"Stamped {stamp.Name}: {placed.Count} objects");
        }
    }

    /// <summary>River: draws rivers and lakes (MapWater) point by point and cuts their beds into the ground.</summary>
    public sealed class RiverTool(MainWindow w) : Base(w)
    {
        private int _point = -1;
        private bool _dragging;
        private string? _before, _action;
        private (EditSession.Change Change, StepState State, string Label, DateTime At)? _lastStep;

        /// <summary>The rivers before and after one step (a slider dragged on keeps updating its step's after).</summary>
        private sealed class StepState(string before, string after)
        {
            public string Before { get; } = before;
            public string After { get; set; } = after;
        }

        /// <summary>The rivers as they are before an edit.</summary>
        private void Start() => _before ??= MapWater.Snapshot();

        /// <summary>
        /// The edit since Start as a step in the history (Ctrl+Z takes it back); merged into the last one when it is the same
        /// slider moved again within a few seconds.
        /// </summary>
        private void Record(string label, bool merge = false)
        {
            if (_before is not string before || Session is not { } s)
            {
                return;
            }
            _before = null;
            string after = MapWater.Snapshot();
            if (after == before)
            {
                return;
            }
            if (merge && _lastStep is var (change, state, last, at) && last == label && (DateTime.Now - at).TotalSeconds < 3 && s.UndoList.Count > 0 && ReferenceEquals(s.UndoList[^1], change))
            {
                state.After = after;
                _lastStep = (change, state, label, DateTime.Now);
                return;
            }
            var kept = new StepState(before, after);
            _lastStep = (s.RecordExtra(label, again => MapWater.Restore(again ? kept.After : kept.Before)), kept, label, DateTime.Now);
        }

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("river", "Rivers", "Pick a river or lake, then click to add a point, drag a point to move it, Shift+click a point to delete it. The sliders change the picked point and the river or lake. Carve cuts the bed into the ground. Every change is a step in History (Ctrl+Z).") { Icon = "M4 6c3 0 3 3 6 3s3-3 6-3 3 3 4 3M4 12c3 0 3 3 6 3s3-3 6-3 3 3 4 3M4 18c3 0 3 3 6 3s3-3 6-3" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ListControl("body", "Rivers and lakes"),
            new ButtonControl("new", "New river"),
            new ButtonControl("newLake", "New lake"),
            new ButtonControl("delete", "Delete this one"),
            new TextControl("name", ""),
            new SliderControl("width", "Half-width", 0.5f, 40, 6) { Step = 0.5f, Unit = "m" },
            new SliderControl("depth", "Depth", 0.2f, 10, 2.5f) { Step = 0.1f, Unit = "m" },
            new SliderControl("current", "Current", 0, 4, 0.6f) { Step = 0.05f, Unit = "m/s" },
            new SliderControl("top", "Start level", -2, 40, 0.35f) { Step = 0.05f, Unit = "m" },
            new SliderControl("end", "End level", -2, 40, 0.12f) { Step = 0.05f, Unit = "m" },
            new ButtonControl("carve", "Carve this one"),
            new ButtonControl("carveAll", "Carve all"),
            new TextControl("status", ""),
        ];

        private Action? _refresh;

        /// <summary>The river or lake picked: rivers first, then lakes.</summary>
        private static int Body(IToolContext ctx) => ctx.Choice("body");

        private static bool Lakes(IToolContext ctx) => Body(ctx) >= MapWater.Rivers.Count;

        private static MapWater.River? River(IToolContext ctx) => Body(ctx) is int i && i >= 0 && i < MapWater.Rivers.Count ? MapWater.Rivers[i] : null;

        private static MapWater.Lake? Lake(IToolContext ctx) => Body(ctx) - MapWater.Rivers.Count is int i && i >= 0 && i < MapWater.Lakes.Count ? MapWater.Lakes[i] : null;

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            // Undo, redo and Live from Unreal change the rivers too: the list follows.
            MapWater.Changed -= _refresh;
            _refresh = () => List(ctx);
            MapWater.Changed += _refresh;
            List(ctx);
            Fields(ctx);
            Show(ctx, null);
        }

        /// <inheritdoc/>
        public override void OnLeft(IToolContext ctx)
        {
            MapWater.Changed -= _refresh;
        }

        /// <summary>The rivers and lakes as cards (picking one, or keeping the pick in range).</summary>
        private static void List(IToolContext ctx, int? pick = null)
        {
            int count = MapWater.Rivers.Count + MapWater.Lakes.Count;
            ctx.SetList("body", [
                .. MapWater.Rivers.Select(r => (r.Name, $"{r.Points.Count} points · {r.Top:0.##} → {r.End:0.##} m", "River")),
                .. MapWater.Lakes.Select(l => (l.Name, $"{l.Outline.Count} points · level {l.Level:0.##} m", "Lake")),
            ], Math.Clamp(pick ?? Body(ctx), 0, Math.Max(0, count - 1)));
        }

        /// <summary>The sliders show the picked river's or lake's values and its picked point's; the line says which.</summary>
        private void Fields(IToolContext ctx)
        {
            if (Lake(ctx) is { } l)
            {
                ctx.SetNumber("top", l.Level);
                ctx.SetNumber("depth", l.Depth);
                ctx.SetText("name", $"{l.Name.ToUpperInvariant()}: Start level is its level; Depth its depth");
            }
            else if (River(ctx) is { } r)
            {
                ctx.SetNumber("top", r.Top);
                ctx.SetNumber("end", r.End);
                if (_point >= 0 && _point < r.Points.Count)
                {
                    var q = r.Points[_point];
                    ctx.SetNumber("width", q.HalfWidth);
                    ctx.SetNumber("depth", q.Depth);
                    ctx.SetNumber("current", q.Current);
                }
                ctx.SetText("name", _point >= 0 && _point < r.Points.Count ? $"POINT {_point + 1} OF {r.Name.ToUpperInvariant()}" : $"{r.Name.ToUpperInvariant()}: click a point to change it");
            }
            else
            {
                ctx.SetText("name", "No rivers or lakes: New river or New lake makes one.");
            }
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            _point = id is "body" or "new" or "newLake" or "delete" ? -1 : _point;
            if (id is not ("body" or "carve" or "carveAll"))
            {
                Start();
            }
            switch (id)
            {
                case "newLake":
                    MapWater.Lakes.Add(new MapWater.Lake { Name = $"Lake {MapWater.Lakes.Count + 1}", Level = ctx.Number("top"), Depth = ctx.Number("depth") });
                    List(ctx, MapWater.Rivers.Count + MapWater.Lakes.Count - 1);
                    ctx.Status($"{MapWater.Lakes[^1].Name}: click its outline.");
                    break;
                case "new":
                    MapWater.Rivers.Add(new MapWater.River { Name = $"River {MapWater.Rivers.Count + 1}", Top = ctx.Number("top"), End = ctx.Number("end") });
                    List(ctx, MapWater.Rivers.Count - 1);
                    ctx.Status($"{MapWater.Rivers[^1].Name}: click its points, source first.");
                    break;
                case "delete" when Lake(ctx) is { } gone:
                    _ = MapWater.Lakes.Remove(gone);
                    break;
                case "delete" when River(ctx) is { } dropped:
                    _ = MapWater.Rivers.Remove(dropped);
                    break;
                case "width" or "depth" or "current" when River(ctx) is { } pr && _point >= 0 && _point < pr.Points.Count:
                    pr.Points[_point] = pr.Points[_point] with { HalfWidth = ctx.Number("width"), Depth = ctx.Number("depth"), Current = ctx.Number("current") };
                    break;
                case "depth" or "top" when Lake(ctx) is { } lake:
                    lake.Depth = ctx.Number("depth");
                    lake.Level = ctx.Number("top");
                    break;
                case "top" or "end" when River(ctx) is { } river:
                    river.Top = ctx.Number("top");
                    river.End = ctx.Number("end");
                    break;
                case "carve":
                    Carve(ctx, Lake(ctx) is { } l ? [l] : [], River(ctx) is { } r ? [r] : []);
                    break;
                case "carveAll":
                    Carve(ctx, MapWater.Lakes, MapWater.Rivers);
                    break;
                default:
                    break;
            }
            if (id is not ("body" or "carve" or "carveAll"))
            {
                MapWater.Touch();
                Record(id switch
                {
                    "new" => "New river",
                    "newLake" => "New lake",
                    "delete" => "Delete river or lake",
                    "width" or "depth" or "current" when River(ctx) != null => "River point " + id,
                    _ => (Lakes(ctx) ? "Lake " : "River ") + id,
                }, merge: id is not ("new" or "newLake" or "delete"));
            }
            List(ctx);
            Fields(ctx);
            Show(ctx, null);
        }

        private void Carve(IToolContext ctx, IReadOnlyList<MapWater.Lake> lakes, IReadOnlyList<MapWater.River> rivers)
        {
            if (Session is not { } s)
            {
                return;
            }
            int n = 0;
            foreach (var r in rivers.Where(r => r.Points.Count > 1))
            {
                float[] h = MapWater.Surfaces(r);
                float m = (r.Points.Max(p => p.HalfWidth) * 2) + 1;
                n += MapWater.Carve(s, "Carve " + r.Name, p => MapWater.RiverBed(r, h, p),
                    (r.Points.Min(p => p.X) - m, r.Points.Min(p => p.Y) - m, r.Points.Max(p => p.X) + m, r.Points.Max(p => p.Y) + m));
            }
            foreach (var l in lakes.Where(l => l.Outline.Count > 2))
            {
                n += MapWater.Carve(s, "Carve " + l.Name, p => MapWater.LakeBed(l, p),
                    (l.Outline.Min(p => p.X) - 7, l.Outline.Min(p => p.Y) - 7, l.Outline.Max(p => p.X) + 7, l.Outline.Max(p => p.Y) + 7));
            }
            ctx.Status($"Carved {n} ground points.");
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
            var at = Ground(ctx, input);
            if (at is Vector3 p)
            {
                var u = new Vector2(p.X, -p.Z);   // Unreal metres
                bool lakes = Lakes(ctx);
                var points = lakes ? Lake(ctx)?.Outline.ToList() : River(ctx)?.Points.ConvertAll(q => new Vector2(q.X, q.Y));
                switch (input.Action)
                {
                    case PointerAction.Press when points != null:
                        Start();
                        int near = points.FindIndex(q => Vector2.Distance(q, u) < 3);
                        _action = near < 0 ? "Add point" : input.Modifiers.HasFlag(KeyModifiers.Shift) ? "Delete point" : "Move point";
                        if (near >= 0 && input.Modifiers.HasFlag(KeyModifiers.Shift))
                        {
                            if (lakes)
                            {
                                Lake(ctx)!.Outline.RemoveAt(near);
                            }
                            else
                            {
                                River(ctx)!.Points.RemoveAt(near);
                            }
                            _point = -1;
                            MapWater.Touch();
                        }
                        else if (near >= 0)
                        {
                            _point = near;
                            _dragging = true;
                        }
                        else
                        {
                            _point = Add(ctx, u, points);
                            _dragging = true;
                            MapWater.Touch();
                        }
                        Fields(ctx);
                        break;
                    case PointerAction.Drag when _dragging && _point >= 0:
                        if (lakes && Lake(ctx) is { } l && _point < l.Outline.Count)
                        {
                            l.Outline[_point] = u;
                        }
                        else if (River(ctx) is { } r && _point < r.Points.Count)
                        {
                            r.Points[_point] = r.Points[_point] with { X = u.X, Y = u.Y };
                        }
                        MapWater.Touch();
                        break;
                    case PointerAction.Release:
                        _dragging = false;
                        Record((Lakes(ctx) ? "Lake: " : "River: ") + (_action ?? "edit"));
                        break;
                    default:
                        break;
                }
            }
            Show(ctx, at);
        }

        /// <summary>A new point where it fits: inserted in the nearest stretch, or added at the nearer end.</summary>
        private static int Add(IToolContext ctx, Vector2 u, List<Vector2> points)
        {
            int at = points.Count;
            if (points.Count >= 2)
            {
                float best = float.MaxValue;
                int count = Lakes(ctx) ? points.Count : points.Count - 1;
                for (int i = 0; i < count; i++)
                {
                    float d = MapWater.SegmentDistance(u, points[i], points[(i + 1) % points.Count], out float t);
                    if (d < best && t > 0.02f && t < 0.98f)
                    {
                        (best, at) = (d, i + 1);
                    }
                }
                if (best > 12 && !Lakes(ctx))
                {
                    at = Vector2.Distance(u, points[0]) < Vector2.Distance(u, points[^1]) ? 0 : points.Count;
                }
                else if (best > 12)
                {
                    at = points.Count;
                }
            }
            if (Lakes(ctx))
            {
                Lake(ctx)!.Outline.Insert(at, u);
            }
            else
            {
                River(ctx)!.Points.Insert(at, new MapWater.Point(u.X, u.Y, ctx.Number("width"), ctx.Number("depth"), ctx.Number("current")));
            }
            return at;
        }

        private void Show(IToolContext ctx, Vector3? at)
        {
            var area = ctx.Area;
            var shapes = new List<Shape>();
            // Each point a little over the water (or the ground where that is higher), in the editor's frame.
            Vector3 Lift(float x, float y, float aboveSea) => MapWater.Editor(x, y, MathF.Max(aboveSea, area.GroundHeight(x, -y)) + 0.4f);
            var (white, orange, edge) = (new Vector4(1, 1, 1, 0.95f), new Vector4(1, 0.6f, 0.1f, 1), new Vector4(0.55f, 0.9f, 1, 0.75f));
            List<Vector3> pts = [];
            if (Lake(ctx) is { } lake)
            {
                pts = [.. lake.Outline.Select(q => Lift(q.X, q.Y, lake.Level))];
                if (pts.Count > 1)
                {
                    shapes.Add(Band(pts, 0.35f, edge, closed: true));
                }
            }
            else if (River(ctx) is { } r && r.Points.Count > 0)
            {
                float[] h = MapWater.Surfaces(r);
                pts = [.. r.Points.Select((q, i) => Lift(q.X, q.Y, h[i]))];
                if (pts.Count > 1)
                {
                    // The middle line, and the water's edges at each point's half-width.
                    shapes.Add(Band(pts, 0.35f, white));
                    // Which way it runs: an arrow halfway along each stretch, pointing downstream.
                    var arrows = new List<Vector3>();
                    var tris = new List<int>();
                    for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        var (a, b) = (pts[i], pts[i + 1]);
                        var d = new Vector3(b.X - a.X, 0, b.Z - a.Z);
                        if (d.Length() < 4)
                        {
                            continue;
                        }
                        d = Vector3.Normalize(d) * 1.6f;
                        var side = new Vector3(-d.Z, 0, d.X);
                        var m = ((a + b) / 2) + new Vector3(0, 0.05f, 0);
                        int k = arrows.Count;
                        arrows.AddRange([m + d, m - (d * 0.6f) + side, m - (d * 0.6f) - side]);
                        tris.AddRange([k, k + 1, k + 2]);
                    }
                    shapes.Add(new Shape([.. arrows], [.. tris], edge) { BothSides = true, WorldSpace = true });
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var e = new List<Vector3>();
                        for (int i = 0; i < r.Points.Count; i++)
                        {
                            var (pa, pb) = (r.Points[Math.Max(0, i - 1)], r.Points[Math.Min(r.Points.Count - 1, i + 1)]);
                            var d = Vector2.Normalize(new Vector2(pb.X - pa.X, pb.Y - pa.Y));
                            var o = new Vector2(-d.Y, d.X) * r.Points[i].HalfWidth * side;
                            e.Add(Lift(r.Points[i].X + o.X, r.Points[i].Y + o.Y, h[i]));
                        }
                        shapes.Add(Band(e, 0.25f, edge));
                    }
                }
            }
            shapes.Add(Discs(pts, 1.1f, white));
            if (_point >= 0 && _point < pts.Count)
            {
                shapes.Add(Discs([pts[_point] + new Vector3(0, 0.05f, 0)], 1.6f, orange));
            }
            if (at is Vector3 p)
            {
                shapes.Add(Ring(area, p, 3, new Vector4(0.6f, 0.85f, 1, 0.9f)));
                // Over a point: what it is.
                int near = pts.FindIndex(q => Vector2.Distance(new(q.X, q.Z), new(p.X, p.Z)) < 3);
                if (near >= 0 && River(ctx) is { } rv)
                {
                    var q = rv.Points[near];
                    ctx.Status($"Point {near + 1} of {rv.Name} · {q.HalfWidth * 2:0.#} m wide · {q.Depth:0.#} m deep · {MapWater.Surfaces(rv)[near]:0.00} m above sea");
                }
                else if (near >= 0 && Lake(ctx) is { } lk)
                {
                    ctx.Status($"Point {near + 1} of {lk.Name} · level {lk.Level:0.00} m · {lk.Depth:0.#} m deep");
                }
            }
            ctx.Preview(shapes);
        }
    }

    /// <summary>Each model's group (its catalog category; markers "Markers"; anything else "Other"), by prefab hash.</summary>
    private static Dictionary<int, (string Group, string Label)>? _groups;

    /// <summary>Another map (or catalog): the groups are read again.</summary>
    internal static void ForgetGroups() => _groups = null;

    /// <summary>A thing's group and label.</summary>
    public static (string Group, string Label) GroupOf(int prefab)
    {
        if (_groups == null)
        {
            _groups = [];
            foreach (var k in Catalog().Kinds)
            {
                _groups[AssetIds.Of(k.Model)] = (k.Category, k.Label);
            }
        }
        string? name = ModelStore.ModelName(prefab);
        return _groups.TryGetValue(prefab, out var g) || (name?.IndexOf('@') is > 0 and int at && _groups.TryGetValue(AssetIds.Of(name[..at]), out g)) ? g : ("Other", name ?? "?");
    }

    /// <summary>The groups the Layers tool offers: the catalog's, then Other.</summary>
    private static List<string> GroupNames(List<Kind> kinds) => [.. kinds.Select(k => k.Category).Distinct(), "Other"];

    /// <summary>The groups hidden (Layers); kept while the map is open, hidden again after each change.</summary>
    public static HashSet<string> Hidden { get; } = [];

    /// <summary>Hides the things of the hidden groups (after a change too: new things of a hidden group stay hidden).</summary>
    public static void ApplyHidden(MainWindow w)
    {
        if (w.Session?.Scene is not { IsMap: true } s)
        {
            return;
        }
        var hide = new List<int>();
        if (Hidden.Count > 0)
        {
            lock (s.Things)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    if (!s.Things[i].Gone && Hidden.Contains(GroupOf(s.Things[i].Prefab).Group))
                    {
                        hide.Add(i);
                    }
                }
            }
        }
        w.View.HideOnly(hide);
    }

    /// <summary>Layers: shows or hides each group of objects, and counts them.</summary>
    public sealed class LayersTool(MainWindow w, List<string> groups) : Base(w)
    {
        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("layers", "Layers", "Shows or hides each group of objects (hidden ones cannot be picked either), and counts what stands on the map.");

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            .. groups.Select(g => (ToolControl)new ToggleControl("show." + g.Replace(' ', '_'), g, true)),
            new ToggleControl("water", "Water", true),
            new ButtonControl("all", "Show all"),
        ];

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx) => Apply(ctx);

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id == "all")
            {
                Hidden.Clear();
                ApplyHidden(W);
                W.WaterBox.IsChecked = true;
                Count(ctx);
                ctx.Status("All groups shown (the switches follow when you choose Layers again).");
                return;
            }
            Apply(ctx);
        }

        private void Apply(IToolContext ctx)
        {
            Hidden.Clear();
            foreach (string g in groups.Where(g => !ctx.IsOn("show." + g.Replace(' ', '_'))))
            {
                _ = Hidden.Add(g);
            }
            ApplyHidden(W);
            W.WaterBox.IsChecked = ctx.IsOn("water");
            Count(ctx);
        }

        /// <summary>Each group's switch says how many it has (hidden ones too).</summary>
        private void Count(IToolContext ctx)
        {
            if (Session is not { } s)
            {
                return;
            }
            var counts = new Dictionary<string, int>();
            lock (s.Scene.Things)
            {
                foreach (var t in s.Scene.Things.Where(t => !t.Gone))
                {
                    string g = GroupOf(t.Prefab).Group;
                    counts[g] = counts.GetValueOrDefault(g) + 1;
                }
            }
            foreach (string g in groups)
            {
                ctx.SetText("show." + g.Replace(' ', '_'), $"{g}   {counts.GetValueOrDefault(g):N0}");
            }
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
        }
    }

    /// <summary>Checks: finds objects floating, buried, under water or on too steep ground; jumps to them one by one.</summary>
    public sealed class ChecksTool(MainWindow w) : Base(w)
    {
        private List<(int Index, string Problem, Vector3 At)> _found = [];
        private int _next;

        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("checks", "Checks", "Finds trees, plants and rocks floating above the ground, buried in it, standing under water or on ground too steep for them. Next jumps to each and selects it; Select all lets you delete them or Settle objects in one go.");

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new SliderControl("float", "Floating more than", 0.05f, 2, 0.3f) { Step = 0.05f, Unit = "m" },
            new SliderControl("buried", "Buried more than", 0.2f, 5, 1) { Step = 0.1f, Unit = "m" },
            new SliderControl("steep", "Trees steeper than", 10, 90, 40) { Step = 1, Unit = "°" },
            new ToggleControl("water", "Under water (not reeds)", true),
            new ButtonControl("find", "Find problems"),
            new TextControl("result", ""),
            new ListControl("groups", "Found (click one to select those)"),
            new ButtonControl("next", "Next problem"),
            new ButtonControl("select", "Select all of them"),
        ];

        /// <summary>A problem's kind: floating, buried, under water or on steep ground.</summary>
        private static string KindOf(string problem) => problem.Contains("floating") ? "Floating" : problem.Contains("buried") ? "Buried" : problem.Contains("water") ? "Under water" : "On steep ground";

        private List<IGrouping<string, (int Index, string Problem, Vector3 At)>> Groups() => [.. _found.GroupBy(f => KindOf(f.Problem)).OrderByDescending(g => g.Count())];

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (Session is not { } s)
            {
                return;
            }
            switch (id)
            {
                case "find":
                    Find(ctx, s);
                    break;
                case "next" when _found.Count > 0:
                    var (index, problem, at) = _found[_next % _found.Count];
                    _next++;
                    W.View.Orbit(at.X, at.Z, 30, 40, 18);
                    W.View.Select([index]);
                    ctx.Status($"{_next} of {_found.Count}: {problem} at x {at.X:0}, z {at.Z:0}");
                    break;
                case "groups" when Groups() is var groups && ctx.Choice("groups") is int g && g >= 0 && g < groups.Count:
                    var first = groups[g].First();
                    W.View.Orbit(first.At.X, first.At.Z, 30, 55, 60);
                    W.View.Select(groups[g].Select(f => f.Index));
                    ctx.Status($"Selected {groups[g].Count():N0} {groups[g].Key.ToLowerInvariant()}: Settle or Delete on the selection bar.");
                    break;
                case "select" when _found.Count > 0:
                    W.View.Select(_found.Select(f => f.Index));
                    ctx.Status($"Selected {_found.Count}: Delete takes them away, Settle objects puts them on the ground.");
                    break;
                default:
                    ctx.Status("Find problems first.");
                    break;
            }
        }

        private void Find(IToolContext ctx, EditSession s)
        {
            var found = FindProblems(s, ctx.Area, Sea, ctx.Number("float"), ctx.Number("buried"), ctx.Number("steep"), ctx.IsOn("water"));
            _found = found;
            W.ChecksFound(found.Count);
            _next = 0;
            // A card per kind of problem: how many, and the kinds of object most often in it.
            ctx.SetList("groups", [.. Groups().Select(g => (g.Key, string.Join(", ", g.GroupBy(f => f.Problem.Split(" floating")[0].Split(" buried")[0].Split(" under water")[0].Split(" on steep")[0]).OrderByDescending(k => k.Count()).Take(3).Select(k => $"{k.Key} {k.Count()}")), $"{g.Count():N0}"))], -1);
            ctx.SetText("result", found.Count == 0 ? "No problems found." : $"{found.Count:N0} problems.");
            ctx.Status(found.Count == 0 ? "No problems found." : "Next jumps to them one by one.");
        }

        /// <summary>
        /// The trees, plants and rocks floating above the ground, buried in it, under the sea, a river or a lake (water: reeds
        /// aside) or (trees) on ground steeper than steep degrees; buildings and props (any kind not a tree, bush, plant or
        /// rock) on ground steeper than steepBuildings: each one's index, what is wrong, where.
        /// </summary>
        internal static List<(int Index, string Problem, Vector3 At)> FindProblems(EditSession s, IAreaView area, float sea, float floatBy, float buriedBy, float steep, bool water, float steepBuildings = 90)
        {
            var found = new List<(int, string, Vector3)>();
            lock (s.Scene.Things)
            {
                // ponytail: an island's reach is a circle a third wider than its mean radius, not its own outline
                var islands = s.Scene.Things.Where(t => !t.Gone && GroupOf(t.Prefab).Group == FloatingIsland.Category)
                    .Select(t => (t.Position, Reach: FloatingIsland.Radius * (t.Scale > 0 ? t.Scale : 1) * 1.3f)).ToList();
                for (int i = 0; i < s.Scene.Things.Count; i++)
                {
                    var t = s.Scene.Things[i];
                    var (group, label) = GroupOf(t.Prefab);
                    // islands hang in the air, and what grows on them stands on them, not on the ground
                    bool onIsland = islands.Any(isl => t.Position.Y > isl.Position.Y - 1 && Vector2.Distance(new(t.Position.X, t.Position.Z), new(isl.Position.X, isl.Position.Z)) < isl.Reach);
                    if (t.Gone || group is "Markers" or FloatingIsland.Category || onIsland)
                    {
                        continue;
                    }
                    float ground = area.GroundHeight(t.Position.X, t.Position.Z), up = t.Position.Y - ground;
                    if (group is not ("Trees" or "Bushes" or "Plants" or "Rocks" or "Grass"))
                    {
                        // ponytail: the ground's rise over 4 m across the spot, not the model's own footprint.
                        float rise = MathF.Max(MathF.Abs(area.GroundHeight(t.Position.X + 2, t.Position.Z) - area.GroundHeight(t.Position.X - 2, t.Position.Z)),
                            MathF.Abs(area.GroundHeight(t.Position.X, t.Position.Z + 2) - area.GroundHeight(t.Position.X, t.Position.Z - 2)));
                        float deg = MathF.Atan(rise / 4) * 180 / MathF.PI;
                        if (deg > steepBuildings)
                        {
                            found.Add((i, $"{label} on steep ground {deg:0}°", t.Position));
                        }
                        continue;
                    }
                    float wet = MathF.Max(sea, MapWater.SurfaceAt(t.Position.X, t.Position.Z) ?? float.MinValue);
                    string? problem = up > floatBy ? $"{label} floating {up:0.0} m"
                        : group != "Rocks" && -up > buriedBy ? $"{label} buried {-up:0.0} m"
                        : water && group != "Rocks" && ground < wet - 0.05f && !label.Contains("Reed", StringComparison.OrdinalIgnoreCase) && !label.Contains("Grass Wide", StringComparison.OrdinalIgnoreCase) ? $"{label} under water"
                        : group == "Trees" && MathF.Acos(Math.Clamp(Normal(area, t.Position.X, t.Position.Z).Y, -1, 1)) * 180 / MathF.PI > steep ? $"{label} on steep ground"
                        : null;
                    if (problem != null)
                    {
                        found.Add((i, problem, t.Position));
                    }
                }
            }
            return found;
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
        }
    }

    /// <summary>Grow: makes the map bigger (saves first, then opens it again).</summary>
    public sealed class GrowTool(MainWindow w) : Base(w)
    {
        /// <inheritdoc/>
        public override ToolInfo Info { get; } = new("grow", "Grow map", "Makes the map bigger by 63 m chunks (Unreal's landscape components): all round or on one side, the new ground flat or generated to carry on from the edge. Saves first, then opens the bigger map. Send to Unreal then rebuilds its landscape at the new size.") { Icon = "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zM12 8v8M8 12h8" };

        /// <inheritdoc/>
        public override IReadOnlyList<ToolControl> Controls { get; } =
        [
            new ChoiceControl("side", "Where", ["All round", "North (up the map)", "South", "East", "West (the sea)"]),
            new SliderControl("chunks", "Chunks of 63 m", 1, 8, 1) { Step = 1 },
            new ChoiceControl("fill", "New ground", ["Generated, carrying on from the edge", "Flat"]),
            new SliderControl("height", "Flat height above the sea", -15, 20, 1) { Step = 0.5f, Unit = "m" },
            new SliderControl("seed", "Seed (generated)", 0, 999, 7) { Step = 1 },
            new ButtonControl("grow", "Save and grow"),
            new TextControl("status", ""),
        ];

        /// <inheritdoc/>
        public override void OnChosen(IToolContext ctx)
        {
            var m = MapProject.Current!.ReadRaster();
            ctx.SetText("status", $"Now {m.Cols} x {m.Rows} m.");
        }

        /// <inheritdoc/>
        public override void OnControl(IToolContext ctx, string id)
        {
            if (id != "grow")
            {
                return;
            }
            var side = ctx.Choice("side") switch
            {
                1 => MapProject.Sides.North,
                2 => MapProject.Sides.South,
                3 => MapProject.Sides.East,
                4 => MapProject.Sides.West,
                _ => MapProject.Sides.North | MapProject.Sides.South | MapProject.Sides.East | MapProject.Sides.West,
            };
            int metres = (int)ctx.Number("chunks") * 63;
            string where = ctx.Choice("side") == 0 ? "on every side" : "to the " + side.ToString().ToLowerInvariant();
            _ = Grow();
            // It changes the map's size: asked first.
            async Task Grow()
            {
                if (await Dialogs.Ask(W, "Grow the map?", $"Adds {metres} m {where}. The map is saved first; Unreal rebuilds its landscape at the new size the next time you send.", "Grow", "Not now"))
                {
                    await W.GrowMap(side, metres, ctx.Choice("fill") == 0, ctx.Number("height"), (int)ctx.Number("seed"));
                }
            }
        }

        /// <inheritdoc/>
        public override void OnPointer(IToolContext ctx, PointerInput input)
        {
        }
    }
}
