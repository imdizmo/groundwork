using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>Bringing the map's glTF models into the open Unreal project (the bridge's /groundwork/import), so Live and Send can place them.</summary>
public sealed partial class UnrealLive
{
    /// <summary>Imports take a while: up to half an hour for a whole kit.</summary>
    private static readonly HttpClient SlowHttp = new() { Timeout = TimeSpan.FromMinutes(30) };

    /// <summary>The models folder's names.json (model name to Unreal mesh), empty when it has none or cannot be read.</summary>
    private static JsonObject Names(string models)
    {
        string file = Path.Combine(models, "names.json");
        try
        {
            return File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o ? o : [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>The glTF models under a models folder that names.json has no Unreal mesh for.</summary>
    internal static List<string> ModelsToImport(string models)
    {
        var names = Names(models);
        return [.. ModelStore.ModelFiles(models).Where(f => !names.ContainsKey(Path.GetFileNameWithoutExtension(f))).Order(StringComparer.Ordinal)];
    }

    /// <summary>Unreal's meshes for models (name to mesh path) added to names.json.</summary>
    internal static void KeepMeshes(string models, JsonObject meshes)
    {
        var names = Names(models);
        foreach (var (name, mesh) in meshes)
        {
            if (mesh?.GetValue<string>() is { Length: > 0 } path)
            {
                names[name] = path;
            }
        }
        File.WriteAllText(Path.Combine(models, "names.json"), names.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// The map's glTF models that Unreal has no mesh for, imported into the open Unreal project (saved there, under
    /// /Game/Groundwork/Models) and kept in names.json: what was done, or why not.
    /// </summary>
    public static async Task<string> ImportModels(string models)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (Project.Length == 0 || !File.Exists(file))
        {
            return "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json).";
        }
        var send = ModelsToImport(models);
        if (send.Count == 0)
        {
            return "Every model already has its Unreal mesh.";
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        var body = new JsonObject { ["files"] = new JsonArray([.. send.Select(f => (JsonNode)Path.GetFullPath(f))]) };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}/groundwork/import")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["token"]!.GetValue<string>());
        try
        {
            using var r = await SlowHttp.SendAsync(req);
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "This Groundwork Bridge plugin cannot import models: update it.";
            }
            if (!r.IsSuccessStatusCode)
            {
                return $"Unreal answered {(int)r.StatusCode}: restart Unreal so the token is fresh.";
            }
            var answer = JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonObject;
            if (answer?["meshes"] is not JsonObject meshes)
            {
                return "Unreal could not import them: " + (answer?["error"]?.GetValue<string>() ?? "no answer");
            }
            KeepMeshes(models, meshes);
            int failed = answer["failed"]?.AsArray().Count ?? 0;
            return $"{meshes.Count} models brought into Unreal" + (failed > 0 ? $", {failed} could not be: {string.Join(", ", answer["failed"]!.AsArray().Take(5))}." : ".");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return "Unreal does not answer: " + e.Message;
        }
    }

    /// <summary>What /groundwork/layer_material is asked for the map: its name and each layer's name, texture (full path), colour and size.</summary>
    internal static JsonObject LayerMaterialBody(MapProject map) => new()
    {
        ["name"] = map.Name,
        ["layers"] = new JsonArray([.. map.Layers.Select(l => (JsonNode)new JsonObject
        {
            ["name"] = l.Name,
            ["texture"] = l.Texture is { } t && File.Exists(t) ? Path.GetFullPath(t) : null,
            ["colour"] = new JsonArray(l.Colour.X, l.Colour.Y, l.Colour.Z),
            ["size"] = l.Size,
        })]),
    };

    /// <summary>
    /// The level's landscape given a material of the map's layers (the bridge makes it, its paint layers and textures,
    /// saved in the project), then every layer painted as the map has it, the holes cut, and its rivers, lakes and sea
    /// made: what Unreal said.
    /// </summary>
    public static async Task<string> PaintLandscape(MapProject map, WorldScene scene)
    {
        string file = Path.Combine(Project, "Saved/GroundworkBridge.json");
        if (Project.Length == 0 || !File.Exists(file))
        {
            return "Unreal is not running with the Groundwork Bridge plugin (no Saved/GroundworkBridge.json).";
        }
        var cfg = JsonNode.Parse(File.ReadAllText(file))!;
        async Task<string> Post(string path, JsonObject body)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg["port"]!.GetValue<int>()}{path}")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["token"]!.GetValue<string>());
            using var r = await SlowHttp.SendAsync(req);
            return r.StatusCode == System.Net.HttpStatusCode.NotFound ? "This Groundwork Bridge plugin cannot paint landscapes: update it."
                : !r.IsSuccessStatusCode ? $"Unreal answered {(int)r.StatusCode}." : await r.Content.ReadAsStringAsync();
        }
        try
        {
            string made = await Post("/groundwork/layer_material", LayerMaterialBody(map));
            if (!made.StartsWith("landscape material", StringComparison.Ordinal))
            {
                return made;
            }
            // every layer by name, a band of rows at a time (a whole big map is too much for one request)
            var (weights, cols, rows) = map.LayersNow(scene);
            var g = scene.Session!.Ground;
            byte[] holes = new byte[cols * rows];
            var raster = map.ReadRaster();
            for (int gz = 0; gz < g.H; gz++)
            {
                for (int gx = 0; gx < g.W; gx++)
                {
                    var (col, row) = raster.Pixel(g.OriginX + gx, g.OriginZ + gz);
                    if (raster.Holds(col, row) && g.MaskOf((gz * g.W) + gx, 3) > 0.5f)
                    {
                        holes[(row * cols) + col] = 255;
                    }
                }
            }
            const int Band = 256;
            for (int row = 0; row < rows; row += Band)
            {
                int h = Math.Min(Band, rows - row);
                var layers = new JsonArray([.. map.Layers.Select((l, k) => (JsonNode)new JsonObject
                {
                    ["name"] = l.Name,
                    ["weights"] = Convert.ToBase64String(weights[k], row * cols, h * cols),
                })]);
                string said = await Post("/groundwork/layers", new JsonObject { ["x0"] = map.X0, ["y0"] = map.Y0, ["col"] = 0, ["row"] = row, ["w"] = cols, ["h"] = h, ["layers"] = layers });
                if (said.Contains("not in this level", StringComparison.Ordinal))
                {
                    return made + "; " + said;
                }
            }
            string cut = holes.Any(v => v > 0)
                ? "; " + await Post("/groundwork/holes", new JsonObject { ["x0"] = map.X0, ["y0"] = map.Y0, ["col"] = 0, ["row"] = 0, ["w"] = cols, ["h"] = rows, ["weights"] = Convert.ToBase64String(holes) })
                : "";
            string water = await Post("/groundwork/water", WaterBody(map));
            return $"{made}; {map.Layers.Count} layers painted" + cut + "; " + water;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return "Unreal does not answer: " + e.Message;
        }
    }

    /// <summary>
    /// What /groundwork/water is sent for the map: its rivers and lakes (MapWater's), the water prefix, and its sea at
    /// its sea level over its extent (none for a map whose sea is far below everything: a dry world's edge).
    /// </summary>
    internal static JsonObject WaterBody(MapProject map)
    {
        var body = JsonNode.Parse(MapWater.Snapshot())!.AsObject();
        body["prefix"] = map.UnrealWaterPrefix;
        if (map.SeaLevel > -500)
        {
            var raster = map.ReadRaster();
            body["sea"] = new JsonObject { ["level"] = map.SeaLevel, ["x0"] = map.X0, ["y0"] = map.Y0, ["cols"] = raster.Cols, ["rows"] = raster.Rows };
        }
        return body;
    }
}

