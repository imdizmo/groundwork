using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;
using static Groundwork.Desktop.Tests.SettingsDialogRun;
using static Groundwork.Desktop.Tests.SharedButtons;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A map project in the window: made (map.json beside its files), opened, its ground edited, taken back and redone,
/// saved and read again as it was left (its height and its paint); its sea level from map.json; a folder of the first
/// editor's files (no map.json) opened and given one on save. Runs alone (the map open is the app's).
/// </summary>
[Collection("DataDir")]
public sealed class MapProjectTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-map-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    /// <summary>The map put back, the test's maps deleted.</summary>
    public void Dispose()
    {
        MapProject.Current = _was;
        Brush.MapOpen = false;   // the paints named as the game's again
        Directory.Delete(_dir, recursive: true);
    }

    [AvaloniaFact]
    public async Task AMapOpensAndKeepsItsGroundThroughSave()
    {
        var map = MapProject.Create("Test", 1, generated: false, height: 2, seed: 1, under: _dir);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();

        await w.OpenMap(map.Folder);

        Assert.True(w.InMap);
        Assert.Equal(map.Folder, MapProject.Current?.Folder);
        Assert.Equal(map.Folder, w.Settings.Maps[0]);
        var s = w.Session!;
        var g = s.Ground;
        int p = (int)(((0 - g.OriginZ) * g.W) + (0 - g.OriginX));   // editor (0, 0), the map's middle
        Assert.Equal(2, s.Scene.Heights[p], 0.01f);
        _ = s.EditGround("Raise", ground =>
        {
            ground.SetHeight(p, 5);
            ground.StartPaint(p);
            ground.Paint[(p * 4) + 0] = 1;   // the second layer, nothing else
            ground.Paint[(p * 4) + 1] = ground.Paint[(p * 4) + 2] = 0;
            return ([p], (p % g.W, p / g.W, p % g.W, p / g.W));
        });
        Assert.True(w.MapDirty);
        Assert.True(s.Undo());
        Assert.Equal(2, s.Ground.HeightOf(p), 0.01f);
        Assert.True(s.Redo());

        Assert.True(await w.SaveMap(quiet: true));

        Assert.False(w.MapDirty);
        var again = MapProject.Open(map.Folder).Read();
        Assert.Equal(5, again.Heights[p], 0.01f);
        Assert.Equal((1f, 0f), (again.Session!.Ground.MaskOf(p, 0), again.Session.Ground.MaskOf(p, 2)));
    }

    // map.json: the name, the origin (the heightmap's pixel (0, 0) in engine metres), the files beside it; the sea
    // level it gives is the scene's water.
    [AvaloniaFact]
    public void ANewMapIsDescribedByItsMapJsonAndItsSeaLevelIsTheWater()
    {
        var made = MapProject.Create("Coast 2", 1, generated: false, height: 2, seed: 1, under: _dir);

        var j = JsonNode.Parse(File.ReadAllText(Path.Combine(made.Folder, "map.json")))!;
        Assert.Equal("Coast 2", j["name"]!.GetValue<string>());
        Assert.Equal([-32, -32], j["origin"]!.AsArray().Select(v => v!.GetValue<int>()));
        Assert.Equal("height.npy", j["files"]!["heights"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(made.Folder, "height.npy")));
        Assert.Equal(0, MapProject.Open(made.Folder).Read().Water);

        j["seaLevel"] = 1.5;
        File.WriteAllText(Path.Combine(made.Folder, "map.json"), j.ToJsonString());
        Assert.Equal(1.5f, MapProject.Open(made.Folder).Read().Water);
    }

    // What a map writes stays in its folder: a map.json naming a file elsewhere is refused (a map from someone else
    // must not write over other files); the model folder, only read, may be anywhere.
    [Theory]
    [InlineData("heights", "../elsewhere.npy")]
    [InlineData("objects", "/tmp/elsewhere.json")]
    public void AMapJsonNamingAFileOutsideItsFolderIsRefused(string file, string path)
    {
        var made = MapProject.Create("Far", 1, generated: false, height: 2, seed: 1, under: _dir);
        string json = Path.Combine(made.Folder, "map.json");
        var j = JsonNode.Parse(File.ReadAllText(json))!;
        j["models"] = "/somewhere/models";
        File.WriteAllText(json, j.ToJsonString());
        Assert.Equal("/somewhere/models", MapProject.Open(made.Folder).Models);

        j["files"]![file] = path;
        File.WriteAllText(json, j.ToJsonString());
        Assert.Throws<InvalidDataException>(() => MapProject.Open(made.Folder));
    }

    // Live's Unreal settings: the layers' texture (none: paint layers by name) and the water bodies' prefix (GW_Water_
    // when not given) are read from map.json and kept when the map is saved.
    [AvaloniaFact]
    public void TheUnrealLayersTextureAndWaterPrefixAreReadAndKept()
    {
        var made = MapProject.Create("Live", 1, generated: false, height: 2, seed: 1, under: _dir);
        string json = Path.Combine(made.Folder, "map.json");
        var j = JsonNode.Parse(File.ReadAllText(json))!;
        j["unreal"] = new JsonObject { ["project"] = _dir };
        File.WriteAllText(json, j.ToJsonString());
        var plain = MapProject.Open(made.Folder);
        Assert.Null(plain.UnrealLayersTexture);
        Assert.Equal("GW_Water_", plain.UnrealWaterPrefix);

        j["unreal"]!["layersTexture"] = "T_Ground_Mask";
        j["unreal"]!["waterPrefix"] = "My_Water_";
        File.WriteAllText(json, j.ToJsonString());
        var map = MapProject.Open(made.Folder);
        _ = map.Save(map.Read());
        var again = MapProject.Open(made.Folder);
        Assert.Equal("T_Ground_Mask", again.UnrealLayersTexture);
        Assert.Equal("My_Water_", again.UnrealWaterPrefix);
    }

    // Send to Unreal runs the script map.json names only once the user says yes to it for that map: Cancel runs
    // nothing; yes runs it and is remembered.
    [AvaloniaFact]
    public async Task AMapsScriptRunsOnlyOnceTheUserTrustsIt()
    {
        var made = MapProject.Create("Sent", 1, generated: false, height: 2, seed: 1, under: _dir);
        string ran = Path.Combine(_dir, "ran"), script = Path.Combine(made.Folder, "apply.sh");
        File.WriteAllText(script, $"cd \"$(dirname \"$0\")\" && touch '{ran}'\n");   // $0 is the script's path, as run by hand
        string json = Path.Combine(made.Folder, "map.json");
        var j = JsonNode.Parse(File.ReadAllText(json))!;
        j["unreal"] = new JsonObject { ["project"] = _dir, ["apply"] = "apply.sh" };
        File.WriteAllText(json, j.ToJsonString());
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenMap(made.Folder);

        var sent = w.SendToUnreal();
        Click(Buttons(TestWindows.Shown(w), "Cancel").Single());
        await sent;
        Assert.False(File.Exists(ran));
        Assert.Empty(w.Settings.TrustedScripts);

        sent = w.SendToUnreal();
        Click(Buttons(TestWindows.Shown(w), "Run it").Single());
        await sent;
        Assert.True(File.Exists(ran));
        Assert.StartsWith(made.Folder + "|" + script + "|", Assert.Single(w.Settings.TrustedScripts));

        // Changed since: asked again.
        File.Delete(ran);
        File.WriteAllText(script, $"touch '{ran}'; echo changed\n");
        sent = w.SendToUnreal();
        Click(Buttons(TestWindows.Shown(w), "Cancel").Single());
        await sent;
        Assert.False(File.Exists(ran));
    }

    // A link in a map's folder is no file of its own: a map.json naming one is refused (it would write through to
    // whatever the link points at).
    [AvaloniaFact]
    public void AMapWhoseFileIsALinkIsRefused()
    {
        var made = MapProject.Create("Linked", 1, generated: false, height: 2, seed: 1, under: _dir);
        string outside = Path.Combine(_dir, "outside.json");
        File.WriteAllText(outside, "[]");
        File.Delete(Path.Combine(made.Folder, "objects.json"));
        File.CreateSymbolicLink(Path.Combine(made.Folder, "objects.json"), outside);

        Assert.Throws<InvalidDataException>(() => MapProject.Open(made.Folder));
    }

    // map.json's layers: their names, colours ("#rrggbb", sRGB) and the layers file's channel each is read from; a pixel
    // painted in a layer's channel is that layer's paint, and saving writes it back there.
    [AvaloniaFact]
    public void ALayerIsReadFromItsChannelAndDrawnInItsColour()
    {
        var made = MapProject.Create("Layers", 1, generated: false, height: 2, seed: 1, under: _dir);
        string json = Path.Combine(made.Folder, "map.json");
        var j = JsonNode.Parse(File.ReadAllText(json))!;
        j["layers"] = JsonNode.Parse("""[{"name": "Moss", "colour": "#000000"}, {"name": "Snow", "colour": "#ffffff", "channel": "b"}]""");
        File.WriteAllText(json, j.ToJsonString());
        var map = MapProject.Open(made.Folder);
        Assert.Equal(["Moss", "Snow"], map.Layers.Select(l => l.Name));
        Assert.Equal(2, map.Layers[1].Channel);

        var scene = map.Read();
        int p = scene.W * (scene.H / 2) + (scene.W / 2);
        Assert.Equal(System.Numerics.Vector3.Zero, scene.GroundColor(p));
        _ = scene.Session!.EditGround("Snow", g =>
        {
            g.StartPaint(p);
            g.Paint[p * 4] = 1;
            return ([p], (p % g.W, p / g.W, p % g.W, p / g.W));
        });
        Assert.Equal(System.Numerics.Vector3.One, scene.GroundColor(p));
        Assert.Equal(1, scene.LayerAt(p));
        _ = map.Save(scene);
        Assert.Equal(1f, MapProject.Open(made.Folder).Read().Session!.Ground.MaskOf(p, 0));
        Assert.Equal("#ffffff", JsonNode.Parse(File.ReadAllText(json))!["layers"]![1]!["colour"]!.GetValue<string>());
    }

    // The objects file keeps what extensions keep on an object: an object of text by key after its scale, read back
    // as the object's data; an object with none is written without it.
    [AvaloniaFact]
    public void AnObjectsDataIsSavedWithItAndReadBack()
    {
        var made = MapProject.Create("Data", 1, generated: false, height: 2, seed: 1, under: _dir);
        var scene = made.Read();
        var s = scene.Session!;
        _ = s.Commit("Place", null, [],
        [
            (new NewObject(0, Groundwork.App.AssetIds.Of("Canal"), new System.Numerics.Vector3(1, 2, 3), System.Numerics.Vector3.Zero, 1, new Dictionary<string, string> { ["path"] = "0,0;10,0" }), false),
            (new NewObject(0, Groundwork.App.AssetIds.Of("Rock_4"), new System.Numerics.Vector3(4, 2, 4), System.Numerics.Vector3.Zero, 1), false),
        ]);
        Assert.Equal(2, made.Save(scene));

        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(made.Folder, "objects.json")))!.AsArray();
        Assert.Equal("0,0;10,0", file[0]![8]!["path"]!.GetValue<string>());
        Assert.Equal(8, file[1]!.AsArray().Count);
        var again = MapProject.Open(made.Folder).Read();
        var canal = again.Things.Single(t => Groundwork.App.AssetIds.NameOf(t.Prefab) == "Canal");
        Assert.Equal("0,0;10,0", again.Session!.Edits.FindAdded(canal.Id)!.Data!["path"]);
    }

    // Holes are saved in holes.png (written only once there is one) and come back; a grown map keeps them in place.
    [AvaloniaFact]
    public void HolesAreSavedReadBackAndGrowWithTheMap()
    {
        var made = MapProject.Create("Holes", 1, generated: false, height: 2, seed: 1, under: _dir);
        var scene = made.Read();
        _ = made.Save(scene);
        Assert.False(File.Exists(made.HolesFile));
        var g = scene.Session!.Ground;
        int p = (20 * g.W) + 20;
        _ = scene.Session.EditGround("Hole", gr =>
        {
            Sculpt.HolePoint(gr, p, 1, fill: false);
            return ([p], (19, 19, 21, 21));
        });
        _ = made.Save(scene);
        Assert.True(File.Exists(made.HolesFile));
        var again = MapProject.Open(made.Folder).Read();
        Assert.True(again.Session!.Ground.MaskOf(p, 3) > 0.5f);
        Assert.True(again.Session.Ground.MaskOf(p + 1, 3) < 0.5f);
        var world = (again.Session.Ground.OriginX + 20, again.Session.Ground.OriginZ + 20);

        var map = MapProject.Open(made.Folder);
        map.Grow(MapProject.Sides.West, 63, generated: false, flatHeight: 2, seed: 1);
        var grown = MapProject.Open(made.Folder).Read().Session!.Ground;
        int q = ((int)(world.Item2 - grown.OriginZ) * grown.W) + (int)(world.Item1 - grown.OriginX);
        Assert.True(grown.MaskOf(q, 3) > 0.5f);
    }
}
