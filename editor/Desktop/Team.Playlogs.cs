using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Groundwork.Desktop;

/// <summary>The play sessions the team server keeps (.playlog files a game sent): listed, and taken down to replay.</summary>
public static partial class Team
{
    /// <summary>A play session on the team server, as listed.</summary>
    /// <param name="Id">Its id (game/received-player-hash.playlog.gz).</param>
    /// <param name="Game">The game that wrote it.</param>
    /// <param name="MapName">The map it was played on ("" when the log names none).</param>
    /// <param name="MapTeam">That map's team id, or "".</param>
    /// <param name="MapVersion">That map's version, or 0.</param>
    /// <param name="Player">The random player id the game keeps.</param>
    /// <param name="Started">When it began (the game's clock, as written).</param>
    /// <param name="Length">Seconds.</param>
    /// <param name="Ended">How it ended ("" when it has no end line).</param>
    /// <param name="Deaths">How many times the player died.</param>
    /// <param name="Size">The packed file's bytes.</param>
    public sealed record PlaylogRow(string Id, string Game, string MapName, string MapTeam, int MapVersion, string Player, string Started, float Length, string Ended, int Deaths, long Size);

    /// <summary>Most unpacked bytes taken from one session (PlayLog.MostBytes).</summary>
    private const long MostPlaylogBytes = PlayLog.MostBytes;

    /// <summary>A play session's id as the server makes it: the game, then its own time, the player and 8 hex of the file's SHA-256.</summary>
    [GeneratedRegex(@"^[a-z0-9-]{1,32}/[0-9]{8}-[0-9]{6}-[0-9a-f]{8}-([0-9a-f]{8})\.playlog\.gz$")]
    private static partial Regex PlaylogIdPattern();

    /// <summary>Whether an id is one the server makes (it goes into the path, so nothing else is used).</summary>
    public static bool PlaylogId(string? id) => id != null && PlaylogIdPattern().IsMatch(id);

    /// <summary>The play sessions the team keeps, newest first.</summary>
    public static async Task<(List<PlaylogRow>? Rows, string? Error)> Playlogs(Uri server)
    {
        var (got, error) = await Call<(List<PlaylogRow>?, string?)>(server, "listing the team's play sessions", async () =>
        {
            using var r = await Http.GetAsync(new Uri(server, "team/playlogs"));
            if (!r.IsSuccessStatusCode)
            {
                return (null, await Error(r));
            }
            var rows = (JsonNode.Parse(await r.Content.ReadAsStringAsync()) as JsonArray ?? []).OfType<JsonObject>().Select(o => new PlaylogRow(
                S(o["id"]), S(o["game"]), S(o["map"]?["name"]), S(o["map"]?["team"]), (int)N(o["map"]?["version"]), S(o["player"]), S(o["started"]),
                (float)N(o["length"]), S(o["ended"]), (int)N(o["counts"]?["death"]), (long)N(o["size"]))).Where(p => PlaylogId(p.Id)).ToList();
            return (rows, null);
        });
        return (got.Item1, error ?? got.Item2);
    }

    /// <summary>A number in a JSON answer, or 0.</summary>
    private static double N(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) && double.IsFinite(d) ? d : 0;

    /// <summary>
    /// A play session taken down and unpacked into <paramref name="file"/> (a .playlog), its bytes checked against the
    /// hash its id holds; the error, or null.
    /// </summary>
    public static async Task<string?> DownloadPlaylog(Uri server, string id, string file)
    {
        if (!PlaylogId(id))
        {
            return "The play session's id is not one the team server makes.";
        }
        var (got, error) = await Call(server, "taking the play session", async () =>
        {
            // the id's parts are checked above: '/' is a path separator here, as the server reads it
            using var r = await Big.GetAsync(new Uri(server, $"team/playlogs/{id}"), HttpCompletionOption.ResponseHeadersRead);
            if (!r.IsSuccessStatusCode)
            {
                return await Error(r);
            }
            byte[] gz;
            await using (var body = await r.Content.ReadAsStreamAsync())
            using (var ms = new MemoryStream())
            {
                // a sent log is at most 20 MB packed
                byte[] buf = new byte[81920];
                for (int n; (n = await body.ReadAsync(buf)) > 0;)
                {
                    if (ms.Length + n > 32L << 20)
                    {
                        return "The play session is bigger than a sent one can be.";
                    }
                    ms.Write(buf, 0, n);
                }
                gz = ms.ToArray();
            }
            if (!Convert.ToHexStringLower(SHA256.HashData(gz)).StartsWith(PlaylogIdPattern().Match(id).Groups[1].Value, StringComparison.Ordinal))
            {
                return "The play session came down damaged (its hash is not the one its id holds); try again.";
            }
            string temp = file + ".tmp";
            try
            {
                await using (var from = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress))
                await using (var to = File.Create(temp))
                {
                    byte[] buf = new byte[81920];
                    for (int n; (n = await from.ReadAsync(buf)) > 0;)
                    {
                        if (to.Length + n > MostPlaylogBytes)
                        {
                            return $"The play session unpacks to more than {MostPlaylogBytes >> 20} MB.";
                        }
                        await to.WriteAsync(buf.AsMemory(0, n));
                    }
                }
                File.Move(temp, file, overwrite: true);
                return null;
            }
            catch (InvalidDataException)
            {
                return "The play session is not a gzip file.";
            }
            finally
            {
                File.Delete(temp);
            }
        });
        return error ?? got;
    }
}
