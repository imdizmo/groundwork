using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The map's layers for Unreal's landscape: each layer's weight per map pixel as it is now, and the material asked for.</summary>
public sealed class UnrealLayersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-ulayers-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [AvaloniaFact]
    public async Task LayerWeightsFollowThePaintAndTheGroundLayerTakesTheRest()
    {
        var map = MapProject.Create("Layers", 2, generated: false, height: 10, seed: 1, under: _dir);
        var s = map.Read().Session!;
        var t = new ClaudeTools(TestWindows.Editing(s));
        await t.MapPath([[-20, 0], [20, 0]], layer: "Dirt");
        var (weights, cols, rows) = map.LayersNow(s.Scene);
        Assert.Equal(map.Layers.Count, weights.Length);
        var (col, row) = map.ReadRaster().Pixel(0, 0);
        int px = (row * cols) + col, dirt = map.Layers.FindIndex(l => l.Name == "Dirt");
        Assert.True(weights[dirt][px] > 230, $"dirt {weights[dirt][px]}");
        Assert.True(weights[0][px] < 25, $"ground {weights[0][px]}");
        Assert.Equal(255, weights[0][((rows - 2) * cols) + 2]);   // untouched ground: all the first layer
        Assert.All(Enumerable.Range(0, cols * rows).Where(i => i % 997 == 0), i => Assert.InRange(weights.Sum(w => w[i]), 250, 260));

        var body = UnrealLive.LayerMaterialBody(map);
        Assert.Equal("Layers", body["name"]!.GetValue<string>());
        Assert.Equal(map.Layers.Count, body["layers"]!.AsArray().Count);
        Assert.Equal(3, body["layers"]![0]!["colour"]!.AsArray().Count);
    }

    [AvaloniaFact]
    public void TheWaterSentHasTheMapsSeaUnlessItHasNone()
    {
        var map = MapProject.Create("Sea", 2, generated: false, height: 10, seed: 1, under: _dir);
        _ = map.Read();
        var body = UnrealLive.WaterBody(map);
        Assert.Equal(map.UnrealWaterPrefix, body["prefix"]!.GetValue<string>());
        Assert.Equal(map.SeaLevel, body["sea"]!["level"]!.GetValue<float>());
        Assert.Equal(map.ReadRaster().Cols, body["sea"]!["cols"]!.GetValue<int>());
        map.SeaLevel = -1000;
        Assert.Null(UnrealLive.WaterBody(map)["sea"]);
    }
}

