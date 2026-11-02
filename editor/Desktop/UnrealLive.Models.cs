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
}
