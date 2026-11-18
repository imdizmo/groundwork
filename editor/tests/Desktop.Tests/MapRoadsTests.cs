using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Kept roads: their lines, settings and the ground before them come back from their files; where they cross rivers.</summary>
[Collection("MapAreas")]
public class MapRoadsTests
{
    [Fact]
    public void ARoadAndItsGroundBeforeComeBackFromItsFiles()
    {
        string dir = Directory.CreateTempSubdirectory("gw-roads-").FullName;
        string file = Path.Combine(dir, "roads.json");
        MapRoads.All.Clear();
        MapRoads.Save(file, Path.ChangeExtension(file, ".bin"));
        Assert.False(File.Exists(file));   // no roads, no file

        var before = new Dictionary<int, MapRoads.Point> { [12] = new(3.5f, [0.25f, 0.75f]), [13] = new(-1, [1, 0]) };
        MapRoads.All.Add(new MapRoads.Road("Road 1", [new(0, 0), new(10, 5)], 4, 2, false, 12, "Sand", "Post", 3, 0.5f, false, "Bridge") { Before = before, Grid = (64, 64, 0, 0) });
        MapRoads.All.Add(new MapRoads.Road("Road 2", [new(5, 5), new(6, 9)], 3, 3, true, 15, null, null, 3, 0.5f, true, null));
        MapRoads.Save(file, Path.ChangeExtension(file, ".bin"));
        MapRoads.All.Clear();
        MapRoads.Load(file, Path.ChangeExtension(file, ".bin"));
        Directory.Delete(dir, recursive: true);

        Assert.Equal(["Road 1", "Road 2"], MapRoads.All.Select(r => r.Name));
        var r1 = MapRoads.All[0];
        Assert.Equal((4f, 2f, false, "Sand", "Post", false, "Bridge"), (r1.Width, r1.Shoulder, r1.Even, r1.Layer, r1.Edge, r1.BothSides, r1.Bridge));
        Assert.Equal((64, 64, 0, 0), r1.Grid);
        Assert.Equal(3.5f, r1.Before![12].Height);
        Assert.Equal([0.25f, 0.75f], r1.Before[12].Paint);
        Assert.Null(MapRoads.All[1].Before);
        Assert.Equal(MathF.Sqrt(125), r1.Length, 3);
        MapRoads.All.Clear();
    }

    [Fact]
    public void ARoadCrossingARiverIsFoundWithTheRiversWidth()
    {
        MapWater.Rivers.Clear();
        var river = new MapWater.River();
        // Unreal metres: Y = -z, so this river runs along z = 10, x from -50 to 50
        river.Points.AddRange([new MapWater.Point(-50, -10, 2.5f, 1, 0.5f), new MapWater.Point(50, -10, 3.5f, 1, 0.5f)]);
        MapWater.Rivers.Add(river);
        var (At, Yaw, HalfWidth, _, _) = Assert.Single(MapRoads.Crossings([new(0, 0), new(0, 20)]));
        Assert.Equal(new Vector2(0, 10), At);
        Assert.Equal((0f, 3.5f), (Yaw, HalfWidth));   // heading north
        Assert.Empty(MapRoads.Crossings([new(0, 0), new(20, 0)]));
        MapWater.Rivers.Clear();
    }

    [Fact]
    public void TheRoadsGroundFileIsNeitherALinkNorAnotherOfTheMapsFiles()
    {
        string dir = Directory.CreateTempSubdirectory("gw-roadmap-").FullName;
        try
        {
            var map = MapProject.Create("Isle", 1, generated: false, height: 2, seed: 1, under: dir);
            string json = Path.Combine(map.Folder, MapProject.FileName);
            Assert.EndsWith("roads.bin", MapProject.Open(map.Folder).RoadsBeforeFile);

            // a roads file named so its .bin is the beds file
            var o = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(json))!.AsObject();
            o["files"]!["roads"] = "beds.json";
            File.WriteAllText(json, o.ToJsonString());
            Assert.Throws<InvalidDataException>(() => MapProject.Open(map.Folder));

            // a link where the ground file goes
            o["files"]!["roads"] = "roads.json";
            File.WriteAllText(json, o.ToJsonString());
            File.CreateSymbolicLink(Path.Combine(map.Folder, "roads.bin"), Path.Combine(dir, "elsewhere.bin"));
            Assert.Throws<InvalidDataException>(() => MapProject.Open(map.Folder));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void APaintedStrokeKeepsItsBendsAsDotsAndADotAtLeastEverySoOften()
    {
        // an L: 100 m east, then 50 m north, a point every metre with a little wobble
        var stroke = Enumerable.Range(0, 101).Select(i => new Vector2(i, (i % 2) * 0.2f))
            .Concat(Enumerable.Range(1, 50).Select(i => new Vector2(100, i))).ToList();
        var dots = MapTools.StrokeDots(stroke, 1, 30);
        Assert.Equal(stroke[0], dots[0]);
        Assert.Equal(stroke[^1], dots[^1]);
        Assert.Contains(dots, d => Vector2.Distance(d, new Vector2(100, 0)) < 1.5f);   // the corner
        Assert.All(dots.Zip(dots.Skip(1)), p => Assert.True(Vector2.Distance(p.First, p.Second) <= 30.01f));
        Assert.InRange(dots.Count, 6, 12);   // far fewer than the stroke's 151
    }
}
