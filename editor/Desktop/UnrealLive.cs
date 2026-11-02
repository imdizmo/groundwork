using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// The map live in Unreal: with Live on, after every change (a stroke, a placement, an undo) what differs from the last
/// sync goes to the open Unreal level through the project's Groundwork Bridge plugin (its own repository,
/// groundwork-bridge; 127.0.0.1, a token from Saved/GroundworkBridge.json): the ground's heights and layers in the changed rectangle, the
/// objects and markers added and taken away, the rivers and lakes. And the other way (Pull, polled while Live is on):
/// what was changed in Unreal by hand, ground, objects, markers, rivers, comes into the editor as a step of its own
/// ("From Unreal").
/// </summary>
public sealed partial class UnrealLive
{
    /// <summary>The bridge protocol this editor speaks (the Groundwork Bridge's /groundwork/ping "version").</summary>
    private const int Protocol = 1;

    /// <summary>The open map's Unreal project (map.json's unreal.project).</summary>
    public static string Project => MapProject.Current?.UnrealProject ?? "";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly WorldScene _scene;
    private readonly string _url, _token;
    private readonly Dictionary<string, string> _meshes;
    private readonly MapProject.Raster _map;
    private readonly MapProject _project;
    private float[] _heights;
    private byte[] _mask;
    private bool[] _gone;
    private int _count;
    private string _water;
    private bool _busy, _again, _pulling;
    private readonly Dictionary<string, (string Class, string? Tag)> _markers;
    private (long Heights, long Objects, long Water) _versions;

    private UnrealLive(WorldScene scene, string url, string token, Dictionary<string, string> meshes, Dictionary<string, (string, string?)> markers)
    {
        _scene = scene;
        _url = url;
        _token = token;
        _meshes = meshes;
        _markers = markers;
        _project = MapProject.Current!;
        _map = _project.ReadRaster();
        (_heights, _mask, _gone, _count) = Snapshot();
        _water = MapWater.Snapshot();
        // the areas go at the first push when there are any (Unreal's are the editor's: rebuilt from them)
        _areas = MapAreas.All.Count > 0 ? "" : MapAreas.Snapshot();
        _areaKinds = MapTools.AreaKinds();
        _holes = Holes();
    }

    /// <summary>The holes as last sent: a byte a ground point (255: a hole).</summary>
    private byte[] _holes;

    /// <summary>The ground's holes now, a byte a point.</summary>
    private byte[] Holes()
    {
        var g = _scene.Session!.Ground;
        byte[] h = new byte[g.W * g.H];
        for (int p = 0; p < h.Length; p++)
        {
            h[p] = Byte(g.MaskOf(p, 3));
        }
        return h;
    }

    /// <summary>The areas as last sent, and their kinds' Unreal classes.</summary>
    private string _areas;
    private readonly IReadOnlyList<MapAreas.Kind> _areaKinds;

    /// <summary>Takes the scene as it is now as in sync with Unreal (after something came from it).</summary>
    private void Rebaseline()
    {
        (_heights, _mask, _gone, _count) = Snapshot();
        _water = MapWater.Snapshot();
        _holes = Holes();
    }

    /// <summary>A live link to the running Unreal editor, from now on (what is already there is taken as in sync), or why not.</summary>
    public static async Task<(UnrealLive? Live, string? Why)> Connect(WorldScene scene)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (!File.Exists(file))
        {
            return (null, "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json). Open the project in Unreal with the plugin on.");
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        string url = $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}", token = cfg["token"]!.GetValue<string>();
        try
        {
            using var ping = new HttpRequestMessage(HttpMethod.Get, url + "/groundwork/ping");
            ping.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var r = await Http.SendAsync(ping);
            if (!r.IsSuccessStatusCode)
            {
                return (null, $"Unreal answered {(int)r.StatusCode}: restart Unreal so the token is fresh.");
            }
            if (Mismatch(await r.Content.ReadAsStringAsync()) is string why)
            {
                return (null, why);
            }
        }
        catch (HttpRequestException e)
        {
            return (null, "Unreal does not answer: " + e.Message);
        }
        string names = Path.Combine(ModelStore.MapModels, "names.json");
        var meshes = File.Exists(names) ? JsonNode.Parse(File.ReadAllText(names))!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>()) : [];
        // the markers' Unreal classes (the catalog's markers)
        var markers = new Dictionary<string, (string, string?)>();
        string catalog = MapTools.CatalogFile;
        if (File.Exists(catalog) && JsonNode.Parse(File.ReadAllText(catalog))?["markers"] is JsonObject ms)
        {
            foreach (var (id, m) in ms)
            {
                markers[id] = (m?["unreal"]?.GetValue<string>() ?? "TargetPoint", m?["tag"]?.GetValue<string>());
            }
        }
        var live = new UnrealLive(scene, url, token, meshes, markers);
        live._versions = await live.Versions() ?? default;
        return (live, null);
    }

    /// <summary>Why the bridge's answer to a ping is not one this editor can work with, or null when it is.</summary>
    internal static string? Mismatch(string ping)
    {
        JsonNode? j;
        try
        {
            j = JsonNode.Parse(ping);
        }
        catch (System.Text.Json.JsonException)
        {
            j = null;
        }
        if (j?["bridge"]?.GetValue<string>() != "groundwork")
        {
            return "The project's Saved/GroundworkBridge.json leads to something that is not the Groundwork Bridge plugin.";
        }
        int version = j["version"]?.GetValue<int>() ?? 0;
        return version == Protocol ? null
            : $"The project's Groundwork Bridge plugin speaks version {version}, this editor {Protocol}: update the {(version < Protocol ? "plugin" : "editor")}.";
    }

    /// <summary>The scene as it is: heights, each layer's weight per grid point, each thing's gone flag, how many things.</summary>
    private (float[] Heights, byte[] Mask, bool[] Gone, int Count) Snapshot()
    {
        var g = _scene.Session!.Ground;
        float[] heights = (float[])_scene.Heights.Clone();
        int n = _project.Layers.Count;
        byte[] mask = new byte[g.W * g.H * n];
        for (int p = 0; p < g.W * g.H; p++)
        {
            // layer 0 is the ground under the others: what they leave
            int rest = 255;
            for (int k = 1; k < n; k++)
            {
                rest -= mask[(p * n) + k] = Byte(g.MaskOf(p, k - 1));
            }
            mask[p * n] = (byte)Math.Max(0, rest);
        }
        bool[] gone;
        lock (_scene.Things)
        {
            gone = [.. _scene.Things.Select(t => t.Gone)];
        }
        return (heights, mask, gone, gone.Length);
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    /// <summary>Sends what changed since the last sync (one at a time; a change during a send is sent after it).</summary>
    public async Task<string?> Sync()
    {
        if (_busy)   // called on the window's thread: one send at a time, a change meanwhile sent after it
        {
            _again = true;
            return null;
        }
        _busy = true;
        try
        {
            string? said;
            do
            {
                _again = false;
                said = await SyncOnce();
            }
            while (_again);
            return said;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<string> SyncOnce()
    {
        if (_pulling)
        {
            return "";
        }
        var (heights, mask, gone, count) = Snapshot();
        var g = _scene.Session!.Ground;
        var said = new List<string>();
        // ground: the rectangle of changed points, in map pixels
        int x0 = int.MaxValue, z0 = int.MaxValue, x1 = -1, z1 = -1, mx0 = int.MaxValue, mz0 = int.MaxValue, mx1 = -1, mz1 = -1;
        for (int p = 0; p < heights.Length; p++)
        {
            int gx = p % g.W, gz = p / g.W;
            if (MathF.Abs(heights[p] - _heights[p]) > 0.002f)
            {
                (x0, z0, x1, z1) = (Math.Min(x0, gx), Math.Min(z0, gz), Math.Max(x1, gx), Math.Max(z1, gz));
            }
            if (!mask.AsSpan(p * _project.Layers.Count, _project.Layers.Count).SequenceEqual(_mask.AsSpan(p * _project.Layers.Count, _project.Layers.Count)))
            {
                (mx0, mz0, mx1, mz1) = (Math.Min(mx0, gx), Math.Min(mz0, gz), Math.Max(mx1, gx), Math.Max(mz1, gz));
            }
        }
        if (x1 >= 0 && Pixels(g, x0, z0, x1, z1) is var (col, row, w, h) && w > 0)
        {
            var values = new JsonArray();
            for (int r = row; r < row + h; r++)
            {
                for (int c = col; c < col + w; c++)
                {
                    values.Add(heights[GridOf(g, c, r)]);
                }
            }
            said.Add(await Post("/groundwork/heights", new JsonObject { ["x0"] = _map.X0, ["y0"] = _map.Y0, ["col"] = col, ["row"] = row, ["w"] = w, ["h"] = h, ["heights"] = values }));
        }
        if (mx1 >= 0 && Pixels(g, mx0, mz0, mx1, mz1) is var (mcol, mrow, mw, mh) && mw > 0)
        {
            // into the texture's channels (map.json's unreal.layersTexture; the ground layer has none), else the
            // landscape's paint layers by name
            string? texture = _project.UnrealLayersTexture;
            int n = _project.Layers.Count;
            var layers = new JsonArray();
            for (int k = texture == null ? 0 : 1; k < n; k++)
            {
                byte[] weights = new byte[mw * mh];
                for (int r = 0; r < mh; r++)
                {
                    for (int c = 0; c < mw; c++)
                    {
                        weights[(r * mw) + c] = mask[(GridOf(g, mcol + c, mrow + r) * n) + k];
                    }
                }
                var layer = _project.Layers[k];
                layers.Add(new JsonObject { ["name"] = layer.Name, ["channel"] = texture == null || layer.Channel < 0 ? null : "rgb"[layer.Channel].ToString(), ["weights"] = Convert.ToBase64String(weights) });
            }
            said.Add(await Post("/groundwork/layers", new JsonObject { ["x0"] = _map.X0, ["y0"] = _map.Y0, ["col"] = mcol, ["row"] = mrow, ["w"] = mw, ["h"] = mh, ["texture"] = texture, ["layers"] = layers }));
        }
        // objects: things gone since (taken away), back or new (added)
        var add = new JsonArray();
        var remove = new JsonArray();
        lock (_scene.Things)
        {
            for (int i = 0; i < count; i++)
            {
                bool was = i >= _count || _gone[i], now = gone[i];
                if (was == now || ModelStore.ModelName(_scene.Things[i].Prefab) is not string name || !_meshes.TryGetValue(name.Split('@')[0], out string? mesh))
                {
                    continue;
                }
                var t = _scene.Things[i];
                var at = new Vector3(t.Position.X * 100, -t.Position.Z * 100, t.Position.Y * 100);
                if (mesh.StartsWith("marker:", StringComparison.Ordinal))
                {
                    string id = mesh[7..];
                    var (cls, tag) = _markers.GetValueOrDefault(id, ("TargetPoint", null));
                    var mq = UnrealQuat(t.Rotation);
                    (now ? remove : add).Add(new JsonObject { ["marker"] = id, ["class"] = cls, ["tag"] = tag, ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z, ["qx"] = mq.X, ["qy"] = mq.Y, ["qz"] = mq.Z, ["qw"] = mq.W, ["sx"] = 1, ["sy"] = 1, ["sz"] = 1 });
                    continue;
                }
                if (now)
                {
                    remove.Add(new JsonObject { ["mesh"] = mesh, ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z });
                }
                else
                {
                    float s = t.Scale == 0 ? 1 : t.Scale;
                    float[] sc = name.Contains('@') ? [.. name[(name.IndexOf('@') + 1)..].Split('_').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture) * s)] : [s, s, s];
                    var q = UnrealQuat(t.Rotation);
                    add.Add(new JsonObject { ["mesh"] = mesh, ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z, ["qx"] = q.X, ["qy"] = q.Y, ["qz"] = q.Z, ["qw"] = q.W, ["sx"] = sc[0], ["sy"] = sc[1], ["sz"] = sc[2] });
                }
            }
        }
        if (add.Count > 0 || remove.Count > 0)
        {
            said.Add(await Post("/groundwork/objects", new JsonObject { ["add"] = add, ["remove"] = remove }));
        }
        (_heights, _mask, _gone, _count) = (heights, mask, gone, count);
        // rivers and lakes: the water bodies prefix + name rebuilt in Unreal (the map's file is written by Save)
        if (MapWater.Snapshot() is var water && water != _water)
        {
            var body = JsonNode.Parse(water)!;
            body["prefix"] = _project.UnrealWaterPrefix;
            said.Add(await Post("/groundwork/water", body));
            _water = water;
        }
        // holes: the landscape's visibility over the rectangle of changed points
        byte[] holes = Holes();
        int hx0 = int.MaxValue, hz0 = int.MaxValue, hx1 = -1, hz1 = -1;
        for (int p = 0; p < holes.Length; p++)
        {
            if (holes[p] != _holes[p])
            {
                int gx = p % g.W, gz = p / g.W;
                (hx0, hz0, hx1, hz1) = (Math.Min(hx0, gx), Math.Min(hz0, gz), Math.Max(hx1, gx), Math.Max(hz1, gz));
            }
        }
        if (hx1 >= 0 && Pixels(g, hx0, hz0, hx1, hz1) is var (hcol, hrow, hw, hh) && hw > 0)
        {
            byte[] weights = new byte[hw * hh];
            for (int r = 0; r < hh; r++)
            {
                for (int c = 0; c < hw; c++)
                {
                    weights[(r * hw) + c] = holes[GridOf(g, hcol + c, hrow + r)];
                }
            }
            said.Add(await Post("/groundwork/holes", new JsonObject { ["x0"] = _map.X0, ["y0"] = _map.Y0, ["col"] = hcol, ["row"] = hrow, ["w"] = hw, ["h"] = hh, ["weights"] = Convert.ToBase64String(weights) }));
        }
        _holes = holes;
        // areas: volumes tagged gw_area:<name>, rebuilt from the map's
        if (MapAreas.Snapshot() is var areas && areas != _areas)
        {
            said.Add(await Post("/groundwork/areas", MapAreas.ForUnreal(_areaKinds)));
            _areas = areas;
        }
        return said.Count == 0 ? "" : "Live: " + string.Join("; ", said);
    }

    private async Task<(long, long, long)?> Versions()
    {
        try
        {
            var v = JsonNode.Parse(await Get("/groundwork/version"))!;
            return (v["heights"]!.GetValue<long>(), v["objects"]!.GetValue<long>(), v["water"]!.GetValue<long>());
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or NullReferenceException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// What was changed in Unreal since the last look (its counters went up), brought into the editor: ground, objects and
    /// markers, rivers and lakes, each a step in the history. Null when nothing came.
    /// </summary>
    public async Task<string?> Pull()
    {
        if (_busy || _scene.Session is not { } s || s.Stroking || await Versions() is not var (h, o, w) || (h, o, w) == _versions)
        {
            return null;
        }
        var said = new List<string>();
        _pulling = true;
        try
        {
            if (h != _versions.Heights && PullHeights(s, JsonNode.Parse(await Get("/groundwork/heights_all"))!) is > 0 and var n)
            {
                said.Add($"{n} ground points");
            }
            if (o != _versions.Objects && PullObjects(s, JsonNode.Parse(await Get("/groundwork/objects_all"))!) is var (added, removed) && added + removed > 0)
            {
                said.Add($"{added} objects added, {removed} taken away");
            }
            if (w != _versions.Water && PullWater(s, JsonNode.Parse(await Get("/groundwork/water_all"))!, _project.UnrealWaterPrefix))
            {
                said.Add("rivers and lakes");
            }
            _versions = (h, o, w);
            Rebaseline();
        }
        finally
        {
            _pulling = false;
        }
        return said.Count == 0 ? null : "From Unreal: " + string.Join(", ", said);
    }

    private static int PullHeights(EditSession s, JsonNode j)
    {
        if (j["data"] is null)
        {
            return 0;
        }
        byte[] raw = Convert.FromBase64String(j["data"]!.GetValue<string>());
        int w = j["w"]!.GetValue<int>(), h = j["h"]!.GetValue<int>();
        double x = j["x"]!.GetValue<double>(), y = j["y"]!.GetValue<double>(), sx = j["sx"]!.GetValue<double>(), sy = j["sy"]!.GetValue<double>(), sz = j["sz"]!.GetValue<double>();
        var g = s.Ground;
        var targets = new Dictionary<int, float>();
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                int gx = (int)Math.Round(((x + (c * sx)) / 100) - g.OriginX), gz = (int)Math.Round((-(y + (r * sy)) / 100) - g.OriginZ);
                if (gx < 1 || gz < 1 || gx >= g.W - 1 || gz >= g.H - 1)
                {
                    continue;
                }
                float metres = (float)((BitConverter.ToUInt16(raw, ((r * w) + c) * 2) - 32768) / 128.0 * sz / 100.0);
                int p = (gz * g.W) + gx;
                if (MathF.Abs(g.HeightOf(p) - metres) > 0.01f)
                {
                    targets[p] = metres;
                }
            }
        }
        if (targets.Count == 0)
        {
            return 0;
        }
        _ = s.EditGround("From Unreal: ground", ground =>
        {
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = 0, z1 = 0;
            foreach (var (p, m) in targets)
            {
                ground.SetHeight(p, m);
                (x0, z0, x1, z1) = (Math.Min(x0, p % g.W), Math.Min(z0, p / g.W), Math.Max(x1, p % g.W), Math.Max(z1, p / g.W));
            }
            return ([.. targets.Keys], (x0, z0, x1, z1));
        });
        return targets.Count;
    }

    private (int Added, int Removed) PullObjects(EditSession s, JsonNode j)
    {
        var modelOf = _meshes.Where(kv => !kv.Key.Contains('@')).GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
        // Unreal's objects and markers, as (model, editor position, Euler, scale)
        var theirs = new List<(string Model, Vector3 At, Vector3 Euler, float Scale)>();
        foreach (var o in j["objects"]?.AsArray() ?? [])
        {
            if (!modelOf.TryGetValue(o![0]!.GetValue<string>(), out string? model))
            {
                continue;
            }
            float F(int i) => o[i]!.GetValue<float>();
            float sx = F(8), sy = F(9), sz = F(10);
            string variant = FormattableString.Invariant($"{model}@{sx:0.00}_{sy:0.00}_{sz:0.00}");
            bool uneven = MathF.Max(sx, MathF.Max(sy, sz)) - MathF.Min(sx, MathF.Min(sy, sz)) > 0.01f * MathF.Max(sx, MathF.Max(sy, sz));
            bool known = uneven && ModelStore.ModelName(Groundwork.App.AssetIds.Of(variant)) != null;   // the scaled copies are models of their own
            theirs.Add((known ? variant : model, new Vector3(F(1) / 100, F(3) / 100, -F(2) / 100), EditorEuler(new Quaternion(F(4), F(5), F(6), F(7))), known ? 1 : sx));
        }
        foreach (var m in j["markers"]?.AsArray() ?? [])
        {
            float F(int i) => m![i]!.GetValue<float>();
            if (!modelOf.TryGetValue("marker:" + m![0]!.GetValue<string>(), out string? marker))
            {
                continue;
            }
            theirs.Add((marker, new Vector3(F(1) / 100, F(3) / 100, -F(2) / 100), EditorEuler(new Quaternion(0, 0, MathF.Sin(F(4) * MathF.PI / 360), MathF.Cos(F(4) * MathF.PI / 360))), 1));
        }
        // matched by model and place (5 cm), on a 1 m grid
        var mine = new Dictionary<(string, int, int), List<(int Index, Vector3 At)>>();
        lock (_scene.Things)
        {
            for (int i = 0; i < _scene.Things.Count; i++)
            {
                var t = _scene.Things[i];
                if (!t.Gone && ModelStore.ModelName(t.Prefab) is string name)
                {
                    var key = (name, (int)MathF.Floor(t.Position.X), (int)MathF.Floor(t.Position.Z));
                    (mine.TryGetValue(key, out var l) ? l : mine[key] = []).Add((i, t.Position));
                }
            }
        }
        var add = new List<(NewObject, bool)>();
        foreach (var (Model, At, Euler, Scale) in theirs)
        {
            bool found = false;
            for (int dx = -1; dx <= 1 && !found; dx++)
            {
                for (int dz = -1; dz <= 1 && !found; dz++)
                {
                    if (mine.TryGetValue((Model, (int)MathF.Floor(At.X) + dx, (int)MathF.Floor(At.Z) + dz), out var list))
                    {
                        int k = list.FindIndex(m => Vector3.Distance(m.At, At) < 0.05f);
                        if (k >= 0)
                        {
                            list.RemoveAt(k);
                            found = true;
                        }
                    }
                }
            }
            if (!found)
            {
                add.Add((new NewObject(0, Groundwork.App.AssetIds.Of(Model), At, Euler, MathF.Abs(Scale - 1) < 1e-3f ? 0 : Scale), false));
            }
        }
        // left over: in the editor, no longer in Unreal (but only kinds Unreal knows: the editor may have others)
        int[] remove = [.. mine.Where(kv => _meshes.ContainsKey(kv.Key.Item1.Split('@')[0])).SelectMany(kv => kv.Value).Select(m => m.Index)];
        if (add.Count + remove.Length > 0)
        {
            _ = s.Commit("From Unreal: objects", null, remove, add);
        }
        return (add.Count, remove.Length);
    }

    /// <summary>
    /// Unreal's rivers and lakes labelled prefix + name into the map's (others in the level are not the map's): those the
    /// map has keep their place, new ones come after.
    /// </summary>
    private static bool PullWater(EditSession s, JsonNode j, string prefix)
    {
        static float F(JsonNode? n, int i) => n![i]!.GetValue<float>();
        string before = MapWater.Snapshot();
        // name -> the body as the map's file holds it
        Dictionary<string, JsonObject> Theirs(string kind, Func<JsonNode, JsonObject?> read)
        {
            var found = new Dictionary<string, JsonObject>();
            foreach (var b in j[kind]?.AsArray() ?? [])
            {
                if (b?["name"]?.GetValue<string>() is string label && label.StartsWith(prefix, StringComparison.Ordinal) && read(b) is { } o)
                {
                    string name = label[prefix.Length..].Replace('_', ' ');
                    o["name"] = name;
                    found[name] = o;
                }
            }
            return found;
        }
        static JsonArray InOrder(IEnumerable<string> mine, Dictionary<string, JsonObject> theirs) =>
            [.. mine.Where(theirs.ContainsKey).Concat(theirs.Keys.Where(k => !mine.Contains(k))).Select(k => (JsonNode)theirs[k])];
        var rivers = Theirs("rivers", r =>
        {
            var pts = r["points"]!.AsArray();
            return pts.Count < 2 ? null : new JsonObject
            {
                ["top"] = F(pts[0], 2) / 100,
                ["end"] = F(pts[^1], 2) / 100,
                ["points"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(F(p, 0) / 100, F(p, 1) / 100, F(p, 3) / 100, F(p, 4) / 100, F(p, 5) / 100))]),
            };
        });
        var lakes = Theirs("lakes", l =>
        {
            var pts = l["points"]!.AsArray();
            return new JsonObject
            {
                ["level"] = pts.Count > 0 ? F(pts[0], 2) / 100 : 0,
                ["depth"] = l["depth"]!.GetValue<float>() / 100,
                ["outline"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(F(p, 0) / 100, F(p, 1) / 100))]),
            };
        });
        MapWater.Restore(new JsonObject { ["rivers"] = InOrder(MapWater.Rivers.Select(r => r.Name), rivers), ["lakes"] = InOrder(MapWater.Lakes.Select(l => l.Name), lakes) }.ToJsonString());
        string after = MapWater.Snapshot();
        if (after == before)
        {
            return false;
        }
        _ = s.RecordExtra("From Unreal: rivers and lakes", again => MapWater.Restore(again ? after : before));
        return true;
    }

    /// <summary>Unreal's quaternion as the editor's Euler (Unity's: y, then x, then z); UnrealQuat the other way.</summary>
    private static Vector3 EditorEuler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        float[,] u =
        {
            { 1 - (2 * ((y * y) + (z * z))), 2 * ((x * y) - (z * w)), 2 * ((x * z) + (y * w)) },
            { 2 * ((x * y) + (z * w)), 1 - (2 * ((x * x) + (z * z))), 2 * ((y * z) - (x * w)) },
            { 2 * ((x * z) - (y * w)), 2 * ((y * z) + (x * w)), 1 - (2 * ((x * x) + (y * y))) },
        };
        // R = M U M^T, M: Unreal axis -> editor axis ((X, Y, Z) -> (X, Z, -Y))
        float[,] r =
        {
            { u[0, 0], u[0, 2], -u[0, 1] },
            { u[2, 0], u[2, 2], -u[2, 1] },
            { -u[1, 0], -u[1, 2], u[1, 1] },
        };
        float ex = MathF.Asin(Math.Clamp(-r[1, 2], -1, 1)), ey = MathF.Atan2(r[0, 2], r[2, 2]), ez = MathF.Atan2(r[1, 0], r[1, 1]);
        const float D = 180 / MathF.PI;
        return new Vector3(ex * D, ey * D, ez * D);
    }

    private async Task<string> Get(string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _url + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var r = await Http.SendAsync(req);
        return await r.Content.ReadAsStringAsync();
    }

    /// <summary>The map pixel rectangle (col, row, w, h) over a grid rectangle, clipped to the map.</summary>
    private (int Col, int Row, int W, int H) Pixels(Ground g, int x0, int z0, int x1, int z1)
    {
        var (c0, r1) = _map.Pixel(g.OriginX + x0, g.OriginZ + z0);
        var (c1, r0) = _map.Pixel(g.OriginX + x1, g.OriginZ + z1);
        int col = Math.Max(0, Math.Min(c0, c1)), row = Math.Max(0, Math.Min(r0, r1));
        int colEnd = Math.Min(_map.Cols - 1, Math.Max(c0, c1)), rowEnd = Math.Min(_map.Rows - 1, Math.Max(r0, r1));
        return (col, row, colEnd - col + 1, rowEnd - row + 1);
    }

    /// <summary>The grid point under a map pixel.</summary>
    private int GridOf(Ground g, int col, int row)
    {
        int gx = Math.Clamp((int)MathF.Round(col + _map.X0 - g.OriginX), 0, g.W - 1), gz = Math.Clamp((int)MathF.Round(-(row + _map.Y0) - g.OriginZ), 0, g.H - 1);
        return (gz * g.W) + gx;
    }

    /// <summary>The editor's Euler (Unity's: y, then x, then z) as Unreal's quaternion.</summary>
    private static Quaternion UnrealQuat(Vector3 euler)
    {
        float x = euler.X * MathF.PI / 180, y = euler.Y * MathF.PI / 180, z = euler.Z * MathF.PI / 180;
        float[,] ry = { { MathF.Cos(y), 0, MathF.Sin(y) }, { 0, 1, 0 }, { -MathF.Sin(y), 0, MathF.Cos(y) } };
        float[,] rx = { { 1, 0, 0 }, { 0, MathF.Cos(x), -MathF.Sin(x) }, { 0, MathF.Sin(x), MathF.Cos(x) } };
        float[,] rz = { { MathF.Cos(z), -MathF.Sin(z), 0 }, { MathF.Sin(z), MathF.Cos(z), 0 }, { 0, 0, 1 } };
        static float[,] Mul(float[,] a, float[,] b)
        {
            float[,] m = new float[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        m[i, j] += a[i, k] * b[k, j];
                    }
                }
            }
            return m;
        }
        float[,] r = Mul(Mul(ry, rx), rz);
        float[,] u = { { r[0, 0], -r[0, 2], r[0, 1] }, { -r[2, 0], r[2, 2], -r[2, 1] }, { r[1, 0], -r[1, 2], r[1, 1] } };
        float t = u[0, 0] + u[1, 1] + u[2, 2];
        if (t > 0)
        {
            float s = MathF.Sqrt(t + 1) * 2;
            return new Quaternion((u[2, 1] - u[1, 2]) / s, (u[0, 2] - u[2, 0]) / s, (u[1, 0] - u[0, 1]) / s, s / 4);
        }
        int a = u[0, 0] >= u[1, 1] && u[0, 0] >= u[2, 2] ? 0 : u[1, 1] >= u[2, 2] ? 1 : 2, b = (a + 1) % 3, c = (a + 2) % 3;
        float sq = MathF.Sqrt(1 + u[a, a] - u[b, b] - u[c, c]) * 2;
        float[] q = new float[3];
        q[a] = sq / 4;
        q[b] = (u[b, a] + u[a, b]) / sq;
        q[c] = (u[c, a] + u[a, c]) / sq;
        return new Quaternion(q[0], q[1], q[2], (u[c, b] - u[b, c]) / sq);
    }

    /// <summary>
    /// Where Play here starts the game in Unreal: the ground point in Unreal centimetres, 1.2 m up (the player's capsule
    /// stands on the ground), facing the way the camera looks (its yaw: 0 looks north, +z).
    /// </summary>
    internal static (float X, float Y, float Z, float Yaw) PlayStart(Vector3 ground, float cameraYaw) =>
        (ground.X * 100, -ground.Z * 100, (ground.Y + 1.2f) * 100, MathF.Atan2(-MathF.Cos(cameraYaw), -MathF.Sin(cameraYaw)) * 180 / MathF.PI);

    /// <summary>
    /// Starts the game in the open Unreal level (Play In Editor) at a ground point, facing the camera's way: what Unreal
    /// answered, or why not.
    /// </summary>
    public static async Task<string> Play(Vector3 ground, float cameraYaw)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (Project.Length == 0 || !File.Exists(file))
        {
            return "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json).";
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        var (x, y, z, yaw) = PlayStart(ground, cameraYaw);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}/groundwork/play")
        {
            Content = new StringContent(new JsonObject { ["x"] = x, ["y"] = y, ["z"] = z, ["yaw"] = yaw }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["token"]!.GetValue<string>());
        try
        {
            using var r = await Http.SendAsync(req);
            return r.StatusCode == System.Net.HttpStatusCode.NotFound ? "This Groundwork Bridge plugin cannot Play here: update it."
                : !r.IsSuccessStatusCode ? $"Unreal answered {(int)r.StatusCode}: restart Unreal so the token is fresh."
                : await r.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            return "Unreal does not answer: " + e.Message;
        }
    }

    /// <summary>
    /// The map's heights (metres) as an Unreal landscape's 16-bit values (little-endian, 32768 at 0) and the Z scale that
    /// holds them: Unreal's usual 100 (±256 m), more for a map that reaches further, to 10 cm.
    /// </summary>
    internal static (float ZScale, byte[] Data) LandscapeHeights(float[] heights)
    {
        float most = heights.Length > 0 ? heights.Max(MathF.Abs) : 0;
        float z = MathF.Max(100, MathF.Ceiling(most * 100 / 250 / 10) * 10);
        byte[] data = new byte[heights.Length * 2];
        for (int i = 0; i < heights.Length; i++)
        {
            ushort v = (ushort)Math.Clamp(MathF.Round((heights[i] * 100 / z * 128) + 32768), 0, 65535);
            (data[i * 2], data[(i * 2) + 1]) = ((byte)(v & 0xFF), (byte)(v >> 8));
        }
        return (z, data);
    }

    /// <summary>
    /// Makes the map's landscape in the open Unreal level (replace: rebuilds the one it has, at the map's size), then
    /// paints it with the map's layers (PaintLandscape): what Unreal answered, or why not.
    /// </summary>
    public static async Task<string> MakeLandscape(MapProject map, WorldScene scene, bool replace)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (Project.Length == 0 || !File.Exists(file))
        {
            return "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json).";
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        var (heights, cols, rows) = map.HeightsNow(scene);
        var (z, data) = LandscapeHeights(heights);
        var body = new JsonObject { ["x0"] = map.X0, ["y0"] = map.Y0, ["cols"] = cols, ["rows"] = rows, ["zscale"] = z, ["heights"] = Convert.ToBase64String(data), ["replace"] = replace };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}/groundwork/landscape")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["token"]!.GetValue<string>());
        try
        {
            using var r = await Http.SendAsync(req);
            string said = r.StatusCode == System.Net.HttpStatusCode.NotFound ? "This Groundwork Bridge plugin cannot make landscapes: update it."
                : !r.IsSuccessStatusCode ? $"Unreal answered {(int)r.StatusCode}: restart Unreal so the token is fresh."
                : await r.Content.ReadAsStringAsync();
            // then its layers: a material of the map's layers, every layer painted, the holes cut
            return said.StartsWith("landscape made", StringComparison.Ordinal) ? said + "; " + await PaintLandscape(map, scene) : said;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return "Unreal does not answer: " + e.Message;
        }
    }

    /// <summary>
    /// A camera looking at an editor point from a compass yaw (0: looking north, 90: east), a pitch down and a distance
    /// (metres), as Unreal's: its place in centimetres, its yaw and pitch in degrees (pitch below 0 looks down).
    /// </summary>
    internal static (Vector3 At, float Yaw, float Pitch) CameraFor(Vector3 target, float yawDegrees, float pitchDegrees, float distance)
    {
        float y = yawDegrees * MathF.PI / 180, p = pitchDegrees * MathF.PI / 180;
        var look = new Vector3(MathF.Sin(y) * MathF.Cos(p), -MathF.Sin(p), MathF.Cos(y) * MathF.Cos(p));   // editor x east, y up, z north
        var eye = target - (look * distance);
        return (new Vector3(eye.X * 100, -eye.Z * 100, eye.Y * 100), MathF.Atan2(-look.Z, look.X) * 180 / MathF.PI, -pitchDegrees);
    }

    /// <summary>A picture of the open Unreal level from a camera (see CameraFor) as the game draws it: a PNG, or why not.</summary>
    public static async Task<(byte[]? Png, string Said)> Screenshot(Vector3 target, float yawDegrees, float pitchDegrees, float distance, float fov, int width, int height)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (Project.Length == 0 || !File.Exists(file))
        {
            return (null, "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json).");
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        var (at, yaw, pitch) = CameraFor(target, yawDegrees, pitchDegrees, distance);
        var body = new JsonObject { ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z, ["yaw"] = yaw, ["pitch"] = pitch, ["fov"] = fov, ["width"] = width, ["height"] = height };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}/groundwork/screenshot")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["token"]!.GetValue<string>());
        try
        {
            using var r = await Http.SendAsync(req);
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (null, "This Groundwork Bridge plugin cannot take pictures: update it.");
            }
            var j = JsonNode.Parse(await r.Content.ReadAsStringAsync());
            return j?["png"]?.GetValue<string>() is string png
                ? (Convert.FromBase64String(png), j["playing"]?.GetValue<bool>() == true ? "From the running game." : "From the editor's level.")
                : (null, "Unreal took no picture: " + (j?["error"]?.GetValue<string>() ?? "no answer"));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return (null, "Unreal does not answer: " + e.Message);
        }
    }

    private async Task<string> Post(string path, JsonNode body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _url + path) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var r = await Http.SendAsync(req);
        return await r.Content.ReadAsStringAsync();
    }
}
