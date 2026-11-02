using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The zone generator: hills between their heights, a rim, lakes that hold water, woods of the share asked, layers by rule.</summary>
public class ZoneGeneratorTests
{
    private static readonly MapTools.Kind Tree = new("Tree", "Tree", "Trees", 1, 1, null), Rock = new("Rock", "Rock", "Rocks", 1, 1, null);
    private static readonly MapTools.Kind Hut = new("Hut", "Hut", "Buildings", 1, 1, null), Crate = new("Crate", "Crate", "Props", 1, 1, null);
    private static readonly ZoneGenerator.Planting Kinds = new([Tree], [], [Rock], []) { Buildings = [Hut], Props = [Crate] };

    private static ZoneGenerator.Zone Make(ZoneGenerator.Spec spec, int n = 400) => ZoneGenerator.Generate(spec, n, n, -200, -200, Kinds);

    [Fact]
    public void TheRollingGroundStaysBetweenItsHeights()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 60, RimHeight: 0, LakeHectares: []));
        Assert.InRange(z.Heights.Min(), 19.9f, 30);
        Assert.InRange(z.Heights.Max(), 40, 60.1f);
    }

    [Fact]
    public void TheRimRisesAtTheEdgeAndIsSteep()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 60, RimHeight: 200, RimWidth: 80, LakeHectares: []));
        // the rim changes along the edge (high in places, low in others): its highest ground near the edge is far up
        float edge = Enumerable.Range(0, 400).Max(c => MathF.Max(z.Heights[(5 * 400) + c], z.Heights[(394 * 400) + c]));
        Assert.True(edge > 150, $"{edge}");
        Assert.True(z.Heights[(200 * 400) + 200] < 61);   // the middle
        int steep = Enumerable.Range(0, 400 * 400).Count(p => ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) > 30);
        Assert.True(steep > 400 * 400 / 20, $"{steep}");
    }

    [Fact]
    public void ALakeHoldsItsWaterAndNothingStandsInIt()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [3]));
        var l = Assert.Single(z.Lakes);
        // the shore a few metres out all round is above the surface, the middle below it
        Assert.All(l.Outline, p => Assert.True(Height(z, p + (Vector2.Normalize(p - l.Middle) * 3)) > l.Level, $"{Height(z, p)} vs {l.Level}"));
        Assert.True(Height(z, l.Middle) < l.Level - 1);
        Assert.DoesNotContain(z.Objects, o => MapWater.InsideBy(l.Outline, new Vector2(o.At.X, -o.At.Z)) > 0);
    }

    [Fact]
    public void TheWoodsCoverTheShareAsked()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [], Woods: 0.4f));
        float share = z.Wooded.Count(w => w > 0.5f) / (float)z.Wooded.Length;
        Assert.InRange(share, 0.32f, 0.48f);
        Assert.Contains(z.Objects, o => o.Model == "Tree");
    }

    [Fact]
    public void SteepGroundIsRock()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 200, RimWidth: 80, LakeHectares: []));
        int p = Enumerable.Range(0, 400 * 400).First(i => ZoneGenerator.Slope(z.Heights, 400, 400, i % 400, i / 400) > 46);
        Assert.True(z.Mask[(p * 4) + 2] > 200);
    }

    [Fact]
    public void ASeedMakesTheSameZoneAgain()
    {
        var spec = new ZoneGenerator.Spec(LakeHectares: [2], Seed: 5);
        Assert.Equal(Make(spec).Heights, Make(spec).Heights);
        Assert.NotEqual(Make(spec).Heights, Make(spec with { Seed = 6 }).Heights);
    }

    [Fact]
    public void TerracesMakeFlatShelvesAndSteepRisers()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 120, HillSize: 150, RimHeight: 0, LakeHectares: [], Terraces: 20));
        float near = z.Heights.Count(v => MathF.Abs(v - (MathF.Round(v / 20) * 20)) < 3) / (float)z.Heights.Length;
        Assert.True(near > 0.5f, $"{near} of the ground near a shelf");
        int steep = Enumerable.Range(0, 400 * 400).Count(p => ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) > 35);
        Assert.True(steep > 400 * 400 / 25, $"{steep} steep");
    }

    [Fact]
    public void CanyonsCutDownIntoTheGround()
    {
        var flat = Make(new ZoneGenerator.Spec(Low: 50, High: 60, RimHeight: 0, LakeHectares: []));
        var cut = Make(new ZoneGenerator.Spec(Low: 50, High: 60, RimHeight: 0, LakeHectares: [], Canyons: 40, CanyonSpacing: 150, CanyonWidth: 25));
        Assert.True(cut.Heights.Min() < 20, $"{cut.Heights.Min()}");
        float deep = cut.Heights.Count(v => v < 30) / (float)cut.Heights.Length;
        Assert.InRange(deep, 0.02f, 0.4f);
        Assert.True(flat.Heights.Min() > 49);
    }

    [Fact]
    public void ACoastGoesUnderTheSeaOnItsSideOnly()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 40, RimHeight: 100, RimWidth: 60, LakeHectares: [], Coast: 120, CoastSide: "west"));
        Assert.True(Enumerable.Range(0, 400).Average(r => z.Heights[r * 400]) < -5);        // the west edge under the sea
        Assert.True(Enumerable.Range(0, 400).Max(r => z.Heights[(r * 400) + 399]) > 80);    // the rim still on the east
        Assert.DoesNotContain(z.Objects, o => o.At.Y < 0.3f);                                  // nothing planted in the sea
        var drop = Make(new ZoneGenerator.Spec(Low: 20, High: 40, RimHeight: 0, LakeHectares: [], Coast: 120, CoastSide: "north", CoastDry: true));
        Assert.True(Enumerable.Range(0, 400).Average(c => drop.Heights[c]) < -200);          // the world's edge: a drop, row 0 is north
        Assert.DoesNotContain(drop.Objects, o => o.At.Y < -200);
    }

    [Fact]
    public void ARiverRunsDownhillInACarvedBed()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 90, HillSize: 250, RimHeight: 0, LakeHectares: [], Rivers: 1, RiverWidth: 6), 500);
        Assert.NotEmpty(z.Rivers);
        float last = float.MaxValue;
        foreach (var r in z.Rivers)
        {
            Assert.True(r.Top >= r.End - 0.01f, $"{r.Top} -> {r.End}");
            Assert.True(r.Top <= last + 0.01f);
            last = r.End;
            // the bed under each point is below the surface there
            for (int i = 0; i < r.Points.Count; i++)
            {
                float t = r.Points.Count == 1 ? 0 : i / (float)(r.Points.Count - 1), surface = r.Top + ((r.End - r.Top) * t);
                var p = r.Points[i];
                int c = Math.Clamp((int)MathF.Round(p.X + 200), 0, 499), row = Math.Clamp((int)MathF.Round(p.Y + 200), 0, 499);
                Assert.True(z.Heights[(row * 500) + c] < surface, $"bed {z.Heights[(row * 500) + c]} vs surface {surface}");
            }
        }
        float length = z.Rivers.Sum(r => r.Points.Zip(r.Points.Skip(1), (a, b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y))).Sum());
        Assert.True(length > 150, $"{length} m");
    }

    [Fact]
    public void FloatersHangHighAboveTheGroundAndSnowLiesHigh()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 200, RimHeight: 0, LakeHectares: [], Woods: 0, OpenTrees: 0, Small: 0, Floaters: 6, SnowLine: 150));
        var up = z.Objects.Where(o => o.Model == "Rock").ToList();
        Assert.Equal(6, up.Count);
        Assert.All(up, o => Assert.True(o.At.Y - Height(z, new Vector2(o.At.X, -o.At.Z)) > 40));
        int high = Enumerable.Range(0, z.Heights.Length).First(p => z.Heights[p] > 180);
        Assert.True(z.Mask[high * 4] > 200);
        int low = Enumerable.Range(0, z.Heights.Length).First(p => z.Heights[p] < 60 && ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) < 10);
        Assert.True(z.Mask[low * 4] < 50);
        var second = Make(new ZoneGenerator.Spec(Low: 20, High: 200, RimHeight: 0, LakeHectares: [], Woods: 0, OpenDirt: 0, SnowLine: 150, SnowChannel: 1));
        Assert.True(second.Mask[(high * 4) + 1] > 200 && second.Mask[high * 4] < 50);
    }

    [Fact]
    public void RoughGroundHasLessFlat()
    {
        int Flat(ZoneGenerator.Zone z) => Enumerable.Range(0, 400 * 400).Count(p => ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) < 10);
        int smooth = Flat(Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [])));
        int rough = Flat(Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [], Rough: 6)));
        Assert.True(rough < smooth * 0.8f, $"{rough} flat points rough, {smooth} smooth");
    }

    [Fact]
    public void CampsStandOnLevelledGroundJoinedByRoads()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 80, HillSize: 200, RimHeight: 0, LakeHectares: [], Camps: 3, CampSize: 5, Roads: true), 500);
        var huts = z.Objects.Where(o => o.Model == "Hut").ToList();
        Assert.Equal(15, huts.Count);
        Assert.Contains(z.Objects, o => o.Model == "Crate");
        Assert.All(huts, h => Assert.True(MathF.Abs(h.At.Y - Height(z, new Vector2(h.At.X, -h.At.Z), 500)) < 0.6f, "a hut off the ground"));
        // each camp's ground is level: its huts stand within a couple of metres of each other's height
        foreach (var camp in huts.Chunk(5))
        {
            Assert.True(camp.Max(h => h.At.Y) - camp.Min(h => h.At.Y) < 2.5f, $"camp heights {string.Join(", ", camp.Select(h => h.At.Y))}");
        }
        Assert.DoesNotContain(z.Objects, o => o.Model == "Tree" && huts.Any(h => Vector2.Distance(new(h.At.X, h.At.Z), new(o.At.X, o.At.Z)) < 6));
        Assert.True(z.Roads.Count >= 2);
        // a road is painted with the dirt layer along it
        var mid = z.Roads[0][z.Roads[0].Count / 2];
        int p = ((int)MathF.Round(mid.Y + 200) * 500) + (int)MathF.Round(mid.X + 200);
        Assert.True(z.Mask[(p * 4) + 1] > 200, $"road paint {z.Mask[(p * 4) + 1]}");
        var sand = Make(new ZoneGenerator.Spec(Low: 20, High: 80, HillSize: 200, RimHeight: 0, LakeHectares: [], Camps: 3, CampSize: 5, Roads: true, RoadChannel: 0), 500);
        Assert.True(sand.Mask[p * 4] > 200 && sand.Mask[(p * 4) + 1] < 50, "a road on the first layer");
    }

    [Fact]
    public void AMarshFloodsPartOfTheLowGround()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 1, High: 30, RimHeight: 0, LakeHectares: [], Marsh: 8, MarshShare: 0.4f));
        var dry = Make(new ZoneGenerator.Spec(Low: 1, High: 30, RimHeight: 0, LakeHectares: []));
        float wet = z.Heights.Count(v => v < 0) / (float)z.Heights.Length;
        Assert.InRange(wet, 0.05f, 0.45f);
        Assert.Equal(0, dry.Heights.Count(v => v < 0));
        Assert.DoesNotContain(z.Objects, o => o.At.Y < 0.3f);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void AZoneMapKeepsItsRiversAndADryEdgeHasNoSea()
    {
        string dir = Directory.CreateTempSubdirectory("gw-zone-").FullName;
        try
        {
            var spec = new ZoneGenerator.Spec(Low: 20, High: 90, HillSize: 250, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Rivers: 1, Coast: 80, CoastSide: "east", CoastDry: true);
            var m = MapProject.CreateZone("River", 8, spec, Kinds, dir);
            var g = m.Read().Session!.Ground;
            Assert.NotEmpty(MapWater.Rivers);
            int east = ((g.H / 2) * g.W) + g.W - 3, west = ((g.H / 2) * g.W) + 2;
            Assert.True(g.MaskOf(east, 3) > 0.5f && g.MaskOf(west, 3) < 0.5f);   // the drop past the edge is a hole: nothing
            Assert.Equal(-1000, MapProject.Open(m.Folder).SeaLevel);
        }
        finally
        {
            MapWater.Rivers.Clear();
            Directory.Delete(dir, true);
        }
    }

    private static float Height(ZoneGenerator.Zone z, Vector2 unreal, int n = 400)
    {
        int c = Math.Clamp((int)MathF.Round(unreal.X + 200), 0, n - 1), r = Math.Clamp((int)MathF.Round(unreal.Y + 200), 0, n - 1);
        return z.Heights[(r * n) + c];
    }
}
