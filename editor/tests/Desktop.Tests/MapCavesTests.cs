using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Tunnels: the tube under the ground with its mouths at the surface, drawn as a scene layer with the ground left
/// undrawn at the mouths, cut into the hole channel as one step, and kept as a snapshot.
/// </summary>
[Collection("MapCaves")]
public sealed class MapCavesTests : IDisposable
{
    // TestScenes.Flat: ground 30 m high, grid point (gx, gz) at world (gx - 32, gz - 32)
    private static int P(Ground g, float x, float z) => ((int)(z + 32) * g.W) + (int)(x + 32);

    public MapCavesTests() => MapCaves.Tunnels.Clear();

    public void Dispose() => MapCaves.Tunnels.Clear();

    private static MapCaves.Tunnel Straight(float depth = 8, float radius = 3, bool endOpen = true)
    {
        var t = new MapCaves.Tunnel { Name = "T", EndOpen = endOpen };
        t.Points.AddRange([new(-20, 0, depth, radius), new(0, 0, depth, radius), new(20, 0, depth, radius)]);
        return t;
    }

    private static float Flat(float x, float z) => 30;

    [Fact]
    public void TheLineRunsFromTheMouthDownToTheDepthAndUpToTheFarMouth()
    {
        var line = MapCaves.Line(Straight(), Flat);
        Assert.True(line.Count > 20);
        // The mouths' middles sit 0.55 radii over the ground (the floor at the ground); the middle of the run has its
        // roof 8 m under (the middle 1.3 radii lower still).
        Assert.Equal(30 + (0.55f * 3), line[1].P.Y, 0.01f);
        Assert.Equal(30 - 8 - (1.3f * 3), line[line.Count / 2].P.Y, 0.6f);
        Assert.Equal(30 + (0.55f * 3), line[^2].P.Y, 0.01f);
        // The lips run on past the ends.
        Assert.True(line[0].P.X < -20 && line[^1].P.X > 20);
    }

    [Fact]
    public void TheTubeIsAValidMeshWhoseFrontFacesInward()
    {
        var line = MapCaves.Line(Straight(), Flat);
        var (pos, nrm, col, tri) = MapCaves.Tube(line, endOpen: true, seed: 1);
        Assert.Equal(line.Count * MapCaves.Sides, pos.Length);
        Assert.Equal(pos.Length, nrm.Length);
        Assert.Equal(pos.Length, col.Length);
        Assert.Equal(0, tri.Length % 3);
        Assert.All(tri, i => Assert.InRange(i, 0, pos.Length - 1));
        // Each corner's normal points towards the tube's middle line (the inside is the front).
        int inward = 0;
        for (int i = 0; i < pos.Length; i++)
        {
            var middle = line[i / MapCaves.Sides].P;
            if (Vector3.Dot(nrm[i], middle - pos[i]) > 0)
            {
                inward++;
            }
        }
        Assert.True(inward > pos.Length * 0.9, $"{inward} of {pos.Length} normals point inward");
    }

    [Fact]
    public void AClosedFarEndIsCapped()
    {
        var line = MapCaves.Line(Straight(endOpen: false), Flat);
        var (openPos, _, _, openTri) = MapCaves.Tube(line, endOpen: true, seed: 1);
        var (closedPos, _, _, closedTri) = MapCaves.Tube(line, endOpen: false, seed: 1);
        Assert.Equal(openPos.Length + 1, closedPos.Length);
        Assert.Equal(openTri.Length + (MapCaves.Sides * 3), closedTri.Length);
    }

    [Fact]
    public void TheMouthsBreakTheGroundAndTheDeepRunDoesNot()
    {
        var line = MapCaves.Line(Straight(), Flat);
        var mouths = MapCaves.Mouths(line, Flat);
        Assert.NotEmpty(mouths);
        Assert.Contains(mouths, m => m.X0 <= -20 && m.X1 >= -20);
        Assert.Contains(mouths, m => m.X0 <= 20 && m.X1 >= 20);
        Assert.DoesNotContain(mouths, m => m.X0 <= 0 && m.X1 >= 0);
    }

    [Fact]
    public void TheSceneLayerDrawsEveryTunnelWithItsHoles()
    {
        MapCaves.Tunnels.Add(Straight());
        MapCaves.Tunnels.Add(new MapCaves.Tunnel());   // too few points: nothing drawn
        var s = TestScenes.Flat(2);
        var layer = new MapCaves.Scene().Build(new SceneLayers.AreaView(s.Scene));
        Assert.NotNull(layer);
        Assert.Single(layer.Meshes);
        Assert.NotEmpty(layer.Holes);
        MapCaves.Tunnels.Clear();
        Assert.Null(new MapCaves.Scene().Build(new SceneLayers.AreaView(s.Scene)));
    }

    [Fact]
    public void CutPaintsTheMouthsIntoTheHoleChannelAsOneStepThatUndoes()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        var area = new SceneLayers.AreaView(s.Scene);
        int n = MapCaves.Cut(s, Straight(), area.GroundHeight);
        Assert.True(n > 10);
        Assert.Equal(1, g.MaskOf(P(g, -20, 0), Ground.HoleChannel), 0.01f);
        Assert.Equal(1, g.MaskOf(P(g, 20, 0), Ground.HoleChannel), 0.01f);
        Assert.Equal(0, g.MaskOf(P(g, 0, 0), Ground.HoleChannel), 0.01f);
        Assert.True(s.Undo());
        Assert.Equal(0, g.MaskOf(P(g, -20, 0), Ground.HoleChannel), 0.01f);
        // Nothing to cut twice.
        Assert.True(s.Redo());
        Assert.Equal(0, MapCaves.Cut(s, Straight(), area.GroundHeight));
    }

    [Fact]
    public void ASnapshotComesBackWhole()
    {
        MapCaves.Tunnels.Add(Straight(depth: 5.5f, radius: 2.5f, endOpen: false));
        string snap = MapCaves.Snapshot();
        MapCaves.Tunnels.Clear();
        MapCaves.Restore(snap);
        var t = Assert.Single(MapCaves.Tunnels);
        Assert.Equal("T", t.Name);
        Assert.False(t.EndOpen);
        Assert.Equal(3, t.Points.Count);
        Assert.Equal(new MapCaves.Point(0, 0, 5.5f, 2.5f), t.Points[1]);
        Assert.True(MapCaves.Dirty);
    }
}
