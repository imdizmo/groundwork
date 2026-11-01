using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

// The newer map tools for Claude: paths and ramps, areas, holes, terraces, whole-map passes, zones, and the Unreal
// actions (play here, make the landscape). Coordinates are the editor's (x east, z north, metres), as the other map tools.
public sealed partial class ClaudeTools
{
    /// <summary>A map layer by name (map_catalog's layers), or -1 for none / not found.</summary>
    private static int LayerNamed(string? name) =>
        name is { Length: > 0 } && MapProject.Current is { } m ? m.Layers.FindIndex(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)) : -1;

    /// <summary>The grid points within radius metres of x, z (not the locked edge), with their distance.</summary>
    private static IEnumerable<(int P, float D)> Circle(Ground g, float x, float z, float radius)
    {
        float cx = x - g.OriginX, cz = z - g.OriginZ;
        for (int gz = Math.Max(1, (int)MathF.Floor(cz - radius)); gz <= Math.Min(g.H - 2, (int)MathF.Ceiling(cz + radius)); gz++)
        {
            for (int gx = Math.Max(1, (int)MathF.Floor(cx - radius)); gx <= Math.Min(g.W - 2, (int)MathF.Ceiling(cx + radius)); gx++)
            {
                float d = MathF.Sqrt(((gx - cx) * (gx - cx)) + ((gz - cz) * (gz - cz)));
                if (d <= radius)
                {
                    yield return ((gz * g.W) + gx, d);
                }
            }
        }
    }

    /// <summary>The grid box round some points, a point wider.</summary>
    private static (int, int, int, int) Box(Ground g, List<int> points) =>
        points.Count == 0 ? (0, 0, 0, 0) : (points.Min(p => p % g.W) - 1, points.Min(p => p / g.W) - 1, points.Max(p => p % g.W) + 1, points.Max(p => p / g.W) + 1);

    /// <inheritdoc/>
    [McpServerTool(Name = "map_path", Title = "Make a path, road or ramp on the map")]
    [Description("As the Path tool: the ground levelled along a line of points (width, with shoulders blending into the ground round it), following the ground smoothed or (even) in straight slopes between the points (two points: a ramp), a layer (map_catalog's layers) painted on it, and a kind (map_catalog) put along its edges every so many metres. One undo step.")]
    public Task<string> MapPath([Description("Points: [x, z] each, in order.")] float[][] points, float width = 3, float shoulder = 3,
        [Description("Straight slopes between the points (a ramp); else the ground's own rise and fall, smoothed.")] bool even = false, float smoothing = 15,
        [Description("The layer painted on it (none: no paint).")] string? layer = null, [Description("A kind put along its edges (none: nothing).")] string? edge = null,
        float spacing = 3, [Description("How far past the edge, metres.")] float offset = 0.5f, bool bothSides = true) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        if (points.Length < 2 || points.Any(p => p.Length < 2))
        {
            return "A path needs two points or more, [x, z] each.";
        }
        string? model = null;
        if (edge is { Length: > 0 })
        {
            var (kinds, _, _) = MapTools.Catalog();
            var (found, _) = KindsNamed(kinds, [edge]);
            if (found.Count == 0)
            {
                return $"No kind \"{edge}\" (map_catalog lists them).";
            }
            model = kinds[found[0]].Model;
        }
        int paint = LayerNamed(layer);
        if (layer is { Length: > 0 } && paint < 0)
        {
            return $"No layer \"{layer}\": {string.Join(", ", MapProject.Current!.Layers.Select(l => l.Name))}.";
        }
        var spec = new Paths.Spec(width, shoulder, even, smoothing, paint, model, spacing, offset, bothSides);
        var adds = new List<(NewObject Object, bool Piece)>();
        string label = even && points.Length == 2 ? "Ramp" : "Path";
        var line = points.Select(p => new Vector2(p[0], p[1])).ToList();
        var touched = _w.Session!.Commit(label + " (Claude)", g => Paths.Apply(g, line, spec, adds), [], adds);
        return $"{label} made: {line.Zip(line.Skip(1), Vector2.Distance).Sum():0} m long" + (adds.Count > 0 ? $", {adds.Count} objects along it." : ".");
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_area", Title = "Add or change an area on the map")]
    [Description("An area (a box that becomes an Unreal volume: map_catalog's area kinds, such as a trigger or a blocking wall): its middle x, z, width (along its turn) and depth, height, turn in degrees, and its bottom (none: a metre under the ground at its middle). An area of the same name is replaced. One undo step.")]
    public Task<string> MapArea(string kind, float x, float z, float width = 10, float depth = 10, float height = 5, float turn = 0, float? bottom = null, string? name = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var kinds = MapTools.AreaKinds();
        var k = kinds.FirstOrDefault(a => string.Equals(a.Id, kind, StringComparison.OrdinalIgnoreCase) || string.Equals(a.Label, kind, StringComparison.OrdinalIgnoreCase));
        if (k == null)
        {
            return $"No area kind \"{kind}\": {string.Join(", ", kinds.Select(a => a.Label))}.";
        }
        float b = bottom ?? MathF.Round(new SceneLayers.AreaView(_w.Session!.Scene).GroundHeight(x, z) - 1);
        string before = MapAreas.Snapshot();
        var area = new MapAreas.Area { Name = name ?? MapAreas.NewName(k.Label), Kind = k.Id, X = x, Y = -z, Width = width, Depth = depth, Bottom = b, Top = b + height, Yaw = turn };
        int at = MapAreas.All.FindIndex(a => a.Name == area.Name);
        if (at >= 0)
        {
            MapAreas.All[at] = area;
        }
        else
        {
            MapAreas.All.Add(area);
        }
        MapAreas.Touch();
        string after = MapAreas.Snapshot();
        _ = _w.Session!.RecordExtra("Area " + area.Name + " (Claude)", again => MapAreas.Restore(again ? after : before));
        return $"Area {area.Name}: {k.Label}, {width:0.#} x {depth:0.#} x {height:0.#} m, bottom {b:0.#} m.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_area_remove", Title = "Remove an area from the map")]
    [Description("Removes the area of that name (map_catalog lists them). One undo step.")]
    public Task<string> MapAreaRemove(string name) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        string before = MapAreas.Snapshot();
        if (MapAreas.All.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return $"No area \"{name}\".";
        }
        MapAreas.Touch();
        string after = MapAreas.Snapshot();
        _ = _w.Session!.RecordExtra("Remove area " + name + " (Claude)", again => MapAreas.Restore(again ? after : before));
        return $"Area {name} removed.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_hole", Title = "Make or fill a hole in the ground")]
    [Description("As the Hole brush: the ground taken away in a circle (x, z, radius): a cave mouth, a pit (Unreal hides its landscape there). fill: puts the ground back. One undo step.")]
    public Task<string> MapHole(float x, float z, float radius = 3, bool fill = false) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        int n = _w.Session!.EditGround((fill ? "Fill hole" : "Hole") + " (Claude)", g =>
        {
            var touched = new List<int>();
            foreach (var (p, _) in Circle(g, x, z, radius))
            {
                Sculpt.HolePoint(g, p, 1, fill);
                touched.Add(p);
            }
            return (touched, Box(g, touched));
        }).Count;
        return n > 0 ? $"{(fill ? "Filled" : "Hole made")}: {n} ground points." : "Nothing changed (outside the map?).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_terrace", Title = "Terrace the ground")]
    [Description("As the Terrace brush in one dab: the ground in a circle (x, z, radius) pulled into flat shelves step metres apart joined by steep rises, fully in the middle and less towards the rim (strength 0..1). One undo step.")]
    public Task<string> MapTerrace(float x, float z, float radius = 30, float step = 3, float strength = 1) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        int n = _w.Session!.EditGround("Terrace (Claude)", g =>
        {
            var touched = new List<int>();
            foreach (var (p, d) in Circle(g, x, z, radius))
            {
                float w = Math.Clamp(strength, 0, 1) * (1 - MathF.Pow(d / radius, 4)), h = g.HeightOf(p);
                g.SetHeight(p, h + ((Sculpt.TerraceTarget(h, step) - h) * w));
                touched.Add(p);
            }
            return (touched, Box(g, touched));
        }).Count;
        return $"Terraced {n} ground points.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_paint_whole", Title = "Paint a layer over the whole map")]
    [Description("As Paint the whole map: a layer (map_catalog's layers) over every ground point between the heights and slopes given (each limit left out: none). Sand on every beach: maxHeight 2; rock on every cliff: minSlope 35. One undo step.")]
    public Task<string> MapPaintWhole(string layer, float? minHeight = null, float? maxHeight = null, float? minSlope = null, float? maxSlope = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        int k = LayerNamed(layer);
        if (k < 0)
        {
            return $"No layer \"{layer}\": {string.Join(", ", MapProject.Current!.Layers.Select(l => l.Name))}.";
        }
        var s = _w.Session!;
        var g = s.Ground;
        float Slope(int p)
        {
            int gx = p % g.W, gz = p / g.W, l = Math.Max(0, gx - 1), r = Math.Min(g.W - 1, gx + 1), u = Math.Max(0, gz - 1), dn = Math.Min(g.H - 1, gz + 1);
            float dx = (g.HeightOf((gz * g.W) + r) - g.HeightOf((gz * g.W) + l)) / Math.Max(1, r - l), dz = (g.HeightOf((dn * g.W) + gx) - g.HeightOf((u * g.W) + gx)) / Math.Max(1, dn - u);
            return MathF.Atan(MathF.Sqrt((dx * dx) + (dz * dz))) * 180 / MathF.PI;
        }
        float Mask(int p)
        {
            float h = g.HeightOf(p);
            return (minHeight is not float lo || h >= lo) && (maxHeight is not float hi || h <= hi)
                && (minSlope is null && maxSlope is null || (Slope(p) is var sl && (minSlope is not float sa || sl >= sa) && (maxSlope is not float sb || sl <= sb))) ? 1 : 0;
        }
        float[] colour = Brush.PaintOf(Brush.PaintFor(k))!;
        int n = s.EditGround($"{MapProject.Current!.Layers[k].Name} over the whole map (Claude)", gr => Sculpt.FillPaint(gr, colour, Mask)).Count;
        return $"{MapProject.Current.Layers[k].Name} painted on {n} ground points.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_erode_whole", Title = "Erode the whole map")]
    [Description("As Erode the whole map: the whole ground weathered at a strength 0..1, Thermal (slopes slide to restAngle degrees) or Water (rain carving gullies and fans). One undo step.")]
    public Task<string> MapErodeWhole(bool water = false, float strength = 0.4f, float restAngle = 33, int seed = 1) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        int n = _w.Session!.EditGround("Erode the whole map (Claude)", g => Sculpt.ErodeWhole(g, water, restAngle, Math.Clamp(strength, 0, 1), null, new Random(seed))).Count;
        return $"Eroded: {n} ground points changed.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_fill", Title = "Fill the whole map with kinds")]
    [Description("As the Brush's Fill the whole map: the kinds everywhere at their groups' density times density (map_catalog's groups), with the Brush's patches, slope, water and (useMask) the user's Mask. One undo step.")]
    public Task<string> MapFill([Description("Kinds: models or labels.")] string[] kinds, float density = 1, float spacing = 1, float patchy = 0, float patchSize = 20,
        float keepFromWater = 0, float maxSlope = 35, bool useMask = false, int seed = 1) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var (all, groups, _) = MapTools.Catalog();
        var (found, unknown) = KindsNamed(all, kinds);
        if (found.Count == 0)
        {
            return "None of those kinds are known (map_catalog lists them).";
        }
        var tool = new MapTools.ScatterTool(_w, all, groups, null);
        var card = CardOf(tool, _w.Session!.Scene);
        card.Palettes["kinds"] = found;
        Set(card, 16, density, spacing, 0.9f, 1.1f, patchy, patchSize, 0, keepFromWater, maxSlope, 0, false, false, useMask, seed);
        tool.OnControl(card, "fill");
        return (card.Said.Length > 0 ? card.Said : "Nothing placed.") + (unknown.Count > 0 ? $" Unknown kinds left out: {string.Join(", ", unknown)}." : "");
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_new_zone", Title = "Make a new map that is a whole zone")]
    [Description("A new map (squares of 63 m a side) generated as a whole zone: rolling hills between two heights so far apart, a mountain rim round the edge (0: none), lakes of the sizes given (hectares), woods on a share of the ground at so many trees a hectare, trees at a size times their own, planted with the open map's kinds; then opened. Another seed: another zone.")]
    public Task<string> MapNewZone(string name, int squares = 16, float low = 20, float high = 70, float hillsApart = 300, float rimHeight = 200, float rimWidth = 120,
        float[]? lakes = null, [Description("0..1")] float woods = 0.35f, float treesPerHectare = 20, float treeSize = 1, int seed = 1) => OnUi(async () =>
    {
        var spec = new ZoneGenerator.Spec(low, MathF.Max(low, high), hillsApart, rimHeight, rimWidth, lakes ?? [], woods, treesPerHectare, TreeScale: treeSize, Seed: seed);
        MapProject made;
        try
        {
            made = MapProject.CreateZone(name, squares, spec, ZoneGenerator.Planting.From(MapTools.Catalog().Kinds), _w.NewMapsFolder(), MapProject.Current);
        }
        catch (IOException e)
        {
            return e.Message;
        }
        await _w.ShowMap(made);
        return $"Zone {made.Name} made and opened ({(squares * 63) + 1} m a side).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_play_here", Title = "Play the game in Unreal from a point")]
    [Description("As Play here: starts the game in the open Unreal level (Play In Editor) with the player on the ground at x, z, facing degrees round from north (90: east). Needs Unreal with the Groundwork Bridge plugin.")]
    public Task<string> MapPlayHere(float x, float z, float facing = 0) => OnUi(async () =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        float y = new SceneLayers.AreaView(_w.Session!.Scene).GroundHeight(x, z);
        return await UnrealLive.Play(new Vector3(x, y, z), -facing * MathF.PI / 180);
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_make_landscape", Title = "Make the Unreal landscape from the map")]
    [Description("As Make the Unreal landscape from this map: the map's landscape made in the open Unreal level (its size, place and heights now); a level that has one is rebuilt only with replace. The editor asks the user first; refused for callers on another computer.")]
    public Task<string> MapMakeLandscape(bool replace = false) => OnUi(async () =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        if (ClaudeServer.FromAnotherComputer.Value)
        {
            return "map_make_landscape only works for Claude on this computer.";
        }
        if (!await _w.Ask("Make the Unreal landscape?", replace ? "Claude asks to rebuild the open Unreal level's landscape from this map (its size and heights)." : "Claude asks to make this map's landscape in the open Unreal level.", "Make it", "Not now"))
        {
            return "The user said no: nothing made.";
        }
        return await UnrealLive.MakeLandscape(MapProject.Current!, _w.Session!.Scene, replace);
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_checks", ReadOnly = true, Title = "Find misplaced trees, plants and rocks")]
    [Description("As the Checks panel: trees, plants and rocks floating above the ground (more than floatBy metres), buried in it (more than buriedBy; rocks may be), standing under water (reeds aside), and trees on ground steeper than steepTrees degrees. Counts by problem, then each (object id, what is wrong, x, z), nearest first when near is given. map_settle puts floating and buried ones back on the ground; select picks them in the editor.")]
    public Task<string> MapChecks(float floatBy = 0.3f, float buriedBy = 1, float steepTrees = 40, bool underWater = true, int limit = 50,
        [Description("[x, z]: list the nearest to it first.")] float[]? near = null, [Description("Select them all in the editor.")] bool select = false) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var found = MapTools.ChecksTool.FindProblems(s, new SceneLayers.AreaView(s.Scene), s.Scene.Water, floatBy, buriedBy, steepTrees, underWater);
        if (select)
        {
            _w.View.Select(found.Select(f => f.Index));
        }
        var listed = near is { Length: >= 2 } ? found.OrderBy(f => System.Numerics.Vector2.Distance(new(f.At.X, f.At.Z), new(near[0], near[1]))) : found.AsEnumerable();
        return ToJson(new
        {
            problems = found.Count,
            by = found.GroupBy(f => f.Problem.Contains("floating") ? "floating" : f.Problem.Contains("buried") ? "buried" : f.Problem.Contains("water") ? "under water" : "on steep ground").ToDictionary(g => g.Key, g => g.Count()),
            first = listed.Take(Math.Clamp(limit, 1, 500)).Select(f => new { id = s.Scene.Things[f.Index].Id, problem = f.Problem, x = MathF.Round(f.At.X, 1), z = MathF.Round(f.At.Z, 1) }),
        });
    });
}
