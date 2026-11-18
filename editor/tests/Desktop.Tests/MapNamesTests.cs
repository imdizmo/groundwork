using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Names on places and named regions: kept in the map's names file, and which of them the view shows where.</summary>
[Collection("MapAreas")]
public class MapNamesTests
{
    private static MapNames.Region Square(string name, float x0, float z0, float side)
    {
        var r = new MapNames.Region { Name = name };
        r.Points.AddRange([new(x0, z0), new(x0 + side, z0), new(x0 + side, z0 + side), new(x0, z0 + side)]);
        return r;
    }

    [Fact]
    public void ASnapshotComesBackAsItWasAndNothingIsWrittenForNoNames()
    {
        MapNames.Labels.Clear();
        MapNames.Regions.Clear();
        string file = Path.Combine(Path.GetTempPath(), $"gw-names-{Guid.NewGuid():N}.json");
        MapNames.Save(file);
        Assert.False(File.Exists(file));

        MapNames.Labels.Add(new MapNames.Label { Text = "Hollow Ford", X = 40, Z = 60.5f, Size = MapNames.Size.Small });
        MapNames.Regions.Add(Square("North Reach", 0, 100, 50));
        MapNames.Save(file);
        MapNames.Labels.Clear();
        MapNames.Regions.Clear();
        MapNames.Load(file);
        File.Delete(file);
        var l = Assert.Single(MapNames.Labels);
        Assert.Equal(("Hollow Ford", 40f, 60.5f, MapNames.Size.Small), (l.Text, l.X, l.Z, l.Size));
        var r = Assert.Single(MapNames.Regions);
        Assert.Equal(("North Reach", 4), (r.Name, r.Points.Count));
    }

    [Fact]
    public void ABadFileKeepsWhatCanBeReadAndNamesAreOneShortLine()
    {
        MapNames.Restore("""{"names": [{"text": "  Two\nlines  ", "x": "no", "z": 3}, {"x": 1}], "regions": [{"name": "Thin", "points": [[0, 0], [1, 1]]}]}""");
        Assert.Equal(["Two lines", "Place"], MapNames.Labels.Select(l => l.Text));
        Assert.Equal((0f, 3f), (MapNames.Labels[0].X, MapNames.Labels[0].Z));
        Assert.Empty(MapNames.Regions);   // two points are no region
        Assert.Equal(MapNames.MostText, MapNames.Clip(new string('a', 500)).Length);
    }

    [Fact]
    public void APointIsInTheSmallestRegionHoldingIt()
    {
        MapNames.Regions.Clear();
        var big = Square("Land", 0, 0, 100);
        var small = Square("Vale", 10, 10, 20);
        MapNames.Regions.AddRange([big, small]);
        Assert.Equal(["Vale", "Land"], MapNames.At(new Vector2(15, 15)).Select(r => r.Name));
        Assert.Equal(["Land"], MapNames.At(new Vector2(60, 60)).Select(r => r.Name));
        Assert.Empty(MapNames.At(new Vector2(-5, 50)));
        Assert.Equal(new Vector2(50, 50), MapNames.Middle(big));
        Assert.Equal(10000, MathF.Abs(MapNames.Area(big)));
        MapNames.Regions.Clear();
    }

    [Fact]
    public void SmallNamesShowOnlyUpCloseAndABiggerNameWinsWhereTheyWouldCover()
    {
        // a camera looking straight down from 300 m: the picture is x across, z up
        var eye = new Vector3(0, 300, 0);
        Vector4 Clip(Vector3 p) => new((p.X - eye.X) / 100, (p.Z - eye.Z) / 100, 0, 1);
        (float, float) Size(string t, MapNames.Size s) => (t.Length * 10, 20);
        var names = new List<(string, MapNames.Size, Vector3)>
        {
            ("Kingdom", MapNames.Size.Region, new Vector3(0, 0, 0)),
            ("Town", MapNames.Size.Place, new Vector3(1, 0, 0)),   // a pixel off the kingdom's name: left out
            ("Farm", MapNames.Size.Small, new Vector3(50, 0, 50)), // 300 m away... within 400 m
            ("Far farm", MapNames.Size.Small, new Vector3(5000, 0, 0)),
            ("Behind", MapNames.Size.Place, new Vector3(-50, 0, -50)),
        };
        var shown = GlView.NamesToShow(names, p => p.X < -10 ? new Vector4(0, 0, 0, -1) : Clip(p), eye, 1000, 1000, Size);
        Assert.Equal(["Kingdom", "Farm"], shown.Select(n => n.Text));
        Assert.Equal((500f, 500f), (shown[0].X, shown[0].Y));
        Assert.True(shown[1].Y < 500);   // north is up the picture
    }

    [Fact]
    public void ANameIsDrawnAsAPicture()
    {
        using var small = GlView.NamePicture("Mill", MapNames.Size.Place, 1);
        using var region = GlView.NamePicture("Mill", MapNames.Size.Region, 1);
        Assert.True(region.Width > small.Width * 1.5);
        Assert.Contains(Enumerable.Range(0, small.Width).SelectMany(x => Enumerable.Range(0, small.Height).Select(y => small.GetPixel(x, y).Alpha)), a => a > 200);
    }
}
