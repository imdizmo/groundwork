using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The inbox (Inbox): a delivery's models go into the map's models folder and catalog, a resend replaces them, and
/// deliveries that are not whole, name a file outside them, are not glTF, need files beside the model or take another
/// model's name are refused
/// with nothing written.
/// </summary>
[Collection("DataDir")]
public sealed class InboxTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-inbox-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    public void Dispose()
    {
        MapProject.Current = _was;
        Brush.MapOpen = false;
        foreach (string d in Directory.GetDirectories(Inbox.Folder))
        {
            Directory.Delete(d, true);
        }
        Directory.Delete(_dir, true);
        string libraries = Path.Combine(AppSettings.DataDir, "libraries");
        if (Directory.Exists(libraries))
        {
            Directory.Delete(libraries, true);
        }
        Inbox.Announce(null);
    }

    private MapProject Map() => MapProject.Open(MapProject.Create("Sent", 1, generated: false, height: 2, seed: 1, under: _dir).Folder);

    /// <summary>A delivery as a sender writes it: in .id.partial, then renamed whole.</summary>
    private static void Send(string id, string json, params string[] glbs)
    {
        string partial = Path.Combine(Inbox.Folder, $".{id}.partial");
        Directory.CreateDirectory(partial);
        foreach (string g in glbs)
        {
            Glb(Path.Combine(partial, g));
        }
        File.WriteAllText(Path.Combine(partial, "send.json"), json);
        Directory.Move(partial, Path.Combine(Inbox.Folder, id));
    }

    private static void Glb(string file) =>
        GeneratedModels.WriteGlb(file, [([0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1], [0, 1, 2])], [(null, new(0.5f, 0.4f, 0.3f))], ["Stone"]);

    private static JsonObject Result(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(Inbox.Folder, ".results", id + ".json")))!.AsObject();

    [Fact]
    public void ADeliveryGoesIntoTheModelsFolderAndCatalogAndAResendReplacesIt()
    {
        var map = Map();
        Directory.CreateDirectory(Path.Combine(Inbox.Folder, ".half.partial"));   // not whole yet: left alone
        Send("t1", """{"version":1,"source":"Prop Maker","kinds":[{"file":"pm_rock.glb","model":"pm_rock","label":"Grey rock","category":"Rocks","scale":[0.8,1.2]},{"file":"pm_bush.glb","category":"Shrubs"}]}""", "pm_rock.glb", "pm_bush.glb");
        var taken = Inbox.TakeAll(map);
        var t = Assert.Single(taken);
        Assert.Null(t.Error);
        Assert.Equal(["pm_rock", "pm_bush"], t.Models);
        Assert.True(File.Exists(Path.Combine(map.Folder, "models", "Prop Maker", "pm_rock.glb")));
        Assert.False(Directory.Exists(Path.Combine(Inbox.Folder, "t1")));
        Assert.True(Directory.Exists(Path.Combine(Inbox.Folder, ".half.partial")));
        Assert.True(Result("t1")["ok"]!.GetValue<bool>());
        var kinds = JsonNode.Parse(File.ReadAllText(map.Catalog!))!["kinds"]!.AsArray();
        Assert.Equal("Rocks", kinds.Single(k => k!["model"]!.GetValue<string>() == "pm_rock")!["category"]!.GetValue<string>());
        Assert.Equal("Other", kinds.Single(k => k!["model"]!.GetValue<string>() == "pm_bush")!["category"]!.GetValue<string>());
        Assert.Equal("catalog.json", JsonNode.Parse(File.ReadAllText(Path.Combine(map.Folder, "map.json")))!["catalog"]!.GetValue<string>());

        Send("t2", """{"version":1,"source":"Prop Maker","kinds":[{"file":"pm_rock.glb","label":"Mossy rock","category":"Rocks"}]}""", "pm_rock.glb");
        Assert.Null(Assert.Single(Inbox.TakeAll(map)).Error);
        kinds = JsonNode.Parse(File.ReadAllText(map.Catalog!))!["kinds"]!.AsArray();
        Assert.Equal("Mossy rock", Assert.Single(kinds, k => k!["model"]!.GetValue<string>() == "pm_rock")!["label"]!.GetValue<string>());
    }

    [Fact]
    public void BadDeliveriesAreRefusedWithNothingWritten()
    {
        var map = Map();
        Send("other", """{"version":1,"source":"Other Tool","kinds":[{"file":"shared.glb"}]}""", "shared.glb");
        Assert.Null(Assert.Single(Inbox.TakeAll(map)).Error);

        Send("clash", """{"version":1,"source":"Prop Maker","kinds":[{"file":"shared.glb"}]}""", "shared.glb");
        Send("escape", """{"version":1,"source":"Prop Maker","kinds":[{"file":"../escape.glb"}]}""");
        Send("source", """{"version":1,"source":"../up","kinds":[{"file":"a.glb"}]}""", "a.glb");
        Send("version", """{"version":2,"source":"Prop Maker","kinds":[{"file":"a.glb"}]}""", "a.glb");
        Send("named", """{"version":1,"source":"Prop Maker","kinds":[{"file":"a.glb","model":"b"}]}""", "a.glb");
        string fake = Path.Combine(Inbox.Folder, ".fake.partial");
        Directory.CreateDirectory(fake);
        File.WriteAllText(Path.Combine(fake, "f.glb"), "not a model");
        File.WriteAllText(Path.Combine(fake, "send.json"), """{"version":1,"source":"Prop Maker","kinds":[{"file":"f.glb"}]}""");
        Directory.Move(fake, Path.Combine(Inbox.Folder, "fake"));

        // A model whose texture is a file beside it: it would be missing once copied.
        string outside = Path.Combine(Inbox.Folder, ".outside.partial");
        Directory.CreateDirectory(outside);
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""{"asset":{"version":"2.0"},"images":[{"uri":"../atlas.png"}]}  """);
        using (var w = new BinaryWriter(File.Create(Path.Combine(outside, "o.glb"))))
        {
            w.Write("glTF"u8);
            w.Write(2u);
            w.Write((uint)(20 + json.Length));
            w.Write((uint)json.Length);
            w.Write(0x4E4F534Au);
            w.Write(json);
        }
        File.WriteAllText(Path.Combine(outside, "send.json"), """{"version":1,"source":"Prop Maker","kinds":[{"file":"o.glb"}]}""");
        Directory.Move(outside, Path.Combine(Inbox.Folder, "outside"));

        var taken = Inbox.TakeAll(map);
        Assert.Equal(7, taken.Count);
        Assert.Contains("outside files", taken.Single(t => t.Id == "outside").Error);
        Assert.All(taken, t => Assert.NotNull(t.Error));
        Assert.All(taken, t => Assert.False(Result(t.Id)["ok"]!.GetValue<bool>()));
        Assert.False(Directory.Exists(Path.Combine(map.Folder, "models", "Prop Maker")));
        Assert.Empty(Inbox.Waiting());
    }

    // The Object tool in use when a model comes in takes the drawer's new kinds: the drawer hands it picks by index, and
    // a kind sent in ahead of the others once made it place the kind the old list had there.
    [AvaloniaFact]
    public async Task TheToolInUseTakesTheKindsSentIn()
    {
        var made = MapProject.Create("Sent", 1, generated: false, height: 2, seed: 1, under: _dir);
        Directory.CreateDirectory(Path.Combine(made.Folder, "models"));
        Glb(Path.Combine(made.Folder, "models", "b_rock.glb"));
        Glb(Path.Combine(made.Folder, "models", "c_rock.glb"));
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenMap(made.Folder);
        w.Tools.ChooseMod(w.Tools.ModTools.First(t => t.Info.Id == "object"));

        Send("whale", """{"version":1,"source":"Shelf","kinds":[{"file":"a_whale.glb","label":"Whale","category":"Props"}]}""", "a_whale.glb");
        // (Opening the map started a look in the inbox already: this one may only ask it to look again.)
        await w.TakeInbox();
        IEnumerable<string> Items() => w.Tools.ModTool!.Controls.OfType<Sdk.PaletteControl>().First().Items.Select(i => i.Name);
        for (int i = 0; i < 100 && Items().First() != "Whale"; i++)
        {
            await Task.Delay(50);
        }

        var items = Items();
        Assert.Equal(MapTools.Catalog().Kinds.Select(k => k.Label), items);
        Assert.Equal("Whale", items.First());
        w.Close();
    }

    // A sender that copied its Unreal assets into the map's Unreal project names them: names.json gets them (Live and
    // Send place those meshes); for another project they are left out with a warning; a collection is a biome; the
    // kind keeps its library id, and a library registered in the data folder is found by the sender's name.
    [Fact]
    public void UnrealMeshesCollectionsAndLibrariesComeWithADelivery()
    {
        var map = Map();
        string project = Path.Combine(_dir, "Game");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Game.uproject"), "{}");
        map.LinkUnreal(project);

        Inbox.Announce(map);
        var said = JsonNode.Parse(File.ReadAllText(Path.Combine(Inbox.Folder, ".map.json")))!;
        Assert.Equal(Path.Combine(project, "Game.uproject"), said["unrealProject"]!.GetValue<string>());

        string up = Path.Combine(project, "Game.uproject").Replace("\\", "\\\\", StringComparison.Ordinal);
        Send("u1", $$"""{"version":1,"source":"Shelf","unrealProject":"{{up}}","collection":"Shore","kinds":[{"file":"rock.glb","label":"Rock","shelf":"pack1/Rocks/SM_Rock","unreal":"/Game/Shelf/SM_Rock.SM_Rock"},{"file":"reed.glb"}]}""", "rock.glb", "reed.glb");
        var t = Assert.Single(Inbox.TakeAll(map));
        Assert.Null(t.Error);
        Assert.Empty(t.Warnings);
        var names = JsonNode.Parse(File.ReadAllText(Path.Combine(map.Folder, "models", "names.json")))!;
        Assert.Equal("/Game/Shelf/SM_Rock.SM_Rock", names["rock"]!.GetValue<string>());
        Assert.Null(names["reed"]);
        var catalog = JsonNode.Parse(File.ReadAllText(map.Catalog!))!;
        Assert.Equal(1.0, catalog["biomes"]!["Shore"]!["kinds"]!["reed"]!.GetValue<double>());
        var rock = catalog["kinds"]!.AsArray().Single(k => k!["model"]!.GetValue<string>() == "rock")!;
        Assert.Equal("pack1/Rocks/SM_Rock", rock["libraryId"]!.GetValue<string>());
        Assert.Equal("Shelf", rock["source"]!.GetValue<string>());

        Send("u2", """{"version":1,"source":"Shelf","unrealProject":"/elsewhere/Other.uproject","kinds":[{"file":"log.glb","unreal":"/Game/Shelf/SM_Log.SM_Log"}]}""", "log.glb");
        t = Assert.Single(Inbox.TakeAll(map));
        Assert.Null(t.Error);
        Assert.Contains("left out", Assert.Single(t.Warnings));
        Assert.Contains("warnings", File.ReadAllText(Path.Combine(Inbox.Folder, ".results", "u2.json")));
        Assert.Null(JsonNode.Parse(File.ReadAllText(Path.Combine(map.Folder, "models", "names.json")))!["log"]);

        Send("u3", """{"version":1,"source":"Shelf","kinds":[{"file":"bad.glb","unreal":"/Game/../x.x"}]}""", "bad.glb");
        Assert.NotNull(Assert.Single(Inbox.TakeAll(map)).Error);

        string libraries = Path.Combine(AppSettings.DataDir, "libraries");
        Directory.CreateDirectory(libraries);
        string exe = Path.Combine(_dir, "shelf-app");
        File.WriteAllText(exe, "");
        File.WriteAllText(Path.Combine(libraries, "shelf.json"), new JsonObject { ["version"] = 1, ["name"] = "Shelf", ["exe"] = exe }.ToJsonString());
        File.WriteAllText(Path.Combine(libraries, "loose.json"), """{"version":1,"name":"Loose","exe":"relative/app"}""");
        Assert.Equal(exe, Inbox.Library("Shelf"));
        Assert.Null(Inbox.Library("Loose"));
        // An id read back from a catalog that could be read as an option, or that hides a control character, is never passed on.
        Assert.NotNull(Inbox.Show(exe, "--help"));
        Assert.NotNull(Inbox.Show(exe, "a\nb"));

        Inbox.Announce(null);
        Assert.False(File.Exists(Path.Combine(Inbox.Folder, ".map.json")));
    }
}
