using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A map whose grid is padded more on one side (grown east): the 3D view's ground and its objects at the same place, the
/// scene's middle the grid's.
/// </summary>
[Collection("DataDir")]
public sealed class MapSceneCentreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-centre-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    /// <summary>The map put back, the test's maps deleted.</summary>
    public void Dispose()
    {
        MapProject.Current = _was;
        Directory.Delete(_dir, recursive: true);
    }

    [AvaloniaFact]
    public void TheGroundIsDrawnWhereItsObjectsAre()
    {
        var map = MapProject.Create("Grown", 2, generated: true, height: 10, seed: 3, under: _dir);
        map.Grow(MapProject.Sides.East, 63, generated: true, flatHeight: 10, seed: 3);

        var s = MapProject.Open(map.Folder).Read();
        var g = s.Session!.Ground;

        Assert.Equal(g.OriginX + ((s.W - 1) / 2f), s.Cx);
        Assert.Equal(g.OriginZ + ((s.H - 1) / 2f), s.Cz);
        foreach (var (wx, wz) in new[] { (60f, 10f), (-50f, -40f), (100f, 50f) })
        {
            int p = ((int)(wz - g.OriginZ) * g.W) + (int)(wx - g.OriginX);
            Assert.Equal(g.HeightOf(p), Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz)), 2);
        }
    }
}
