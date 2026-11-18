using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The team server's client against a stand-in on this computer: the address as typed, who it says this is, a pack
/// published (a new map, then a version with If-Match), listed and taken back with its hash checked, and put in a folder.
/// </summary>
public sealed class TeamTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-team-").FullName;
    private readonly HttpListener _http = new();
    private readonly Uri _server;
    private byte[] _pack = [];
    private int _version;
    private string? _ifMatch;
    private bool _damaged;

    /// <summary>A play session as the team server keeps it (gzipped), and its id (the hash of those bytes in it).</summary>
    private static readonly byte[] PlaylogGz = Gzip("""{"playlog":1,"game":"test","build":"t1","map":{"name":"Isle","team":"m1","version":2},"player":"0a1b2c3d","started":"2026-11-18T01:40:00Z"}""" + "\n" + """{"t":4,"e":"death","p":[1,0,1],"by":"wolf"}""" + "\n");
    private static readonly string PlaylogIdOk = $"test/20261118-013743-0a1b2c3d-{Convert.ToHexStringLower(SHA256.HashData(PlaylogGz))[..8]}.playlog.gz";

    private static byte[] Gzip(string text)
    {
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
        {
            z.Write(Encoding.UTF8.GetBytes(text));
        }
        return ms.ToArray();
    }

    public TeamTests()
    {
        int port = 0;
        for (int tries = 0; ; tries++)
        {
            port = Random.Shared.Next(20000, 60000);
            _http.Prefixes.Clear();
            _http.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _http.Start();
                break;
            }
            catch (HttpListenerException) when (tries < 20)
            {
            }
        }
        _server = new Uri($"http://127.0.0.1:{port}/");
        _ = Task.Run(Serve);
    }

    public void Dispose()
    {
        _http.Close();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task Serve()
    {
        while (_http.IsListening)
        {
            HttpListenerContext c;
            try
            {
                c = await _http.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }
            string path = c.Request.Url!.AbsolutePath;
            byte[] body = [];
            string Row() => new JsonObject { ["id"] = "m1", ["name"] = "Isle", ["version"] = _version, ["size"] = _pack.Length, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(_pack)), ["publishedBy"] = new JsonObject { ["login"] = "a@b", ["name"] = "Ana" }, ["publishedAt"] = "2026-11-16T20:00:00Z" }.ToJsonString();
            if (path == "/team/whoami")
            {
                body = Encoding.UTF8.GetBytes("""{"login":"a@b","name":"Ana","role":"member","service":"shelf","version":"0.1.0"}""");
            }
            else if (path == "/maps/m1" && c.Request.HttpMethod == "PUT")
            {
                _ifMatch = c.Request.Headers["If-Match"];
                using var ms = new MemoryStream();
                await c.Request.InputStream.CopyToAsync(ms);
                _pack = ms.ToArray();
                _version++;
                c.Response.StatusCode = _version == 1 ? 201 : 200;
                body = Encoding.UTF8.GetBytes(Row());
            }
            else if (path == "/team/crashes" && c.Request.HttpMethod == "POST")
            {
                // as the team server from 0.6.0: a report must say it is a zip (a web page cannot post one in a teammate's name)
                bool zip = c.Request.ContentType?.StartsWith("application/zip", StringComparison.Ordinal) == true;
                c.Response.StatusCode = zip ? 201 : 415;
                body = Encoding.UTF8.GetBytes(zip ? """{"id":"c1"}""" : """{"error":"A crash report is a zip."}""");
            }
            else if (path == "/team/playlogs")
            {
                string Pl(string id, string team) => new JsonObject { ["id"] = id, ["game"] = "test", ["map"] = new JsonObject { ["name"] = "Isle", ["team"] = team, ["version"] = 2 }, ["player"] = "0a1b2c3d", ["started"] = "2026-11-18T01:40:00Z", ["length"] = 4, ["ended"] = null, ["counts"] = new JsonObject { ["death"] = 1 }, ["size"] = PlaylogGz.Length }.ToJsonString();
                body = Encoding.UTF8.GetBytes($"[{Pl(PlaylogIdOk, "m1")},{Pl("../maps/m1/pack", "m1")},{Pl("test/20261118-013743-0a1b2c3d-00000000.playlog.gz", "other")}]");
            }
            else if (path == "/team/playlogs/" + PlaylogIdOk)
            {
                body = _damaged ? [.. PlaylogGz[..^1], 1] : PlaylogGz;   // the last byte (the size's highest) is 0 in a small file
            }
            else if (path == "/maps")
            {
                body = Encoding.UTF8.GetBytes($"[{Row()}]");
            }
            else if (path == "/maps/m1/pack")
            {
                c.Response.Headers["ETag"] = $"\"{Convert.ToHexStringLower(SHA256.HashData(_pack))}\"";
                body = _damaged ? [.. _pack, 0] : _pack;
            }
            else
            {
                c.Response.StatusCode = 404;
                body = Encoding.UTF8.GetBytes("""{"error":"No such thing here."}""");
            }
            c.Response.ContentLength64 = body.Length;
            await c.Response.OutputStream.WriteAsync(body);
            c.Response.Close();
        }
    }

    [Fact]
    public void TheAddressIsAHostWithThePortAddedWhenNoneIsGiven()
    {
        Assert.Equal(new Uri($"http://100.64.0.1:{Team.DefaultPort}/"), Team.Base("100.64.0.1"));
        Assert.Equal(new Uri("http://nas.tail:9000/"), Team.Base("nas.tail:9000"));
        Assert.Null(Team.Base("http://nas"));
        Assert.Null(Team.Base("nas/path"));
        Assert.Null(Team.Base("a@nas"));
        var s = new Groundwork.App.AppSettings { Team = "" };
        Assert.Null(Team.Address(s));   // saved empty: offline, whatever the Launcher says
        Assert.True(Team.PlainId("0f3a_b-C"));
        Assert.False(Team.PlainId("../team/whoami"));
        Assert.False(Team.PlainId("a?b"));
        Assert.False(Team.PlainId(new string('a', 65)));
    }

    [Fact]
    public async Task ThePlaySessionsOfTheMapComeDownCheckedAndUnpacked()
    {
        var (rows, error) = await Team.Playlogs(_server);
        Assert.Null(error);
        // the row whose id the server never makes is dropped
        Assert.Equal(2, rows!.Count);
        Assert.Equal((1, 4f, "", 2), (rows[0].Deaths, rows[0].Length, rows[0].Ended, rows[0].MapVersion));
        var map = MapProject.Create("Isle", 1, generated: false, height: 2, seed: 1, under: _dir);
        map.SetTeam("m1", 2);
        var mine = Assert.Single(MainWindow.TeamPlaylogsOf(rows, map));
        Assert.Equal(PlaylogIdOk, mine.Id);

        string file = Path.Combine(_dir, "got.playlog");
        Assert.Null(await Team.DownloadPlaylog(_server, PlaylogIdOk, file));
        var log = PlayLog.Read(file).Log!;
        Assert.Equal(("Isle", "wolf"), (log.MapName, log.Events[0].Text("by")));

        _damaged = true;
        Assert.Contains("damaged", await Team.DownloadPlaylog(_server, PlaylogIdOk, Path.Combine(_dir, "bad.playlog")));
        Assert.False(File.Exists(Path.Combine(_dir, "bad.playlog")));
        _damaged = false;
        Assert.Contains("not one the team server makes", await Team.DownloadPlaylog(_server, "../maps/m1/pack", file));
        Assert.False(Team.PlaylogId("test/20261118-013743-0a1b2c3d-00000000.playlog.gz/../x"));
        Assert.False(Team.PlaylogId("Test/20261118-013743-0a1b2c3d-00000000.playlog.gz"));
    }

    [Fact]
    public async Task ACrashReportGoesUpAsAZip()
    {
        var (id, error) = await Team.SendCrash(_server, "0.0.0-test", [0x50, 0x4b, 5, 6]);
        Assert.Null(error);
        Assert.Equal("c1", id);
    }

    [Fact]
    public async Task AMapGoesUpComesBackAndOpensInAFolderOfItsOwn()
    {
        var (who, error) = await Team.WhoAmI(_server);
        Assert.Null(error);
        Assert.Equal(("a@b", "member"), (who!.Login, who.Role));

        var made = MapProject.Create("Isle", 1, generated: false, height: 2, seed: 1, under: _dir);
        string zip = Path.Combine(_dir, "up.zip");
        MapPack.Write(MapProject.Open(made.Folder), zip, withModels: true);
        var (row, failed) = await Team.Publish(_server, "m1", "Isle", zip, had: 0);
        Assert.Null(failed);
        Assert.Equal(1, row!.Version);
        Assert.Null(_ifMatch);
        (row, _) = await Team.Publish(_server, "m1", "Isle", zip, had: 1);
        Assert.Equal((2, "\"1\""), (row!.Version, _ifMatch));

        var (maps, _) = await Team.Maps(_server);
        Assert.Equal(("m1", "Isle", "Ana"), (maps![0].Id, maps[0].Name, maps[0].By));

        string under = Directory.CreateDirectory(Path.Combine(_dir, "mine")).FullName;
        Directory.CreateDirectory(Path.Combine(under, "Isle"));   // taken: the map goes in "Isle 2"
        string down = Path.Combine(_dir, "down.zip");
        Assert.Null(await Team.Download(_server, "m1", down));
        string folder = MainWindow.Unpack(down, Directory.CreateDirectory(Path.Combine(_dir, "t")).FullName, under, "Isle");
        Assert.Equal(Path.Combine(under, "Isle 2"), folder);
        Assert.True(File.Exists(Path.Combine(folder, MapProject.FileName)));

        Assert.NotNull(await Team.Download(_server, "../maps", down));
        Assert.NotNull((await Team.Publish(_server, "m1/../x", "Isle", zip, 0)).Error);
        _damaged = true;
        Assert.NotNull(await Team.Download(_server, "m1", down));
        var (none, why) = await Team.Maps(new Uri(_server, "nowhere/"));
        Assert.Null(none);
        Assert.Equal("No such thing here.", why);
    }
}
