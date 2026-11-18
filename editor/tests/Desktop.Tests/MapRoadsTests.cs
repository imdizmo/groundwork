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
        MapRoads.Save(file);
        Assert.False(File.Exists(file));   // no roads, no file

        var before = new Dictionary<int, MapRoads.Point> { [12] = new(3.5f, [0.25f, 0.75f]), [13] = new(-1, [1, 0]) };
        MapRoads.All.Add(new MapRoads.Road("Road 1", [new(0, 0), new(10, 5)], 4, 2, false, 12, "Sand", "Post", 3, 0.5f, false, "Bridge") { Before = before, Grid = (64, 64, 0, 0) });
        MapRoads.All.Add(new MapRoads.Road("Road 2", [new(5, 5), new(6, 9)], 3, 3, true, 15, null, null, 3, 0.5f, true, null));
        MapRoads.Save(file);
        MapRoads.All.Clear();
        MapRoads.Load(file);
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
}
