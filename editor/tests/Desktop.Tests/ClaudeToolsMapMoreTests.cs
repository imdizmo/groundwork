using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The newer map tools for Claude, on a real map: paths, areas, holes, terraces, paint over the whole map.</summary>
public sealed class ClaudeToolsMapMoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-claude-map-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private (MainWindow W, ClaudeTools T, EditSession S) Open()
    {
        var map = MapProject.Create("Claude", 2, generated: false, height: 10, seed: 1, under: _dir);
        var s = map.Read().Session!;
        var w = TestWindows.Editing(s);
        return (w, new ClaudeTools(w), s);
    }

    [AvaloniaFact]
    public async Task APathIsOneStepAndPaintsItsLayer()
    {
        var (_, t, s) = Open();
        string said = await t.MapPath([[-20, 0], [20, 0]], layer: "Dirt");
        Assert.StartsWith("Path made: 40 m", said);
        Assert.Single(s.UndoList);
        var g = s.Ground;
        int p = ((int)(0 - g.OriginZ) * g.W) + (int)(0 - g.OriginX);
        Assert.True(g.MaskOf(p, 1) > 0.9f);   // Dirt: the new map's third layer, channel g
        Assert.StartsWith("No layer", await t.MapPath([[0, 0], [5, 0]], layer: "Lava"));
    }

    [AvaloniaFact]
    public async Task AnAreaIsAddedReplacedAndRemoved()
    {
        var (_, t, s) = Open();
        Assert.StartsWith("Area Gate:", await t.MapArea("Trigger", 5, 5, 10, 4, name: "Gate"));
        await t.MapArea("Blocking", 6, 5, 10, 4, name: "Gate");
        var a = Assert.Single(MapAreas.All);
        Assert.Equal(("blocking", 6f, -5f), (a.Kind, a.X, a.Y));
        Assert.Equal(9, a.Bottom);   // a metre under the 10 m ground
        Assert.StartsWith("Area Gate removed", await t.MapAreaRemove("Gate"));
        Assert.Empty(MapAreas.All);
        Assert.Equal(3, s.UndoList.Count);
        Assert.StartsWith("No area kind", await t.MapArea("Lava", 0, 0));
    }

    [AvaloniaFact]
    public async Task AHoleIsMadeAndFilled()
    {
        var (_, t, s) = Open();
        await t.MapHole(0, 0, 3);
        var g = s.Ground;
        int p = ((int)(0 - g.OriginZ) * g.W) + (int)(0 - g.OriginX);
        Assert.True(g.MaskOf(p, 3) > 0.5f);
        await t.MapHole(0, 0, 3, fill: true);
        Assert.True(g.MaskOf(p, 3) < 0.5f);
    }

    [AvaloniaFact]
    public async Task PaintingTheWholeMapFollowsItsLimits()
    {
        var (_, t, s) = Open();
        await t.MapTerrace(0, 0, 20, step: 4);   // the flat 10 m ground lies between shelves: it moves onto one
        Assert.StartsWith("Sand painted on 0", await t.MapPaintWhole("Sand", maxHeight: 2));   // none of it that low
        string said = await t.MapPaintWhole("Rock", minHeight: 0);
        Assert.Contains("painted on", said);
        Assert.Equal(3, s.UndoList.Count(c => c.Points.Length > 0) + 1);   // the empty sand step made none
    }

    [AvaloniaFact]
    public async Task ChecksFindAFloatingTreeAndTheGroundHasASlope()
    {
        string meadow = Path.GetFullPath(Path.Combine(Groundwork.Tests.Fixtures.Editor, "..", "samples", "Meadow"));
        var map = MapProject.Create("Checks", 2, generated: false, height: 10, seed: 1, under: _dir, like: MapProject.Open(meadow));
        var s = map.Read().Session!;
        var t = new ClaudeTools(TestWindows.Editing(s));
        _ = s.Commit("Place", null, [], [(new NewObject(0, Groundwork.App.AssetIds.Of("Pine_1"), new System.Numerics.Vector3(3, 12, 3), System.Numerics.Vector3.Zero, 1), false)]);
        string found = await t.MapChecks();
        Assert.Contains("\"problems\":1", found.Replace(" ", ""));
        Assert.Contains("floating 2.0 m", found);
        Assert.Contains("\"slope\":0", (await t.GroundAtPoints([[0, 0]])).Replace(" ", ""));
    }

    [AvaloniaFact]
    public async Task ARoadThroughALakeAndAHouseOnASlopeAreFound()
    {
        var (_, t, s) = Open();
        await t.MapPath([[-30, 0], [30, 0]], layer: "Dirt");
        await t.MapLake([[-8, -8], [8, -8], [8, 8], [-8, 8]], level: 12);
        _ = s.Commit("Place", null, [], [(new NewObject(0, Groundwork.App.AssetIds.Of("House"), new System.Numerics.Vector3(0, 10, 20), System.Numerics.Vector3.Zero, 1), false)]);
        s.EditGround("Slope", g =>
        {
            var touched = Enumerable.Range(0, g.W * g.H).Where(p => p / g.W is > 0 and var gz && gz < g.H - 1 && g.OriginZ + gz > 12).ToList();
            touched.ForEach(p => g.SetHeight(p, 10 + ((g.OriginZ + (p / g.W) - 12) * 0.3f)));   // 17° up to the north
            return (touched, (0, 0, g.W - 1, g.H - 1));
        });
        try
        {
            string found = (await t.MapChecks(road: "Dirt", steepBuildings: 5)).Replace(" ", "");
            Assert.Contains("\"roadunderwater\":", found);
            Assert.Contains("\"onsteepground\":1", found);
            Assert.Contains("roadunderwater2.0m", found);   // the lake at 12 m over the 10 m ground
            Assert.DoesNotContain("\"x\":20", found);       // the road beyond the lake is dry
        }
        finally
        {
            MapWater.Lakes.Clear();
        }
    }

    [AvaloniaFact]
    public async Task DensityCountsAKindInACircleAndItsSpacing()
    {
        var (_, t, s) = Open();
        int pine = Groundwork.App.AssetIds.Of("Pine_1");
        (float X, float Z)[] at = [(0, 0), (4, 0), (0, 6), (30, 30)];
        _ = s.Commit("Place", null, [], at.Select(p => (new NewObject(0, pine, new System.Numerics.Vector3(p.X, 10, p.Z), System.Numerics.Vector3.Zero, 1), false)).ToList());
        string said = (await t.MapDensity(0, 0, 10, kind: "Pine_1")).Replace(" ", "");
        Assert.Contains("\"count\":3", said);
        Assert.Contains("\"perHectare\":95.5", said);   // 3 in 314 m2
        Assert.Contains("\"nearestMin\":4", said);
        Assert.Contains("\"nearestMean\":4.7", said);    // 4, 4 and 6
    }
}
