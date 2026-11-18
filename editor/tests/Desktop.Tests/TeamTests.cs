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
