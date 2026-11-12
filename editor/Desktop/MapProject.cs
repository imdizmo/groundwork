using System.Text;
using System.Text.Json.Nodes;
using Groundwork.App;
using SkiaSharp;

namespace Groundwork.Desktop;

/// <summary>
/// A map project: a folder with map.json and the map's files beside it. map.json says
/// <list type="bullet">
/// <item>name, origin [x, y] (where the heightmap's pixel (0, 0) is, in the engine's metres, 1 m a pixel) and seaLevel
/// (metres; the editor draws water there);</item>
/// <item>files: heights (rows x cols float32 metres, .npy), layers (the ground's paint, RGB .png; a list of them for more
/// than four layers), objects (name, x, y,
/// z, Euler x, y, z, scale, and an object of the data extensions keep on it when there is any; the editor's axes) and
/// water (rivers and lakes, MapWater); relative to the folder;</item>
/// <item>layers: the ground's colours, the first everywhere and up to seven painted over it (name, colour "#rrggbb",
/// channel "r", "g" or "b" of the first layers file, "r2" to "b2" of the second, "r3" to "b3" of the third; in that
/// order when not given);</item>
/// <item>optional: catalog (the kinds Place and the scatter tools offer), models (the model folder), stamps, and
/// unreal { project, apply, layersTexture, waterPrefix } (the Unreal project Live talks to, the script Send to Unreal
/// runs, the texture the landscape material reads the layers from, the water bodies' label prefix), and generator { spec,
/// print } (a generated map's numbers and its files' fingerprint when made: MapProject.Generator).</item>
/// </list>
/// Seen from above the
/// engine's +Y is the editor's -z (Unreal is left-handed, Z up): z = -Y keeps the map the right way round. Heights are
/// the engine's metres as they are: the editor's y is the file's height.
/// </summary>
public sealed partial class MapProject
{
    /// <summary>The map project's own file.</summary>
    public const string FileName = "map.json";

    /// <summary>The map open, or to open next.</summary>
    public static MapProject? Current { get; set; }

    private MapProject(string folder) => Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    /// <summary>The map's folder.</summary>
    public string Folder { get; }
    /// <summary>Its name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Where the heightmap's pixel (0, 0) is, engine metres.</summary>
    public int X0 { get; private set; }
    /// <summary>As <see cref="X0"/>, along y.</summary>
    public int Y0 { get; private set; }
    /// <summary>The sea level, metres.</summary>
    public float SeaLevel { get; set; }
    /// <summary>The heightmap.</summary>
    public string HeightsFile { get; private set; } = "";
    /// <summary>The ground's paint.</summary>
    public string LayersFile { get; private set; } = "";
    /// <summary>The objects as edited.</summary>
    public string ObjectsFile { get; private set; } = "";
    /// <summary>The rivers and lakes.</summary>
    public string WaterFile { get; private set; } = "";
    /// <summary>The areas (boxes that become Unreal volumes).</summary>
    public string AreasFile { get; private set; } = "";
    /// <summary>The holes: a grey picture the heightmap's size, white where the ground is taken away (written once there are any).</summary>
    public string HolesFile { get; private set; } = "";

    /// <summary>The ground kept under the beds (EditSession.Beds): grid index and height pairs; written once there is a bed.</summary>
    public string BedsFile { get; private set; } = "";
    /// <summary>
    /// The ground's layers: the first is the ground everywhere, the others (up to seven) are painted over it, each from a
    /// channel of the layers files (r, g and b of each: an image editor paints them as they are).
    /// </summary>
    public List<Layer> Layers { get; private set; } = DefaultLayers;

    /// <summary>
    /// The most layers a map has (ponytail: the 3D view blends eight; more takes a texture array in its ground shader).
    /// </summary>
    public const int MaxLayers = 8;

    /// <summary>The paint files past the first, as map.json names them.</summary>
    private List<string> _moreLayersFiles = [];

    /// <summary>
    /// The paint files the layers need: the first (<see cref="LayersFile"/>), and one more for each three layers past
    /// the fourth (named after the first, layers2.png and on, unless map.json names them).
    /// </summary>
    public IReadOnlyList<string> LayersFiles =>
        [LayersFile, .. Enumerable.Range(1, Layers.Max(l => l.Channel) / 3).Select(i => _moreLayersFiles.ElementAtOrDefault(i - 1)
            ?? Inside(Path.Combine(Path.GetDirectoryName(LayersFile)!, $"{Path.GetFileNameWithoutExtension(LayersFile)}{i + 1}{Path.GetExtension(LayersFile)}")))];

    /// <summary>
    /// A ground layer: its name, its colour (linear), the layers files' channel it is read from (0 to 8: r, g, b of the
    /// first file, then of the second and the third; the first layer has none), and the picture drawn for it instead of
    /// its colour (null: none) repeated every Size metres.
    /// </summary>
    public sealed record Layer(string Name, System.Numerics.Vector3 Colour, int Channel, string? Texture = null, float Size = 4);

    /// <summary>A new map's layers.</summary>
    internal static List<Layer> NewLayers => DefaultLayers;

    /// <summary>A new map's gently rolling ground at a point (metres over its height), for a seed.</summary>
    internal static float RollingAt(int seed, float x, float y) => new Relief(seed).At(x, y);

    private static List<Layer> DefaultLayers =>
        [new("Grass", Linear(0.42f, 0.60f, 0.27f), -1), new("Sand", Linear(0.80f, 0.74f, 0.52f), 0), new("Dirt", Linear(0.40f, 0.31f, 0.20f), 1), new("Rock", Linear(0.50f, 0.50f, 0.48f), 2)];

    private static System.Numerics.Vector3 Linear(float r, float g, float b) => new(MathF.Pow(r, 2.2f), MathF.Pow(g, 2.2f), MathF.Pow(b, 2.2f));

    /// <summary>The layer a scatter biome's paint names ("sand", "forest"...: its name or its first word), or the first.</summary>
    public int LayerNamed(string name) => Math.Max(0, Layers.FindIndex(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
        || l.Name.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)));

    /// <summary>The kinds, groups, biomes and markers the tools offer (none: only the models).</summary>
    public string? Catalog { get; set; }
    /// <summary>The model folder (glTF files, or ModelStore's Files format).</summary>
    public string? Models { get; set; }
    /// <summary>Where the stamps are kept.</summary>
    public string? Stamps { get; set; }
    /// <summary>The Unreal project Live talks to (its Saved/ has the plugin's address and token).</summary>
    public string? UnrealProject { get; set; }
    /// <summary>The script Send to Unreal runs (once the user trusts it for this map: Settings' TrustedScripts).</summary>
    public string? UnrealApply { get; set; }
    /// <summary>
    /// The texture the landscape material reads the layers from, by name prefix: Live writes each layer into its channel.
    /// Null: the layers go into the landscape's paint layers of the same names.
    /// </summary>
    public string? UnrealLayersTexture { get; set; }
    /// <summary>The label prefix of the level's water bodies that are the map's rivers and lakes (prefix + name).</summary>
    public string UnrealWaterPrefix { get; set; } = "GW_Water_";
    /// <summary>The map has an Unreal level: Live and Send to Unreal work.</summary>
    public bool HasUnrealLevel => UnrealProject != null;

    /// <summary>Whether a folder holds a map.</summary>
    public static bool IsMap(string folder) => File.Exists(Path.Combine(folder, FileName));

    /// <summary>The map in a folder (its map.json).</summary>
    public static MapProject Open(string folder)
    {
        var m = new MapProject(folder);
        string json = Path.Combine(m.Folder, FileName);
        if (!File.Exists(json))
        {
            throw new FileNotFoundException($"{m.Folder}: no {FileName}");
        }
        var j = JsonNode.Parse(File.ReadAllText(json)) as JsonObject ?? throw new InvalidDataException($"{json}: not a JSON object");
        static string? S(JsonNode? n) => n?.GetValue<string>() is { Length: > 0 } v ? v : null;
        m.Name = S(j["name"]) ?? Path.GetFileName(m.Folder);
        (m.X0, m.Y0) = (j["origin"]?[0]?.GetValue<int>() ?? 0, j["origin"]?[1]?.GetValue<int>() ?? 0);
        m.SeaLevel = j["seaLevel"]?.GetValue<float>() ?? 0;
        var f = j["files"];
        // What the editor writes stays in the folder; the model folder is only read, anywhere.
        m.HeightsFile = m.Inside(S(f?["heights"]) ?? "height.npy");
        var files = f?["layers"] as JsonArray;
        m.LayersFile = m.Inside((files != null ? S(files.FirstOrDefault()) : S(f?["layers"])) ?? "layers.png");
        m._moreLayersFiles = [.. (files ?? []).Skip(1).Select(x => m.Inside(S(x) ?? throw new InvalidDataException($"{json}: a layers file with no name")))];
        m.ObjectsFile = m.Inside(S(f?["objects"]) ?? "objects.json");
        m.WaterFile = m.Inside(S(f?["water"]) ?? "water.json");
        m.AreasFile = m.Inside(S(f?["areas"]) ?? "areas.json");
        m.HolesFile = m.Inside(S(f?["holes"]) ?? "holes.png");
        m.BedsFile = m.Inside(S(f?["beds"]) ?? "beds.bin");
        m.Catalog = S(j["catalog"]) is string c ? m.Inside(c) : null;
        m.Models = S(j["models"]) is string mo ? m.Abs(mo) : null;
        m.Stamps = S(j["stamps"]) is string st ? m.Inside(st) : null;
        if (j["layers"] is JsonArray layers && layers.Count > 0)
        {
            m.Layers = [.. layers.Take(MaxLayers).Select((l, i) => new Layer(
                S(l?["name"]) ?? $"Layer {i + 1}",
                S(l?["colour"]) is string c && c.Length == 7 && c[0] == '#' && int.TryParse(c[1..], System.Globalization.NumberStyles.HexNumber, null, out int rgb)
                    ? Linear(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f)
                    : Linear(0.5f, 0.5f, 0.5f),
                i == 0 ? -1 : S(l?["channel"]) is string ch && ChannelNamed(ch) is int k and >= 0 ? k : i - 1,
                S(l?["texture"]) is string t ? m.Inside(t) : null,
                l?["size"]?.GetValue<float>() is float size && size > 0 ? size : 4))];
        }
        m.UnrealProject = S(j["unreal"]?["project"]) is string up ? m.Abs(up) : null;
        m.UnrealApply = S(j["unreal"]?["apply"]) is string ua ? m.Abs(ua) : null;
        m.UnrealLayersTexture = S(j["unreal"]?["layersTexture"]);
        m.UnrealWaterPrefix = S(j["unreal"]?["waterPrefix"]) ?? m.UnrealWaterPrefix;
        m.Generated = ReadGenerated(j["generator"]);
        m.BaseFile = S(j["generator"]?["base"]) is string gb ? m.Inside(gb) : null;
        return m;
    }

    /// <summary>Writes map.json.</summary>
    public void WriteJson()
    {
        var j = new JsonObject
        {
            ["name"] = Name,
            ["origin"] = new JsonArray(X0, Y0),
            ["seaLevel"] = SeaLevel,
            ["files"] = new JsonObject { ["heights"] = Rel(HeightsFile), ["layers"] = LayersFiles.Count == 1 ? Rel(LayersFile) : new JsonArray([.. LayersFiles.Select(l => (JsonNode)Rel(l))]), ["objects"] = Rel(ObjectsFile), ["water"] = Rel(WaterFile), ["areas"] = Rel(AreasFile), ["holes"] = Rel(HolesFile), ["beds"] = Rel(BedsFile) },
        };
        j["layers"] = new JsonArray([.. Layers.Select(l => (JsonNode)new JsonObject
        {
            ["name"] = l.Name,
            ["colour"] = $"#{Srgb(l.Colour.X):x2}{Srgb(l.Colour.Y):x2}{Srgb(l.Colour.Z):x2}",
            ["channel"] = l.Channel < 0 ? null : ChannelName(l.Channel),
            ["texture"] = l.Texture == null ? null : Rel(l.Texture),
            ["size"] = l.Texture == null ? null : l.Size,
        })]);
        if (Catalog != null)
        {
            j["catalog"] = Rel(Catalog);
        }
        if (Models != null)
        {
            j["models"] = Rel(Models);
        }
        if (Stamps != null)
        {
            j["stamps"] = Rel(Stamps);
        }
        if (UnrealProject != null)
        {
            j["unreal"] = new JsonObject
            {
                ["project"] = Rel(UnrealProject),
                ["apply"] = UnrealApply == null ? null : Rel(UnrealApply),
            };
            if (UnrealLayersTexture != null)
            {
                j["unreal"]!["layersTexture"] = UnrealLayersTexture;
            }
            j["unreal"]!["waterPrefix"] = UnrealWaterPrefix;
        }
        if (Generated is { } g)
        {
            j["generator"] = WriteGenerated(g);
        }
        NoLinks(Path.Combine(Folder, FileName));
        File.WriteAllText(Path.Combine(Folder, FileName), j.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>A layers channel from its name in map.json ("r", "g", "b", "r2" ... "b3"), or -1.</summary>
    private static int ChannelNamed(string ch) =>
        ch.Length is 1 or 2 && "rgb".IndexOf(ch[0]) is int c and >= 0 && (ch.Length == 1 ? 1 : ch[1] - '0') is int file and >= 1 and <= 3 ? ((file - 1) * 3) + c : -1;

    /// <summary>A layers channel's name in map.json.</summary>
    internal static string ChannelName(int channel) => "rgb"[channel % 3] + (channel >= 3 ? $"{(channel / 3) + 1}" : "");

    /// <summary>The paint files' pixels, RGBA a file (a file not made yet: nothing painted).</summary>
    private byte[][] ReadLayers(Raster map) =>
        [.. LayersFiles.Select((f, i) => i > 0 && !File.Exists(f) ? Blank(map.Cols * map.Rows) : ReadMask(f, map))];

    /// <summary>A paint file's pixels with nothing painted, opaque.</summary>
    private static byte[] Blank(int pixels)
    {
        byte[] b = new byte[pixels * 4];
        for (int i = 3; i < b.Length; i += 4)
        {
            b[i] = 255;
        }
        return b;
    }

    /// <summary>A pixel's byte of a layers channel in the paint files' pixels.</summary>
    private static ref byte At(byte[][] masks, int px, int channel) => ref masks[channel / 3][(px * 4) + (channel % 3)];

    private static int Srgb(float linear) => (int)MathF.Round(MathF.Pow(Math.Clamp(linear, 0, 1), 1 / 2.2f) * 255);

    private string Abs(string path) => Path.GetFullPath(Path.Combine(Folder, path));

    /// <summary>
    /// A file the editor writes: in the map's folder, and no link (a map from elsewhere must not name other files, nor
    /// carry a link that writes through to one).
    /// </summary>
    private string Inside(string path)
    {
        string full = Abs(path);
        if (!full.StartsWith(Folder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{Path.Combine(Folder, FileName)}: {path} is not in the map's folder");
        }
        NoLinks(full);
        return full;
    }

    /// <summary>Refuses a path that is a link, or lies under one, between the map's folder and it.</summary>
    private void NoLinks(string full)
    {
        for (string? p = full; p != null && p.Length > Folder.Length; p = Path.GetDirectoryName(p))
        {
            if (new FileInfo(p).LinkTarget != null)
            {
                throw new InvalidDataException($"{p} is a link: a map's files are its own");
            }
        }
    }

    /// <summary>The old file kept as .bak (neither of them a link) before it is written again.</summary>
    private void KeepOld(string f)
    {
        NoLinks(f);
        NoLinks(f + ".bak");
        if (File.Exists(f))
        {
            File.Copy(f, f + ".bak", true);
        }
    }

    private string Rel(string path) => Path.GetRelativePath(Folder, path) is var r && !r.StartsWith("..", StringComparison.Ordinal) ? r : path;

    /// <summary>
    /// A new map like <see cref="Create"/>, its ground a whole zone (<see cref="ZoneGenerator"/>): hills, a rim, lakes,
    /// woods and the layers painted by rule, planted with the catalog's kinds.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <param name="chunks">How many 63 m chunks across.</param>
    /// <param name="spec">The zone's numbers.</param>
    /// <param name="planting">The kinds it is planted with.</param>
    /// <param name="under">The folder it goes in.</param>
    /// <param name="like">The map whose models it shares and whose catalog it copies.</param>
    /// <param name="ground">
    /// The ground to make it over (a heightmap made elsewhere, metres, rows of the map's width; null: the generator's own
    /// hills), kept beside it as base.npy so that it can be made again over it.
    /// </param>
    /// <param name="chunksZ">How many 63 m chunks north to south (null: as many as across).</param>
    public static MapProject CreateZone(string name, int chunks, ZoneGenerator.Spec spec, ZoneGenerator.Planting planting, string under, MapProject? like = null, float[]? ground = null, int? chunksZ = null)
    {
        var m = Create(name, chunks, generated: false, spec.Low, spec.Seed, under, like, chunksZ);
        if (ground != null)
        {
            var r = m.ReadRaster();
            if (ground.Length != r.Cols * r.Rows)
            {
                throw new InvalidDataException($"The ground given is {ground.Length} points, not the map's {r.Cols} x {r.Rows}.");
            }
            m.BaseFile = m.Inside("base.npy");
            Write(m.BaseFile, ground, r.Rows, r.Cols);
        }
        m.Generate(spec, planting);
        return m;
    }

    /// <summary>
    /// A new map in a folder of its own under <paramref name="under"/>: chunks x chunks of 63 m (Unreal's landscape
    /// components) plus one (or chunks across by chunksZ north to south), centred on the origin, flat at a height or
    /// rolling ground, the first layer below 1.5 m; no objects. It shares <paramref name="like"/>'s models and starts with
    /// a copy of its catalog (no Unreal level).
    /// </summary>
    /// <param name="name">Its name (its folder's, letters and digits kept).</param>
    /// <param name="chunks">How many 63 m chunks across.</param>
    /// <param name="generated">Rolling ground; else flat.</param>
    /// <param name="height">Its height above the sea, metres.</param>
    /// <param name="seed">The rolling ground's seed.</param>
    /// <param name="under">The folder it goes in.</param>
    /// <param name="like">The map whose models it shares and whose catalog it copies.</param>
    /// <param name="chunksZ">How many 63 m chunks north to south (null: as many as across).</param>
    /// <exception cref="InvalidDataException">More 63 m squares than a map holds (HeightmapImport.MaxSquares).</exception>
    public static MapProject Create(string name, int chunks, bool generated, float height, int seed, string under, MapProject? like = null, int? chunksZ = null)
    {
        int squaresX = Math.Clamp(chunks, 1, HeightmapImport.MaxSquaresASide), squaresZ = Math.Clamp(chunksZ ?? chunks, 1, HeightmapImport.MaxSquaresASide);
        if (squaresX * squaresZ > HeightmapImport.MaxSquares)
        {
            throw new InvalidDataException($"{squaresX} x {squaresZ} squares of 63 m is more than a map holds ({HeightmapImport.MaxSquares} in all).");
        }
        string safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')).Trim('_');
        string folder = Path.Combine(under, safe.Length > 0 ? safe : "Map");
        if (Directory.Exists(folder))
        {
            throw new IOException($"There is already a map called {Path.GetFileName(folder)}.");
        }
        _ = Directory.CreateDirectory(folder);
        int n = (squaresX * 63) + 1, rows = (squaresZ * 63) + 1, x0 = -(n / 2), y0 = -(rows / 2);
        // like's models where they are: its own setting, else the models folder in its folder
        var m = new MapProject(folder) { Name = name, X0 = x0, Y0 = y0, Models = like == null ? null : like.Models ?? Path.Combine(like.Folder, "models") };
        if (like?.Catalog is { } catalog && File.Exists(catalog))   // a copy: what a map writes stays in its folder
        {
            m.Catalog = m.Abs("catalog.json");
            File.Copy(catalog, m.Catalog);
        }
        (m.HeightsFile, m.LayersFile, m.ObjectsFile, m.WaterFile, m.AreasFile, m.HolesFile, m.BedsFile) = (m.Abs("height.npy"), m.Abs("layers.png"), m.Abs("objects.json"), m.Abs("water.json"), m.Abs("areas.json"), m.Abs("holes.png"), m.Abs("beds.bin"));
        var raster = new Raster(n, rows, x0, y0);
        float[] heights = new float[n * rows];
        byte[] mask = new byte[n * rows * 4];
        var noise = new Relief(seed);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < n; col++)
            {
                float h = generated ? height + noise.At(col + x0, row + y0) : height;
                heights[(row * n) + col] = h;
                mask[((row * n) + col) * 4] = (byte)(h < 1.5f ? 255 : 0);
            }
        }
        Write(m.HeightsFile, heights, rows, n);
        WriteMask(m.LayersFile, mask, raster);
        File.WriteAllText(m.ObjectsFile, "[]");
        File.WriteAllText(m.WaterFile, "{\"rivers\": [], \"lakes\": []}");
        m.WriteJson();
        return m;
    }

    /// <summary>The heightmap's size in pixels (metres) and where its pixel (0, 0) is in engine metres.</summary>
    public readonly record struct Raster(int Cols, int Rows, int X0, int Y0)
    {
        /// <summary>The map pixel (col, row) under an editor point.</summary>
        public (int Col, int Row) Pixel(float x, float z) => ((int)MathF.Round(x) - X0, (int)MathF.Round(-z) - Y0);

        /// <summary>Whether a pixel is on the map.</summary>
        public bool Holds(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;
    }

    /// <summary>The heightmap's size and place, as the files say.</summary>
    public Raster ReadRaster()
    {
        _ = Read(HeightsFile, out int rows, out int cols, headerOnly: true);
        return new Raster(cols, rows, X0, Y0);
    }

    /// <summary>The map as a scene: its ground (outside the heightmap: its nearest edge), objects and water.</summary>
    public WorldScene Read()
    {
        // Read as the current map: its catalog and model folder sort its objects, not the last map's
        Current = this;
        MapTools.ForgetGroups();
        var map = ReadRaster();
        float[] heights = Read(HeightsFile, out _, out _);
        byte[][] masks = ReadLayers(map);
        byte[]? holes = File.Exists(HolesFile) ? ReadMask(HolesFile, map) : null;
        // Zones of 64 m (centred on multiples of 64) round the map, editor x = X, z = -Y.
        int zx0 = (int)MathF.Floor((map.X0 + 32f) / 64f), zx1 = (int)MathF.Floor((map.X0 + map.Cols - 1 + 32f) / 64f);
        int zz0 = (int)MathF.Floor((-(map.Y0 + map.Rows - 1) + 32f) / 64f), zz1 = (int)MathF.Floor((-map.Y0 + 32f) / 64f);
        int zonesX = zx1 - zx0 + 1, zonesZ = zz1 - zz0 + 1, w = (zonesX * 64) + 1, h = (zonesZ * 64) + 1;
        var ground = new Ground(w, h, zx0, zz0, zonesX, zonesZ, Ground.ChannelsFor(Layers.Count));
        for (int gz = 0; gz < h; gz++)
        {
            for (int gx = 0; gx < w; gx++)
            {
                var (col, row) = map.Pixel(ground.OriginX + gx, ground.OriginZ + gz);
                int px = (Math.Clamp(row, 0, map.Rows - 1) * map.Cols) + Math.Clamp(col, 0, map.Cols - 1), g = (gz * w) + gx;
                ground.Base[g] = heights[px];
                // Each painted layer's channel of the ground, read from that layer's channel of the files.
                for (int k = 1; k < Layers.Count; k++)
                {
                    ground.BaseMask[(g * ground.C) + Ground.ChannelOf(k)] = At(masks, px, Layers[k].Channel) / 255f;
                }
                // the hole channel (Unreal's landscape visibility), 0 where the ground is
                ground.BaseMask[(g * ground.C) + Ground.HoleChannel] = holes != null ? holes[px * 4] / 255f : 0;
            }
        }
        var scene = new WorldScene
        {
            Name = Name,
            X0 = ground.X0,
            Z0 = ground.Z0,
            ZonesX = zonesX,
            ZonesZ = zonesZ,
            W = w,
            H = h,
            Heights = (float[])ground.Base.Clone(),
            Things = [],
            Mask = new byte[w * h * ground.C],
            C = ground.C,
            LayerColours = [.. Layers.Select(l => l.Colour)],
            LayerTextures = [.. Layers.Select(l => (l.Texture, l.Size))],
            // the grid's middle, where the 3D view puts it (not 0: a map padded more on one side, a grown one or one of 64
            // squares, had its ground drawn tens of metres off its objects)
            Cx = ground.OriginX + ((w - 1) / 2f),
            Cz = ground.OriginZ + ((h - 1) / 2f),
            Water = SeaLevel,
            LoadInfo = $"{HeightsFile} · {map.Cols} x {map.Rows} m",
            IsMap = true,
        };
        scene.Refresh(ground, 0, 0, w - 1, h - 1);
        scene.Session = new EditSession(scene, ground);
        // The catalog's categories are the objects' kinds (View, picking).
        ObjectKinds.UseCategories(MapTools.Catalog().Kinds.Select(k => (k.Model, k.Category)));
        // The objects as editor objects (Select moves, turns and deletes them; Place and the tools add more), loaded as
        // one step that is then forgotten: undoing it would empty the map.
        var placed = ModelStore.ReadPlacements(ObjectsFile);
        _ = scene.Session.Commit("Map objects", null, [], [.. placed.Select(p => (new NewObject(0, AssetIds.Of(p.Name), p.Position, p.Rotation, MathF.Abs(p.Scale - 1) < 1e-3f ? 0 : p.Scale, p.Data),
            ObjectKinds.Of(p.Name) == ObjectKind.Buildings))]);
        scene.Session.ForgetHistory();
        if (File.Exists(BedsFile))
        {
            scene.Session.SetLifted(ReadBeds(BedsFile));
        }
        MapWater.Load(WaterFile);
        MapAreas.Load(AreasFile);
        MapNotes.Load(Path.Combine(Folder, "notes"));
        return scene;
    }

    /// <summary>The map's heights as they are now (its file's, with the open ground's over them): rows of cols.</summary>
    public (float[] Heights, int Cols, int Rows) HeightsNow(WorldScene s)
    {
        var g = s.Session!.Ground;
        var map = ReadRaster();
        float[] heights = Read(HeightsFile, out _, out _);
        for (int gz = 0; gz < g.H; gz++)
        {
            for (int gx = 0; gx < g.W; gx++)
            {
                var (col, row) = map.Pixel(g.OriginX + gx, g.OriginZ + gz);
                if (map.Holds(col, row))
                {
                    heights[(row * map.Cols) + col] = g.HeightOf((gz * g.W) + gx);
                }
            }
        }
        return (heights, map.Cols, map.Rows);
    }

    /// <summary>
    /// Each layer's weight (0..255) per map pixel as it is now (the file's, the open ground's over it), rows of cols:
    /// the painted layers from their channels, the first layer what they leave.
    /// </summary>
    public (byte[][] Weights, int Cols, int Rows) LayersNow(WorldScene s)
    {
        var g = s.Session!.Ground;
        var map = ReadRaster();
        byte[][] masks = ReadLayers(map);
        for (int gz = 0; gz < g.H; gz++)
        {
            for (int gx = 0; gx < g.W; gx++)
            {
                var (col, row) = map.Pixel(g.OriginX + gx, g.OriginZ + gz);
                if (map.Holds(col, row))
                {
                    for (int k = 1; k < Layers.Count; k++)
                    {
                        At(masks, (row * map.Cols) + col, Layers[k].Channel) = Byte(g.MaskOf((gz * g.W) + gx, Ground.ChannelOf(k)));
                    }
                }
            }
        }
        byte[][] weights = [.. Layers.Select(_ => new byte[map.Cols * map.Rows])];
        for (int px = 0; px < map.Cols * map.Rows; px++)
        {
            int rest = 255;
            for (int k = 1; k < Layers.Count; k++)
            {
                rest -= weights[k][px] = (byte)Math.Min(At(masks, px, Layers[k].Channel), Math.Max(0, rest));
            }
            weights[0][px] = (byte)Math.Max(0, rest);
        }
        return (weights, map.Cols, map.Rows);
    }

    /// <summary>The ground kept under the beds: a count, then grid index (int32) and height (float32) pairs, little-endian.</summary>
    private static void WriteBeds(string file, IReadOnlyDictionary<int, float> lifted)
    {
        using var w = new BinaryWriter(File.Create(file));
        w.Write(lifted.Count);
        foreach (var (p, h) in lifted.OrderBy(kv => kv.Key))
        {
            w.Write(p);
            w.Write(h);
        }
    }

    /// <summary>The ground kept under the beds, read back (a short or odd file: what it holds).</summary>
    private static List<(int Point, float Height)> ReadBeds(string file)
    {
        var lifted = new List<(int, float)>();
        using var r = new BinaryReader(File.OpenRead(file));
        int n = Math.Clamp(r.ReadInt32(), 0, 1 << 26);
        for (int i = 0; i < n && r.BaseStream.Position + 8 <= r.BaseStream.Length; i++)
        {
            lifted.Add((r.ReadInt32(), r.ReadSingle()));
        }
        return lifted;
    }

    /// <summary>The ground, its colours, the objects and the water as they are now back into the map's files.</summary>
    /// <returns>How many objects were written.</returns>
    public int Save(WorldScene s)
    {
        var g = s.Session!.Ground;
        var map = ReadRaster();
        float[] heights = Read(HeightsFile, out _, out _);
        byte[][] masks = ReadLayers(map);
        byte[] holes = File.Exists(HolesFile) ? ReadMask(HolesFile, map) : new byte[map.Cols * map.Rows * 4];
        bool anyHole = File.Exists(HolesFile);
        for (int gz = 0; gz < g.H; gz++)
        {
            for (int gx = 0; gx < g.W; gx++)
            {
                var (col, row) = map.Pixel(g.OriginX + gx, g.OriginZ + gz);
                if (map.Holds(col, row))
                {
                    int p = (gz * g.W) + gx, px = (row * map.Cols) + col;
                    heights[px] = g.HeightOf(p);
                    for (int k = 1; k < Layers.Count; k++)
                    {
                        At(masks, px, Layers[k].Channel) = Byte(g.MaskOf(p, Ground.ChannelOf(k)));
                    }
                    byte hole = Byte(g.MaskOf(p, Ground.HoleChannel));
                    (holes[px * 4], holes[(px * 4) + 1], holes[(px * 4) + 2], holes[(px * 4) + 3]) = (hole, hole, hole, 255);
                    anyHole |= hole > 127;
                }
            }
        }
        var objects = new JsonArray();
        lock (s.Things)
        {
            foreach (var t in s.Things.Where(t => !t.Gone))
            {
                if (AssetIds.NameOf(t.Prefab) is string name)
                {
                    var o = new JsonArray(name, R(t.Position.X), R(t.Position.Y), R(t.Position.Z), R(t.Rotation.X), R(t.Rotation.Y), R(t.Rotation.Z), R(t.Scale == 0 ? 1 : t.Scale));
                    if (s.Session.Edits.FindAdded(t.Id)?.Data is { Count: > 0 } data)
                    {
                        o.Add(new JsonObject([.. data.Select(d => KeyValuePair.Create(d.Key, (JsonNode?)d.Value))]));
                    }
                    objects.Add(o);
                }
            }
        }
        var layersFiles = LayersFiles;
        foreach (string f in new[] { HeightsFile, ObjectsFile, WaterFile, AreasFile }.Concat(layersFiles))
        {
            KeepOld(f);
        }
        Write(HeightsFile, heights, map.Rows, map.Cols);
        var lifted = s.Session.Lifted;
        if (lifted.Count > 0)
        {
            KeepOld(BedsFile);
            WriteBeds(BedsFile, lifted);
        }
        else if (File.Exists(BedsFile))
        {
            KeepOld(BedsFile);
            File.Delete(BedsFile);
        }
        for (int i = 0; i < layersFiles.Count; i++)
        {
            WriteMask(layersFiles[i], masks[i], map);
        }
        if (anyHole)
        {
            KeepOld(HolesFile);
            WriteMask(HolesFile, holes, map);
        }
        File.WriteAllText(ObjectsFile, objects.ToJsonString());
        MapWater.Save(WaterFile);
        MapAreas.Save(AreasFile);
        WriteJson();
        return objects.Count;
    }

    /// <summary>Which sides the map grows on.</summary>
    [Flags]
    public enum Sides
    {
        /// <summary>Unreal -Y (the editor's +z).</summary>
        North = 1,
        /// <summary>Unreal +Y.</summary>
        South = 2,
        /// <summary>Unreal +X.</summary>
        East = 4,
        /// <summary>Unreal -X (the open sea).</summary>
        West = 8,
    }

    /// <summary>
    /// The map's files made bigger by metres on the sides given (save first: the scene is not read): the new ground flat
    /// at a height, or generated, continuing its edge (the edge's height, with rolling ground growing in over the
    /// first 64 m, the map generator's relief). The new ground is grass, sand below 1.5 m; nothing stands on it.
    /// </summary>
    public void Grow(Sides sides, int metres, bool generated, float flatHeight, int seed)
    {
        var map = ReadRaster();
        float[] old = Read(HeightsFile, out _, out _);
        byte[][] oldMasks = ReadLayers(map);
        int n = (sides & Sides.North) != 0 ? metres : 0, s = (sides & Sides.South) != 0 ? metres : 0;
        int e = (sides & Sides.East) != 0 ? metres : 0, wst = (sides & Sides.West) != 0 ? metres : 0;
        var grown = new Raster(map.Cols + e + wst, map.Rows + n + s, map.X0 - wst, map.Y0 - n);
        float[] heights = new float[grown.Cols * grown.Rows];
        byte[][] masks = [.. oldMasks.Select(_ => Blank(heights.Length))];
        var noise = new Relief(seed);
        for (int row = 0; row < grown.Rows; row++)
        {
            for (int col = 0; col < grown.Cols; col++)
            {
                int oc = col - wst, or = row - n, px = (row * grown.Cols) + col;
                if (map.Holds(oc, or))
                {
                    heights[px] = old[(or * map.Cols) + oc];
                    for (int i = 0; i < masks.Length; i++)
                    {
                        Array.Copy(oldMasks[i], ((or * map.Cols) + oc) * 4, masks[i], px * 4, 4);
                    }
                    continue;
                }
                int ec = Math.Clamp(oc, 0, map.Cols - 1), er = Math.Clamp(or, 0, map.Rows - 1);
                float edge = old[(er * map.Cols) + ec], d = MathF.Sqrt(((oc - ec) * (oc - ec)) + ((or - er) * (or - er)));
                float x = col + grown.X0, y = row + grown.Y0;
                float h = generated
                    ? edge + ((noise.At(x, y) - noise.At(ec + map.X0, er + map.Y0)) * Smooth(0, 64, d))
                    : edge + ((flatHeight - edge) * Smooth(0, 32, d));
                heights[px] = h;
                masks[0][px * 4] = (byte)(h < 1.5f ? 255 : 0);
            }
        }
        var layersFiles = LayersFiles;
        foreach (string f in new[] { HeightsFile, Path.Combine(Folder, FileName) }.Concat(layersFiles))
        {
            KeepOld(f);
        }
        Write(HeightsFile, heights, grown.Rows, grown.Cols);
        for (int i = 0; i < layersFiles.Count; i++)
        {
            WriteMask(layersFiles[i], masks[i], grown);
        }
        if (File.Exists(HolesFile))
        {
            byte[] oldHoles = ReadMask(HolesFile, map), holes = new byte[heights.Length * 4];
            for (int px = 0; px < heights.Length; px++)
            {
                int oc = (px % grown.Cols) - wst, or = (px / grown.Cols) - n;
                holes[(px * 4) + 3] = 255;
                if (map.Holds(oc, or))
                {
                    Array.Copy(oldHoles, ((or * map.Cols) + oc) * 4, holes, px * 4, 4);
                }
            }
            KeepOld(HolesFile);
            WriteMask(HolesFile, holes, grown);
        }
        (X0, Y0) = (grown.X0, grown.Y0);
        WriteJson();
    }

    private static float Smooth(float e0, float e1, float v)
    {
        float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    /// <summary>Rolling ground: smooth value noise in bands from 16 m to 256 m waves, metres.</summary>
    private sealed class Relief(int seed)
    {
        private static readonly (float Wave, float Amp)[] Bands = [(16, 0.08f), (32, 0.15f), (64, 0.31f), (128, 0.57f), (256, 1.0f)];

        public float At(float x, float y)
        {
            float v = 0;
            for (int i = 0; i < Bands.Length; i++)
            {
                v += Bands[i].Amp * Noise((x / Bands[i].Wave) + (i * 17.3f), (y / Bands[i].Wave) - (i * 9.1f));
            }
            return v * 2f;
        }

        private float Noise(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float fx = x - xi, fy = y - yi, ux = fx * fx * (3 - (2 * fx)), uy = fy * fy * (3 - (2 * fy));
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + ((b - a) * ux) + ((c - a) * uy) + ((a - b - c + d) * ux * uy);
        }

        private float Hash(int x, int y)
        {
            uint h = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
            h = (h ^ (h >> 13)) * 1274126177;
            return (((h ^ (h >> 16)) & 0xFFFF) / 32767.5f) - 1f;
        }
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    private static float R(float v) => MathF.Round(v * 1000f) / 1000f;

    /// <summary>The mask's RGBA, not premultiplied (A is 0 off the shore: premultiplying would lose the rest).</summary>
    private static byte[] ReadMask(string path, Raster map)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"{path}: not an image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (info.Width != map.Cols || info.Height != map.Rows)
        {
            throw new InvalidDataException($"{path}: {info.Width} x {info.Height}, expected {map.Cols} x {map.Rows} like the heightmap");
        }
        byte[] rgba = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* p = rgba)
            {
                if (codec.GetPixels(info, (IntPtr)p) != SKCodecResult.Success)
                {
                    throw new InvalidDataException($"{path}: could not be read");
                }
            }
        }
        return rgba;
    }

    private static void WriteMask(string path, byte[] rgba, Raster map)
    {
        var info = new SKImageInfo(map.Cols, map.Rows, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        unsafe
        {
            fixed (byte* p = rgba)
            {
                using var pixmap = new SKPixmap(info, (IntPtr)p);
                using var data = pixmap.Encode(SKEncodedImageFormat.Png, 100) ?? throw new IOException($"{path}: could not be encoded");
                using var f = File.Create(path);
                data.SaveTo(f);
            }
        }
    }

    /// <summary>A 2-D little-endian float32 .npy (C order); headerOnly reads only its shape.</summary>
    private static float[] Read(string path, out int rows, out int cols, bool headerOnly = false)
    {
        byte[] b = headerOnly ? ReadHead(path) : File.ReadAllBytes(path);
        int hl = b[6] == 1 ? BitConverter.ToUInt16(b, 8) : BitConverter.ToInt32(b, 8), at = b[6] == 1 ? 10 + hl : 12 + hl;
        string head = Encoding.ASCII.GetString(b, b[6] == 1 ? 10 : 12, hl);
        if (!head.Contains("'<f4'") || head.Contains("'fortran_order': True"))
        {
            throw new InvalidDataException($"{path}: not a C-order float32 array ({head.Trim()})");
        }

        string[] dims = head[(head.IndexOf('(') + 1)..head.IndexOf(')')].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        rows = int.Parse(dims[0]);
        cols = int.Parse(dims[1]);
        if (headerOnly)
        {
            return [];
        }
        float[] a = new float[rows * cols];
        Buffer.BlockCopy(b, at, a, 0, a.Length * 4);
        return a;
    }

    private static byte[] ReadHead(string path)
    {
        using var f = File.OpenRead(path);
        byte[] b = new byte[Math.Min(4096, f.Length)];
        f.ReadExactly(b);
        return b;
    }

    private static void Write(string path, float[] a, int rows, int cols)
    {
        string head = $"{{'descr': '<f4', 'fortran_order': False, 'shape': ({rows}, {cols}), }}";
        head = head.PadRight(((head.Length + 11) / 64 * 64) + 63 - 10) + "\n";   // 10-byte prefix + header: a multiple of 64
        using var f = File.Create(path);
        f.Write([0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 1, 0]);
        f.Write(BitConverter.GetBytes((ushort)head.Length));
        f.Write(Encoding.ASCII.GetBytes(head));
        byte[] bytes = new byte[a.Length * 4];
        Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
        f.Write(bytes);
    }
}
