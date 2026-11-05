using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A map of more than four layers: the fifth on painted with their brushes into a second layers file, saved and read
/// again; the hole keeps its channel; a map of four layers keeps its one file.
/// </summary>
[Collection("DataDir")]
public sealed class MoreLayersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-layers-").FullName;
    private readonly MapProject? _was = MapProject.Current;
    private static readonly string[] Six = ["Grass", "Sand", "Dirt", "Rock", "Moss", "Snow"];

    /// <summary>The map put back, the test's maps deleted.</summary>
    public void Dispose()
    {
        MapProject.Current = _was;
        Directory.Delete(_dir, recursive: true);
    }

    [AvaloniaFact]
    public void TheFifthAndSixthLayersArePaintedSavedAndReadAgain()
    {
        var made = MapProject.Create("Six", 1, generated: false, height: 2, seed: 1, under: _dir);
        string json = Path.Combine(made.Folder, "map.json");
        var j = JsonNode.Parse(File.ReadAllText(json))!;
        j["layers"] = new JsonArray([.. Six.Select(n => (JsonNode)new JsonObject { ["name"] = n, ["colour"] = "#808080" })]);
        File.WriteAllText(json, j.ToJsonString());

        var map = MapProject.Open(made.Folder);
        Assert.Equal(["r", "g", "b", "r2", "g2"], map.Layers.Skip(1).Select(l => MapProject.ChannelName(l.Channel)));
        var s = map.Read();
        var g = s.Session!.Ground;
        Assert.Equal(6, g.C);
        int p = (int)(((0 - g.OriginZ) * g.W) + (0 - g.OriginX)), q = p + 1;   // the map's middle, and the point east of it
        _ = s.Session.EditGround("Paint", ground =>
        {
            Sculpt.PaintPoint(ground, p, Brush.PaintOf(BrushTool.PaintLayer5)!, 1);
            Sculpt.PaintPoint(ground, q, Brush.PaintOf(BrushTool.PaintLayer6)!, 1);
            Sculpt.HolePoint(ground, q, 1, fill: false);
            return ([p, q], (p % g.W, p / g.W, q % g.W, q / g.W));
        });
        Assert.Equal(4, s.LayerAt(p));
        Assert.Equal(5, s.LayerAt(q));
        _ = map.Save(s);

        Assert.True(File.Exists(Path.Combine(made.Folder, "layers2.png")));
        Assert.Equal(2, JsonNode.Parse(File.ReadAllText(json))!["files"]!["layers"]!.AsArray().Count);
        var again = MapProject.Open(made.Folder);
        var ag = again.Read().Session!.Ground;
        Assert.Equal((1f, 0f, 0f), (ag.MaskOf(p, Ground.ChannelOf(4)), ag.MaskOf(p, Ground.ChannelOf(5)), ag.MaskOf(p, Ground.ChannelOf(1))));
        Assert.Equal((1f, 1f), (ag.MaskOf(q, Ground.ChannelOf(5)), ag.MaskOf(q, Ground.HoleChannel)));
        var (weights, cols, _) = again.LayersNow(again.Read());
        var raster = again.ReadRaster();
        var (col, row) = raster.Pixel(0, 0);
        Assert.Equal((0, 255, 0), (weights[0][(row * cols) + col], weights[4][(row * cols) + col], weights[5][(row * cols) + col]));
    }

    [AvaloniaFact]
    public void AMapOfFourLayersKeepsOneLayersFile()
    {
        var made = MapProject.Create("Four", 1, generated: false, height: 2, seed: 1, under: _dir);
        var map = MapProject.Open(made.Folder);
        var s = map.Read();
        Assert.Equal(4, s.Session!.Ground.C);
        _ = map.Save(s);

        Assert.False(File.Exists(Path.Combine(made.Folder, "layers2.png")));
        Assert.Equal("layers.png", JsonNode.Parse(File.ReadAllText(Path.Combine(made.Folder, "map.json")))!["files"]!["layers"]!.GetValue<string>());
    }
}
