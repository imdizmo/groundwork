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
    public void ARiverRunsRoundTheHillsInsteadOfCuttingATrench()
    {
        // the same ground without its rivers: how deep each river cut under where it runs
        var spec = new ZoneGenerator.Spec(Low: 0, High: 160, HillSize: 350, RimHeight: 0, LakeHectares: [], Rivers: 2, RiverWidth: 6, Seed: 3);
        var z = Make(spec, 1000);
        var dry = Make(spec with { Rivers = 0 }, 1000);
        // (away from the lakes it may end in, whose banks slope down to their water)
        var cuts = z.Rivers.SelectMany(r => r.Points).Where(p => z.Lakes.All(l => MapWater.InsideBy(l.Outline, new(p.X, p.Y)) < -100)).Select(p =>
        {
            int i = (Math.Clamp((int)MathF.Round(p.Y + 200), 0, 999) * 1000) + Math.Clamp((int)MathF.Round(p.X + 200), 0, 999);
            return dry.Heights[i] - z.Heights[i];
        }).Order().ToList();
        Assert.True(cuts.Count > 20, $"{cuts.Count} points");
        // its bed (a couple of metres under the ground it follows), deeper only through the odd lip out of a hollow
        Assert.True(cuts[(int)(cuts.Count * 0.9f)] < 4.5f, $"90% of the river cut under {cuts[(int)(cuts.Count * 0.9f)]} m (deepest {cuts[^1]} m)");
        Assert.True(cuts[^1] < 8, $"deepest cut {cuts[^1]} m");
    }

    [Fact]
    public void ARiverEndsWhereItReachesTheSeaWithoutACoast()
    {
        // low ground under the sea's level (0), no coast: the sea there is where the rivers end, not run on through
        var spec = ZoneGenerator.Styles.Single(s => s.Name == "Grassland with plateaus").Spec with { RimWidth = 101, LakeHectares = [] };
        var z = Make(spec, 800);
        var dry = Make(spec with { Rivers = 0 }, 800);
        Assert.NotEmpty(z.Rivers);
        var points = z.Rivers.SelectMany(r => r.Points).ToList();
        int inSea = points.Count(p => dry.Heights[(Math.Clamp((int)MathF.Round(p.Y + 200), 0, 799) * 800) + Math.Clamp((int)MathF.Round(p.X + 200), 0, 799)] < -1);
        Assert.True(inSea <= z.Rivers.Count, $"{inSea} of {points.Count} river points out in the sea");
    }

    [Fact]
    public void CliffPiecesStepTheSteepGroundAndFaceDownIt()
    {
        var cliff = new MapTools.Kind("Cliff_0", "Cliff", CliffPiece.Category, 1, 1, null);
        var kinds = Kinds with { Cliffs = [cliff] };
        var spec = new ZoneGenerator.Spec(Low: 0, High: 50, HillSize: 250, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Terraces: 20, CliffSlope: 35);
        var z = ZoneGenerator.Generate(spec, 500, 500, -200, -200, kinds);
        var pieces = z.Objects.Where(o => o.Model == "Cliff_0").ToList();
        Assert.True(pieces.Count > 20, $"{pieces.Count} pieces");
        float G(float x, float y) => z.Heights[(Math.Clamp((int)MathF.Round(y + 200), 0, 499) * 500) + Math.Clamp((int)MathF.Round(x + 200), 0, 499)];
        int stepped = 0, flush = 0;
        foreach (var (_, at, yaw, scale) in pieces)
        {
            // the ground steps down at its face: just behind it the hill's top (filled out to it, the piece's top just
            // under it), a few metres in front well below
            var ahead = new Vector2(MathF.Sin(yaw * MathF.PI / 180), MathF.Cos(yaw * MathF.PI / 180));   // editor x, z
            float behind = G(at.X - (ahead.X * CliffPiece.Depth * scale * 0.3f), -(at.Z - (ahead.Y * CliffPiece.Depth * scale * 0.3f)));
            float before = G(at.X + (ahead.X * (3 + (2 * scale))), -(at.Z + (ahead.Y * (3 + (2 * scale)))));
            stepped += behind - before > 3 ? 1 : 0;
            float top = at.Y + (CliffPiece.Height * scale);
            flush += top <= behind + 0.1f && behind - top < 4 ? 1 : 0;
            Assert.True(at.Y < G(at.X, -at.Z), "its foot in the ground");
        }
        Assert.True(stepped > pieces.Count * 0.8f, $"{stepped} of {pieces.Count} with the ground stepping down at their face");
        Assert.True(flush > pieces.Count * 0.8f, $"{flush} of {pieces.Count} with their top just under the ground behind");
        Assert.DoesNotContain(ZoneGenerator.Generate(spec with { CliffSlope = 0 }, 500, 500, -200, -200, kinds).Objects, o => o.Model == "Cliff_0");
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
    public void FloatingIslandsHangApartWithTheirTopsPlanted()
    {
        var island = new MapTools.Kind("FloatingIsland_Test_2", "Floating island 3", FloatingIsland.Category, 1, 1, null);
        var z = ZoneGenerator.Generate(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [], Woods: 0, OpenTrees: 0, Small: 0, Floaters: 4), 400, 400, -200, -200, Kinds with { Islands = [island] });
        var islands = z.Objects.Where(o => o.Model == island.Model).ToList();
        Assert.InRange(islands.Count, 2, 4);
        // every tree is on an island's top: within its edge, at its top's height there
        var trees = z.Objects.Where(o => o.Model == "Tree").ToList();
        Assert.NotEmpty(trees);
        Assert.All(trees, t => Assert.Contains(islands, i =>
        {
            var local = ZoneGenerator.Turn((t.At.X - i.At.X) / i.Scale, (t.At.Z - i.At.Z) / i.Scale, -i.Yaw);
            return FloatingIsland.TopAt(2, local.X, local.Y) is { } top && MathF.Abs(i.At.Y + (top * i.Scale) - 0.3f - t.At.Y) < 0.01f;
        }));
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
    public void AShareOfTheTreesAreGiants()
    {
        var z = Make(new ZoneGenerator.Spec(RimHeight: 0, LakeHectares: [], Woods: 0.4f, WoodDensity: 120, Small: 0, Giants: 0.2f, GiantScale: 3), 500);
        var trees = z.Objects.Where(o => o.Model == "Tree").ToList();
        float giants = trees.Count(t => t.Scale > 2) / (float)trees.Count;
        Assert.InRange(giants, 0.15f, 0.25f);
        Assert.All(trees.Where(t => t.Scale > 2), t => Assert.InRange(t.Scale, 2.4f, 3.6f));
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
