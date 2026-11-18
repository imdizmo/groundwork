using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The team server: its address (Settings, else the Launcher's JATIFY_TEAM; none: offline) and who it says this
/// person is (GET /team/whoami, answered from their Tailscale account). Plain http: Tailscale encrypts the way.
/// </summary>
public static partial class Team
{
    /// <summary>The port when the address names none.</summary>
    // the team's library service (Questlog has 8780)
    public const int DefaultPort = 8781;

    /// <summary>The variable the Launcher hands the address in.</summary>
    public const string Variable = "JATIFY_TEAM";

    /// <summary>Who the server says is connected: Tailscale login and name, role on the team (null: not on it).</summary>
    public sealed record Who(string Login, string Name, string? Role, string Service, string Version);

    /// <summary>A map kept by the team: its id, name, newest version, size, hash and who published it when.</summary>
    public sealed record MapRow(string Id, string Name, int Version, long Size, string Sha256, string By, string At);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>For packs: up to an hour (a map with its models is hundreds of MB).</summary>
    private static readonly HttpClient Big = new() { Timeout = TimeSpan.FromHours(1) };

    private static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    private static MapRow Row(JsonNode? j) => new(
        S(j?["id"]), S(j?["name"]), j?["version"]?.GetValue<int>() ?? 0, j?["size"]?.GetValue<long>() ?? 0, S(j?["sha256"]),
        S(j?["publishedBy"]?["name"]) is { Length: > 0 } n ? n : S(j?["publishedBy"]?["login"]), S(j?["publishedAt"]));

    /// <summary>The server's error as a sentence: its {error}, else the status.</summary>
    private static async Task<string> Error(HttpResponseMessage r)
    {
        try
        {
            if (S((JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonObject)?["error"]) is { Length: > 0 } e)
            {
                return e;
            }
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or NotSupportedException or HttpRequestException)
        {
        }
        return $"The team server answered {(int)r.StatusCode} {r.ReasonPhrase}.";
    }

    /// <summary>A call to the server, its failures as a sentence.</summary>
    private static async Task<(T? Value, string? Error)> Call<T>(Uri server, string what, Func<Task<T>> call)
    {
        try
        {
            return (await call(), null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException or InvalidOperationException or FormatException)
        {
            Log.CaughtBrief(e, what);
            return (default, $"The team server at {server.Authority} cannot be reached ({e.Message}).");
        }
    }

    /// <summary>The maps the team keeps, the newest version of each.</summary>
    public static async Task<(List<MapRow>? Maps, string? Error)> Maps(Uri server)
    {
        var (got, error) = await Call(server, "listing the team's maps", async () =>
        {
            using var r = await Http.GetAsync(new Uri(server, "maps"));
            return r.IsSuccessStatusCode ? ((JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonArray ?? []).Select(Row).ToList(), (string?)null) : (null, await Error(r));
        });
        return (got.Item1, error ?? got.Item2);
    }

    /// <summary>A packed map up to the team: a new map, or a new version of the one this copy is (refused when someone published since).</summary>
    /// <param name="server">The team server.</param>
    /// <param name="id">The map's id on the team.</param>
    /// <param name="name">Its name.</param>
    /// <param name="zip">The pack (MapPack).</param>
    /// <param name="had">The version this copy is (0: a new map).</param>
    public static async Task<(MapRow? Map, string? Error)> Publish(Uri server, string id, string name, string zip, int had)
    {
        if (!PlainId(id))
        {
            return (null, "The map's team id is not a plain one.");
        }
        var (got, error) = await Call(server, "publishing the map", async () =>
        {
            await using var file = File.OpenRead(zip);
            using var body = new StreamContent(file);
            body.Headers.ContentType = new("application/zip");
            body.Headers.ContentLength = file.Length;
            using var put = new HttpRequestMessage(HttpMethod.Put, new Uri(server, $"maps/{id}?name={Uri.EscapeDataString(name)}")) { Content = body };
            if (had > 0)
            {
                put.Headers.TryAddWithoutValidation("If-Match", $"\"{had}\"");
            }
            using var r = await Big.SendAsync(put);
            return r.IsSuccessStatusCode ? (Row(JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonObject), (string?)null) : (null, await Error(r));
        });
        return (got.Item1, error ?? got.Item2);
    }

    /// <summary>A map's newest pack down into a file, its hash checked against the server's; null when done, else why not.</summary>
    public static async Task<string?> Download(Uri server, string id, string zip)
    {
        if (!PlainId(id))
        {
            return "The team's map has an id that is not a plain one.";
        }
        var (got, error) = await Call(server, "taking the team's map", async () =>
        {
            using var r = await Big.GetAsync(new Uri(server, $"maps/{Uri.EscapeDataString(id)}/pack"), HttpCompletionOption.ResponseHeadersRead);
            if (!r.IsSuccessStatusCode)
            {
                return await Error(r);
            }
            await using (var to = File.Create(zip))
            {
                await (await r.Content.ReadAsStreamAsync()).CopyToAsync(to);
            }
            string? want = r.Headers.ETag?.Tag.Trim('"');
            await using var read = File.OpenRead(zip);
            string have = Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(read));
            return want is { Length: 64 } && !want.Equals(have, StringComparison.OrdinalIgnoreCase) ? "The map came down damaged (its hash is not the server's); try again." : null;
        });
        return error ?? got;
    }

    /// <summary>A map's id as the server takes it: 1 to 64 of A-Z, a-z, 0-9, '_' and '-' (it is a part of the path).</summary>
    public static bool PlainId(string? id) => id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    /// <summary>The Launcher's address (empty: none).</summary>
    public static string Launcher => Environment.GetEnvironmentVariable(Variable)?.Trim() ?? "";

    /// <summary>The address used: the one saved in Settings, else the Launcher's; null: offline.</summary>
    public static string? Address(AppSettings s) => (s.Team ?? Launcher).Trim() is { Length: > 0 } a ? a : null;

    /// <summary>The server's base (http, the default port added when none is given); null when not an address.</summary>
    public static Uri? Base(string address) =>
        address.Contains("://", StringComparison.Ordinal) || !Uri.TryCreate("http://" + address, UriKind.Absolute, out var u)
            || u.AbsolutePath != "/" || u.Query.Length > 0 || u.UserInfo.Length > 0
            ? null
            : new UriBuilder(u) { Port = address.EndsWith($":{u.Port}", StringComparison.Ordinal) ? u.Port : DefaultPort }.Uri;

    /// <summary>Asks the server who this is: the answer, or a sentence saying why there is none.</summary>
    public static async Task<(Who? Who, string? Error)> WhoAmI(Uri server)
    {
        try
        {
            using var r = await Http.GetAsync(new Uri(server, "team/whoami"));
            if (!r.IsSuccessStatusCode)
            {
                return (null, $"The team server answered {(int)r.StatusCode} {r.ReasonPhrase}.");
            }
            var j = JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonObject;
            return S(j?["login"]) is { Length: > 0 } login
                ? (new Who(login, S(j!["name"]), S(j["role"]) is { Length: > 0 } role ? role : null, S(j["service"]), S(j["version"])), null)
                : (null, "The team server's answer names nobody.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            Log.CaughtBrief(e, "asking the team server who this is");
            return (null, $"The team server at {server.Authority} cannot be reached ({e.Message}).");
        }
    }

    /// <summary>A crash report (a zip) to the team: its id there, or why it was not taken.</summary>
    public static async Task<(string? Id, string? Error)> SendCrash(Uri server, string version, byte[] zip)
    {
        string os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        var (got, error) = await Call(server, "sending the crash report", async () =>
        {
            using var body = new ByteArrayContent(zip);
            body.Headers.ContentType = new("application/zip");
            using var r = await Big.PostAsync(new Uri(server, $"team/crashes?tool=groundwork&version={Uri.EscapeDataString(version)}&os={os}"), body);
            return r.IsSuccessStatusCode ? (S((JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonObject)?["id"]), (string?)null) : (null, await Error(r));
        });
        return (got.Item1, error ?? got.Item2);
    }

    /// <summary>The answer as a line for a person.</summary>
    public static string Said(Who w) =>
        $"Connected as {(w.Name.Length > 0 ? $"{w.Name} ({w.Login})" : w.Login)}" + (w.Role is { } r ? $", {r} on the team." : ", not on the team yet: ask its owner to add you.");
}
