using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

// the map for Claude: the map tools (MapTools.cs) driven as the user drives them, through
// a stand-in for their card's values, so a stroke by Claude is the same undo step a stroke by hand is. Coordinates are
// the editor's (x east, z north, metres; screenshot and area_map use the same); heights are the map's metres (water at its sea level).
public sealed partial class ClaudeTools
{
    /// <summary>A map tool's card values for one call.</summary>
    private sealed class MapCard(IAreaView area) : IToolContext
    {
        public Dictionary<string, float> Numbers { get; } = [];
        public Dictionary<string, bool> Toggles { get; } = [];
        public Dictionary<string, int> Choices { get; } = [];
        public Dictionary<string, List<int>> Palettes { get; } = [];
        public string Said { get; private set; } = "";

        public IAreaView Area => area;

        public float Number(string id) => Numbers.GetValueOrDefault(id);

        public int Choice(string id) => Palettes.TryGetValue(id, out var p) && p.Count > 0 ? p[0] : Choices.GetValueOrDefault(id);

        public bool IsOn(string id) => Toggles.GetValueOrDefault(id);

        public IReadOnlyList<int> Picked(string id) => Palettes.TryGetValue(id, out var p) ? p : [];

        public void SetText(string id, string text) => Said = id == "status" && !string.IsNullOrEmpty(text) ? text : Said;

        public void SetNumber(string id, float value) => Numbers[id] = value;

        public void SetList(string id, IReadOnlyList<(string Title, string Sub, string Tag)> items, int picked) => Choices[id] = picked;

        public void Preview(IReadOnlyList<Shape> shapes)
        {
        }

        public void Status(string text) => Said = text;

        public void Commit(string label, ToolEdit edit)
        {
        }
    }

    /// <summary>Why the map cannot be edited now, or null.</summary>
    private string? NoMap() => _w.Session is { Scene.IsMap: true } ? null : "No map is open: call map_open first.";

    /// <summary>A card with a tool's own defaults (its controls' values), for the call to change.</summary>
    private static MapCard CardOf(IEditorTool tool, WorldScene s)
    {
        var card = new MapCard(new SceneLayers.AreaView(s));
        foreach (var c in tool.Controls)
        {
            switch (c)
            {
                case SliderControl sl:
                    card.Numbers[sl.Id] = sl.Value;
                    break;
                case ToggleControl t:
                    card.Toggles[t.Id] = t.Value;
                    break;
                case PaletteControl p:
                    card.Palettes[p.Id] = [.. p.Picked];
                    break;
                default:
                    break;
            }
        }
        return card;
    }

    /// <summary>A press and a release on the ground at x, z (a click, or a one-dab stroke).</summary>
    private static void Click(IEditorTool tool, MapCard card, float x, float z, bool shift = false)
    {
        var at = new Vector3(x, card.Area.GroundHeight(x, z), z);
        var mods = shift ? KeyModifiers.Shift : KeyModifiers.None;
        tool.OnPointer(card, new PointerInput(PointerAction.Press, mods, at + new Vector3(0, 50, 0), -Vector3.UnitY) { Target = PointerTarget.Ground, Point = at, Normal = Vector3.UnitY });
        tool.OnPointer(card, new PointerInput(PointerAction.Release, mods, at + new Vector3(0, 50, 0), -Vector3.UnitY) { Target = PointerTarget.Ground, Point = at, Normal = Vector3.UnitY });
    }

    /// <summary>The kinds named (model names like Pine_1, or labels like "Pine 1"), as indices into the catalog's kinds.</summary>
    private static (List<int> Found, List<string> Unknown) KindsNamed(List<MapTools.Kind> kinds, IEnumerable<string> names)
    {
        var found = new List<int>();
        var unknown = new List<string>();
        foreach (string n in names)
        {
            int i = kinds.FindIndex(k => string.Equals(k.Model, n, StringComparison.OrdinalIgnoreCase) || string.Equals(k.Label, n, StringComparison.OrdinalIgnoreCase));
            if (i >= 0)
            {
                found.Add(i);
            }
            else
            {
                unknown.Add(n);
            }
        }
        return (found, unknown);
    }

    /// <inheritdoc/>
    [McpServerTool(Name = "map_open", Title = "Open a map")]
    [Description("Opens a map in the editor (the last one opened unless another is named; map_maps lists them: the maps the user opened), as the start page's buttons do. Its tools are then on the rail and the map_* tools work. Live and Send to Unreal need the map's Unreal level (map.json's unreal).")]
    public Task<string> MapOpen([Description("The map's name or folder (map_maps); none: the last one opened.")] string? map = null) => OnUi(async () =>
    {
        var maps = Maps();
        // Only maps the user opened (Settings): a folder from elsewhere is theirs to open first.
        string? folder = map == null ? maps.FirstOrDefault().Folder
            : maps.FirstOrDefault(m => string.Equals(m.Name, map, StringComparison.OrdinalIgnoreCase) || m.Folder == Path.GetFullPath(map)).Folder;
        if (folder == null)
        {
            return $"No map \"{map}\": {string.Join(", ", maps.Select(m => m.Name))}.";
        }
        if (_w.InMap && MapProject.Current?.Folder == Path.GetFullPath(folder))
        {
            return $"{MapProject.Current.Name} is already open.";
        }
        if (_w.InMap && _w.MapDirty)
        {
            return "The open map has unsaved changes: map_save first.";
        }
        await _w.OpenMap(folder);
        return _w.InMap && MapProject.Current is { } m && m.Folder == Path.GetFullPath(folder)
            ? $"{m.Name} is open: {_w.Session?.Scene.Things.Count(t => !t.Gone)} objects, {m.ReadRaster().Cols} x {m.ReadRaster().Rows} m."
            : "The map did not open (see the editor's message).";
    });

    /// <summary>The maps opened before (Settings), their names.</summary>
    private List<(string Name, string Folder)> Maps()
    {
        var maps = new List<(string, string)>();
        foreach (string f in _w.Settings.Maps.Where(MapProject.IsMap))
        {
            try
            {
                maps.Add((MapProject.Open(f).Name, f));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                maps.Add((Path.GetFileName(f), f));
            }
        }
        return maps;
    }

    /// <inheritdoc/>
    [McpServerTool(Name = "map_maps", Title = "The maps")]
    [Description("The maps opened before (folders, the last first), which is open, and which have an Unreal level.")]
    public Task<string> MapList() => OnUi(() => ToJson(Maps().Select(m => new
    {
        name = m.Name,
        folder = m.Folder,
        open = _w.InMap && MapProject.Current?.Folder == Path.GetFullPath(m.Folder),
        unreal = MapProject.IsMap(m.Folder) && TryOpen(m.Folder)?.HasUnrealLevel == true,
    })));

    private static MapProject? TryOpen(string folder)
    {
        try
        {
            return MapProject.Open(folder);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    [McpServerTool(Name = "map_new_map", Title = "Make a new map")]
    [Description("A new map in the maps folder (Settings; not opened: map_open it): squares of 63 m across (8 = 505 m), rolling ground or flat at a height above the sea; the first layer below 1.5 m, no objects, no Unreal level. It shares the open map's models and starts with a copy of its catalog.")]
    public Task<string> MapNew(string name, int squares = 8, bool rolling = true, float height = 3, int seed = 1) => OnUi(() =>
    {
        try
        {
            var m = MapProject.Create(name, squares, rolling, height, seed, _w.NewMapsFolder(), MapProject.Current);
            _w.Settings.Maps.Add(m.Folder);
            _w.SaveSettings(_w.Settings);
            _w.StartPage?.ShowMaps();
            return $"Made {m.Name}: {(Math.Clamp(squares, 1, 64) * 63) + 1} m across, in {m.Folder}.";
        }
        catch (IOException e)
        {
            return e.Message;
        }
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_catalog", Title = "The map's kinds, biomes, markers, stamps, layers and areas")]
    [Description("What the map tools can place: kinds (model, label, group, size range), the groups' base density (per 100 m2) and spacing, biomes, markers (kinds of the group Markers), stamps, rivers and lakes, the ground layers (map_path, map_paint_whole), the area kinds and the areas there are (map_area).")]
    public static Task<string> MapCatalog() => OnUi(() =>
    {
        var (kinds, groups, biomes) = MapTools.Catalog();
        return ToJson(new
        {
            kinds = kinds.Select(k => new { model = k.Model, label = k.Label, group = k.Category, size = new[] { k.MinScale, k.MaxScale } }),
            groups = groups.ToDictionary(g => g.Key, g => new { density = g.Value.Density, spacing = g.Value.Spacing }),
            biomes = biomes.Select(b => new { name = b.Name, ground = b.Paint, density = b.Density, kinds = b.Kinds.Count }),
            stamps = MapTools.Stamps().Select(s => new { name = s.Name, objects = s.Parts.Count }),
            layers = MapProject.Current?.Layers.Select(l => l.Name),
            areaKinds = MapTools.AreaKinds().Select(k => new { kind = k.Id, label = k.Label, unreal = k.Unreal }),
            areas = MapAreas.All.Select(a => new { name = a.Name, kind = a.Kind, x = a.X, z = -a.Y, width = a.Width, depth = a.Depth, bottom = a.Bottom, top = a.Top, turn = a.Yaw }),
            rivers = MapWater.Rivers.Select((r, i) => new { number = i + 1, name = r.Name, points = r.Points.Count, top = r.Top, end = r.End }),
            lakes = MapWater.Lakes.Select((l, i) => new { number = i + 1, name = l.Name, points = l.Outline.Count, level = l.Level }),
            names = MapNames.Labels.Select(l => new { text = l.Text, x = l.X, z = l.Z, size = l.Size.ToString().ToLowerInvariant() }),
            regions = MapNames.Regions.Select(r => new { name = r.Name, middle = MapNames.Middle(r) is var m ? new[] { m.X, m.Y } : null, points = r.Points.Count, hectares = MathF.Abs(MapNames.Area(r)) / 1e4f }),
        });
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_object", Title = "Place one object on the map")]
    [Description("Puts one object of a kind (map_catalog's model or label; markers too) on the ground at x, z. One undo step.")]
    public Task<string> MapObject([Description("The kind: model (Pine_1) or label (Pine 1).")] string kind, float x, float z,
        [Description("Turn, degrees (yaw).")] float turn = 0, [Description("Size (1 = the model's own).")] float size = 1,
        [Description("Lean with the ground.")] bool align = false, [Description("Sink into the ground, metres.")] float sink = 0) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var (kinds, _, _) = MapTools.Catalog();
        var (found, unknown) = KindsNamed(kinds, [kind]);
        if (found.Count == 0)
        {
            return $"No kind \"{kind}\" (map_catalog lists them).";
        }
        var tool = new MapTools.ObjectTool(_w, kinds);
        var card = CardOf(tool, _w.Session!.Scene);
        card.Palettes["kind"] = found;
        card.Numbers["turn"] = turn;
        card.Numbers["size"] = size;
        card.Numbers["sink"] = sink;
        card.Toggles["randomTurn"] = false;
        card.Toggles["randomSize"] = false;
        card.Toggles["align"] = align;
        tool.OnChosen(card);
        Click(tool, card, x, z);
        return card.Said.Length > 0 ? card.Said : "Nothing placed.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_brush", Title = "Scatter kinds on the map")]
    [Description("The Brush in one dab: scatters the kinds in a circle (x, z, radius) up to each group's density times density (map_catalog's groups), spaced by each group's spacing times spacing, each kind at its own size times sizeFrom..sizeTo. erase: takes those kinds away in the circle instead. One undo step.")]
    public Task<string> MapBrush([Description("Kinds: models or labels.")] string[] kinds, float x, float z, float radius = 8, float density = 1, float spacing = 1,
        float sizeFrom = 0.9f, float sizeTo = 1.1f, [Description("0 even .. 1 clumps and clearings.")] float patchy = 0, float patchSize = 20,
        [Description("Thinning towards the rim, 0..1.")] float soft = 0.4f, float keepFromWater = 0, float maxSlope = 35, float tilt = 0, bool align = false,
        bool underWater = false, [Description("Follow the user's Mask panel.")] bool useMask = false, int seed = 1, bool erase = false) => OnUi(() =>
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
        Set(card, radius, density, spacing, sizeFrom, sizeTo, patchy, patchSize, soft, keepFromWater, maxSlope, tilt, align, underWater, useMask, seed);
        Click(tool, card, x, z, erase);
        return (card.Said.Length > 0 ? card.Said : "Nothing placed (full already, or no room).") + (unknown.Count > 0 ? $" Unknown kinds left out: {string.Join(", ", unknown)}." : "");
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_biome", Title = "Paint a biome on the map")]
    [Description("The Biome tool in one dab: a biome (map_catalog's biomes) in a circle: its ground colour and its kinds. One undo step.")]
    public Task<string> MapBiome(string biome, float x, float z, float radius = 16, float density = 1, float spacing = 1, float patchy = 0.3f, float patchSize = 20,
        float soft = 0.4f, float keepFromWater = 0, float maxSlope = 35, bool useMask = false, int seed = 1, bool erase = false) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var (all, groups, biomes) = MapTools.Catalog();
        int i = biomes.FindIndex(b => string.Equals(b.Name, biome, StringComparison.OrdinalIgnoreCase));
        if (i < 0)
        {
            return $"No biome \"{biome}\": {string.Join(", ", biomes.Select(b => b.Name))}.";
        }
        var tool = new MapTools.ScatterTool(_w, all, groups, biomes);
        var card = CardOf(tool, _w.Session!.Scene);
        card.Choices["biome"] = i;
        Set(card, radius, density, spacing, 0.9f, 1.1f, patchy, patchSize, soft, keepFromWater, maxSlope, 0, false, false, useMask, seed);
        Click(tool, card, x, z, erase);
        return card.Said.Length > 0 ? card.Said : "Nothing changed.";
    });

    private static void Set(MapCard card, float radius, float density, float spacing, float sizeFrom, float sizeTo, float patchy, float patchSize, float soft,
        float keepFromWater, float maxSlope, float tilt, bool align, bool underWater, bool useMask, int seed)
    {
        (card.Numbers["radius"], card.Numbers["density"], card.Numbers["spacing"], card.Numbers["minSize"], card.Numbers["maxSize"]) = (radius, density, spacing, sizeFrom, sizeTo);
        (card.Numbers["patchy"], card.Numbers["patchSize"], card.Numbers["soft"], card.Numbers["awayFromWater"]) = (patchy, patchSize, soft, keepFromWater);
        (card.Numbers["maxSlope"], card.Numbers["tilt"], card.Numbers["seed"]) = (maxSlope, tilt, seed);
        (card.Toggles["align"], card.Toggles["water"], card.Toggles["useMask"], card.Toggles["erase"]) = (align, underWater, useMask, false);
    }

    /// <inheritdoc/>
    [McpServerTool(Name = "map_stamp", Title = "Put a stamp down on the map")]
    [Description("Puts a saved stamp (map_catalog's stamps) down at x, z, each object as high above the ground as it was. One undo step.")]
    public Task<string> MapStamp(string stamp, float x, float z, float turn = 0, float size = 1) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var stamps = MapTools.Stamps();
        int i = stamps.FindIndex(s => string.Equals(s.Name, stamp, StringComparison.OrdinalIgnoreCase));
        if (i < 0)
        {
            return $"No stamp \"{stamp}\": {string.Join(", ", stamps.Select(s => s.Name))}.";
        }
        var tool = new MapTools.StampTool(_w, stamps);
        var card = CardOf(tool, _w.Session!.Scene);
        card.Choices["stamp"] = i;
        card.Numbers["turn"] = turn;
        card.Numbers["size"] = size;
        card.Numbers["sizeJitter"] = 0;
        card.Toggles["randomTurn"] = false;
        tool.OnControl(card, "turn");
        Click(tool, card, x, z);
        return card.Said.Length > 0 ? card.Said : "Nothing placed.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_river", Title = "Add or replace a river on the map")]
    [Description("A river through points (editor x, z, with half-width m, depth m, current m/s each), its surface top..end metres above the sea along it. number: replace that river (1 = the creek); none: a new one. Not carved: map_carve cuts its bed. One undo step.")]
    public Task<string> MapRiver([Description("Points: [x, z, half_width, depth, current] each, source first.")] float[][] points, float top = 0.35f, float end = 0.12f,
        string? name = null, int? number = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        if (points.Length < 2 || points.Any(p => p.Length < 2))
        {
            return "A river needs at least two points of [x, z, half_width, depth, current].";
        }
        string before = MapWater.Snapshot();
        var river = new MapWater.River { Name = name ?? $"River {MapWater.Rivers.Count + 1}", Top = top, End = end };
        river.Points.AddRange(points.Select(p => new MapWater.Point(p[0], -p[1], p.Length > 2 ? p[2] : 5, p.Length > 3 ? p[3] : 2, p.Length > 4 ? p[4] : 0.6f)));
        if (number is int n && n >= 1 && n <= MapWater.Rivers.Count)
        {
            MapWater.Rivers[n - 1] = river;
        }
        else
        {
            MapWater.Rivers.Add(river);
        }
        MapWater.Touch();
        string after = MapWater.Snapshot();
        _ = _w.Session!.RecordExtra("River " + river.Name, again => MapWater.Restore(again ? after : before));
        return $"River {river.Name}: {river.Points.Count} points (number {MapWater.Rivers.IndexOf(river) + 1}).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_lake", Title = "Add a lake on the map")]
    [Description("A lake inside an outline (editor x, z points), its surface level metres above the sea, depth in the middle. Not carved: map_carve cuts its bed. One undo step.")]
    public Task<string> MapLake([Description("Outline: [x, z] each.")] float[][] outline, float level = 2, float depth = 3, string? name = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        if (outline.Length < 3)
        {
            return "A lake needs at least three outline points.";
        }
        string before = MapWater.Snapshot();
        var lake = new MapWater.Lake { Name = name ?? $"Lake {MapWater.Lakes.Count + 1}", Level = level, Depth = depth };
        lake.Outline.AddRange(outline.Select(p => new Vector2(p[0], -p[1])));
        MapWater.Lakes.Add(lake);
        MapWater.Touch();
        string after = MapWater.Snapshot();
        _ = _w.Session!.RecordExtra("Lake " + lake.Name, again => MapWater.Restore(again ? after : before));
        return $"Lake {lake.Name}: {lake.Outline.Count} points (number {MapWater.Lakes.Count}).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_carve", Title = "Carve rivers and lakes into the map")]
    [Description("Cuts beds into the ground (never raises it): river number, lake number, or everything (both left out). One undo step each.")]
    public Task<string> MapCarve(int? river = null, int? lake = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var tool = new MapTools.RiverTool(_w);
        var card = CardOf(tool, _w.Session!.Scene);
        if (river is null && lake is null)
        {
            tool.OnControl(card, "carveAll");
        }
        else
        {
            card.Choices["body"] = lake is int l ? MapWater.Rivers.Count + l - 1 : (river ?? 1) - 1;
            tool.OnControl(card, "carve");
        }
        return card.Said.Length > 0 ? card.Said : "Nothing carved.";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_settle", Title = "Settle the map's objects")]
    [Description("As the Settle objects button: the selected objects onto the ground, or (none selected) every object up or down as much as the ground under it changed since the map opened or was last settled. One undo step.")]
    public Task<string> MapSettle() => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        _w.SettleObjects();
        return "Settled (see the editor's message for how many).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_save", Title = "Save the map")]
    [Description("Writes the map's ground, colours, rivers and objects to its files (as Save). Does not touch Unreal: map_send does.")]
    public Task<string> MapSave() => OnUi(async () =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        return await _w.SaveMap(quiet: true) ? "Saved." : "Not saved (see the editor).";
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_send", Title = "Send the map to Unreal")]
    [Description("As Send to Unreal: saves, then puts it all into the Unreal level (Unreal must be running; about a minute). The editor asks the user first; refused for callers on another computer.")]
    public Task<string> MapSend() => OnUi(async () =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        // It runs a script on this computer that changes the Unreal level: never for a caller on another computer, and
        // only when the user says yes in the editor.
        if (ClaudeServer.FromAnotherComputer.Value)
        {
            return "map_send only works for Claude on this computer.";
        }
        if (!await _w.Ask("Send the map to Unreal?", "Claude asks to save the map and put it into the Unreal level (ground, colours, rivers, objects, markers).", "Send", "Not now"))
        {
            return "The user said no: nothing sent.";
        }
        await _w.SendToUnreal();
        return "Sent (the editor's status bar says how it went).";
    });
}
