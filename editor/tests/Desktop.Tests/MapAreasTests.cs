using System.Numerics;
using System.Text.Json.Nodes;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Areas: boxes on the map, kept in its areas file, sent to Unreal as volumes.</summary>
[Collection("MapAreas")]
public class MapAreasTests
{
    private static MapAreas.Area Box() => new() { Name = "Gate", Kind = "blocking", X = 10, Y = -4, Width = 6, Depth = 2, Bottom = 1, Top = 4, Yaw = 90 };

    [Fact]
    public void ASnapshotComesBackAsItWas()
    {
        MapAreas.All.Clear();
        MapAreas.All.Add(Box());
        string snap = MapAreas.Snapshot();
        MapAreas.All.Clear();
        MapAreas.Restore(snap);
        var a = Assert.Single(MapAreas.All);
        Assert.Equal(("Gate", "blocking", 10f, -4f, 6f, 2f, 1f, 4f, 90f), (a.Name, a.Kind, a.X, a.Y, a.Width, a.Depth, a.Bottom, a.Top, a.Yaw));
    }

    [Fact]
    public void AFileThatIsNotThereIsNoAreas()
    {
        MapAreas.All.Add(Box());
        MapAreas.Load(Path.Combine(Path.GetTempPath(), "no-such-areas.json"));
        Assert.Empty(MapAreas.All);
    }

    [Fact]
    public void TheCatalogsKindsReplaceTheDefaults()
    {
        Assert.Equal("TriggerVolume", MapAreas.Kinds(null)[0].Unreal);
        var kinds = MapAreas.Kinds(JsonNode.Parse("""{"areas": {"spawn": {"label": "Spawn zone", "unreal": "TriggerVolume", "tag": "spawn", "color": [1, 0, 0]}}}""")!.AsObject());
        var k = Assert.Single(kinds);
        Assert.Equal(("spawn", "Spawn zone", "spawn", new Vector3(1, 0, 0)), (k.Id, k.Label, k.Tag, k.Colour));
    }

    [Fact]
    public void ATurnedBoxHoldsWhatIsInsideIt()
    {
        var a = Box();   // 6 wide along its turn (90°: along Unreal Y), 2 deep
        Assert.True(MapAreas.Holds(a, new Vector2(10, -1.5f)));
        Assert.False(MapAreas.Holds(a, new Vector2(12.5f, -4)));
        Assert.Equal(new Vector2(11, -7), MapAreas.Corners(a)[0], new Vector2Comparer());
    }

    [Fact]
    public void UnrealGetsCentimetresAndTheKindsClass()
    {
        MapAreas.All.Clear();
        MapAreas.All.Add(Box());
        var a = MapAreas.ForUnreal(MapAreas.Defaults)["areas"]![0]!;
        Assert.Equal("BlockingVolume", a["class"]!.GetValue<string>());
        Assert.Equal((1000f, -400f, 250f, 600f, 200f, 300f), (a["x"]!.GetValue<float>(), a["y"]!.GetValue<float>(), a["z"]!.GetValue<float>(), a["sx"]!.GetValue<float>(), a["sy"]!.GetValue<float>(), a["sz"]!.GetValue<float>()));
    }

    [Fact]
    public void ANewNameIsFree()
    {
        MapAreas.All.Clear();
        MapAreas.All.Add(new MapAreas.Area { Name = "Trigger 1" });
        Assert.Equal("Trigger 2", MapAreas.NewName("Trigger"));
    }

    private sealed class Vector2Comparer : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 1e-3f;

        public int GetHashCode(Vector2 v) => 0;
    }
}
