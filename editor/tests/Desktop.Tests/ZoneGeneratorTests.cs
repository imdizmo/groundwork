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
    public void FjordsCutDeepSheerInletsFromTheCoastThatEndInland()
    {
        const int N = 1200;
        var spec = new ZoneGenerator.Spec(Low: 250, High: 300, HillSize: 1200, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Shore: 0, Coast: 150, CoastDepth: 300, Fjords: 300, FjordWidth: 150, FjordSpacing: 400);
        var cut = ZoneGenerator.Generate(spec, N, N, -600, -600, Kinds);
        var plain = ZoneGenerator.Generate(spec with { Fjords = 0 }, N, N, -600, -600, Kinds);
        float Under(ZoneGenerator.Zone z, int c0, int c1) => Enumerable.Range(0, N * N).Count(p => p % N >= c0 && p % N < c1 && z.Heights[p] < -100) / (float)(N * (c1 - c0));
        Assert.True(Under(cut, 300, 500) > 0.05f, $"{Under(cut, 300, 500)}");   // deep water well in from the coast
        Assert.Equal(0, Under(plain, 300, 500));
        Assert.True(Under(cut, 1000, N) < Under(cut, 300, 500) / 4, "shallower and narrower inland");
        // sheer walls inland of the coast's own drop: the ground over 45° many times what the plain fells have
        int Sheer(ZoneGenerator.Zone z) => Enumerable.Range(0, N * N).Count(p => p % 3 == 0 && p % N >= 300 && z.Heights[p] > 0 && ZoneGenerator.Slope(z.Heights, N, N, p % N, p / N) > 45);
        Assert.True(Sheer(cut) > 5 * Math.Max(1, Sheer(plain)), $"{Sheer(cut)} against {Sheer(plain)}");
        Assert.DoesNotContain(cut.Objects, o => o.At.Y < 0.3f);
        // no water trapped at the walls' feet (trenches between a wall and the shore below it): each pool under the sea
        // that the open sea (from the map's edge) does not reach, small ones counted (a big one is a loch, its line a loop)
        int[] pool = new int[N * N];
        var sizes = new List<int> { 0 };
        for (int p0 = 0; p0 < N * N; p0++)
        {
            if (cut.Heights[p0] >= 0 || pool[p0] != 0)
            {
                continue;
            }
            var queue = new Queue<int>([p0]);
            (pool[p0], bool edge, int size) = (sizes.Count, false, 0);
            while (queue.TryDequeue(out int p))
            {
                size++;
                edge |= p % N == 0 || p % N == N - 1 || p / N == 0 || p / N == N - 1;
                foreach (int q in new[] { p - 1, p + 1, p - N, p + N })
                {
                    if (q >= 0 && q < N * N && Math.Abs((q % N) - (p % N)) <= 1 && pool[q] == 0 && cut.Heights[q] < 0)
                    {
                        pool[q] = sizes.Count;
                        queue.Enqueue(q);
                    }
                }
            }
            sizes.Add(edge ? 0 : size);
        }
        int trapped = sizes.Where(n => n < 5000).Sum();
        Assert.True(trapped < N * N / 2000, $"{trapped} m² of water trapped in small pools: {string.Join(", ", sizes.Where(n => n > 0).OrderDescending().Take(8))}");
    }

    [Fact]
    public void CragsGrainTheSteepFacesAndLeaveTheFlatAlone()
    {
        const int N = 800;
        var spec = new ZoneGenerator.Spec(Low: 150, High: 200, HillSize: 800, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Shore: 0, Coast: 150, CoastDepth: 300, Fjords: 200, FjordWidth: 150, FjordSpacing: 400);
        var plain = ZoneGenerator.Generate(spec, N, N, -400, -400, Kinds);
        var cragged = ZoneGenerator.Generate(spec with { Crags = 5 }, N, N, -400, -400, Kinds);
        float Change(int p) => MathF.Abs(cragged.Heights[p] - plain.Heights[p]);
        bool Flat(int p) => ZoneGenerator.Slope(plain.Heights, N, N, p % N, p / N) < 3;
        var steep = Enumerable.Range(0, N * N).Where(p => plain.Heights[p] > 2 && ZoneGenerator.Slope(plain.Heights, N, N, p % N, p / N) > 40).ToList();
        bool FlatAround(int p) => Enumerable.Range(-6, 13).All(dr => Enumerable.Range(-6, 13).All(dc => Flat(p + (dr * N) + dc)));   // flat 6 m each way: no face blurred into it
        var flat = Enumerable.Range(0, N * N).Where(p => p % 3 == 0 && p % N is >= 6 and < N - 6 && p / N is >= 6 and < N - 6 && plain.Heights[p] > 2 && FlatAround(p)).ToList();
        Assert.True(steep.Count > 1000 && flat.Count > 1000, $"{steep.Count} steep, {flat.Count} flat");
        Assert.True(steep.Average(Change) > 1, $"{steep.Average(Change)} m on the faces");   // the faces carved by metres
        Assert.True(flat.Max(Change) < 0.5f, $"{flat.Max(Change)} m on the flats");   // the flats as they were
        Assert.DoesNotContain(Enumerable.Range(0, N * N), p => plain.Heights[p] > 1 && cragged.Heights[p] < 1);   // no land cut under the sea
    }

    private static readonly ZoneGenerator.Spec Steep = new(Low: 20, High: 260, HillSize: 300, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Shore: 0, Coast: 120, CoastDepth: 40);

    [Fact]
    public void StrataCutBandsBackIntoTheFacesOnly()
    {
        var plain = Make(Steep);
        var banded = Make(Steep with { Strata = 8 });
        var all = Enumerable.Range(0, plain.Heights.Length).ToList();
        var faces = all.Where(p => plain.Heights[p] > 2 && ZoneGenerator.Slope(plain.Heights, 400, 400, p % 400, p / 400) > 35).ToList();
        Assert.True(faces.Count > 500, $"{faces.Count} face points");
        Assert.True(faces.Average(p => plain.Heights[p] - banded.Heights[p]) > 0.5f, "the faces cut back by bands");
        Assert.DoesNotContain(all, p => banded.Heights[p] > plain.Heights[p] + 1e-3f);   // never built out
        Assert.DoesNotContain(all, p => plain.Heights[p] > 1 && banded.Heights[p] < 1 - 1e-3f);   // no land cut under the sea
    }

    [Fact]
    public void ErosionMovesGroundDownhillAndLeavesTheSeaAlone()
    {
        var plain = Make(Steep);
        var worn = Make(Steep with { Erosion = 1 });
        var land = Enumerable.Range(0, plain.Heights.Length).Where(p => plain.Heights[p] > 5).ToList();
        float moved = land.Average(p => MathF.Abs(worn.Heights[p] - plain.Heights[p])), net = land.Average(p => worn.Heights[p] - plain.Heights[p]);
        Assert.True(moved > 0.2f, $"{moved} m moved on average");
        Assert.True(MathF.Abs(net) < moved / 3, $"{net} m net against {moved} m moved: ground carried, not made or lost");
        // what was high lost ground, what was low gained it
        float Top(ZoneGenerator.Zone z) => land.Where(p => plain.Heights[p] > 200).Average(p => z.Heights[p]);
        Assert.True(Top(worn) < Top(plain), "the heights worn down");
        Assert.Equal(plain.Heights.Where(v => v < -5), worn.Heights.Where((v, p) => plain.Heights[p] < -5));
    }

    [Fact]
    public void ScreeLetsTheSteepGroundRestAtItsAngle()
    {
        var plain = Make(Steep with { Crags = 0 });
        var settled = Make(Steep with { Talus = 30 });
        int Steeper(ZoneGenerator.Zone z) => Enumerable.Range(0, z.Heights.Length).Count(p => z.Heights[p] > 2 && ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) > 40);
        Assert.True(Steeper(plain) > 500, $"{Steeper(plain)}");
        Assert.True(Steeper(settled) < Steeper(plain) / 3, $"{Steeper(settled)} steeper than 40° against {Steeper(plain)}");
    }

    [Fact]
    public void RidgesSharpenTheHillsWithinTheSameHeights()
    {
        var round = Make(Steep with { Coast = 0 });
        var ridged = Make(Steep with { Coast = 0, Ridges = 1 });
        Assert.NotEqual(round.Heights, ridged.Heights);
        // still stretched over the same heights, and the crests sharper: more ground near the top is steep
        Assert.InRange(ridged.Heights.Max(), 200, 400);
        float SteepTops(ZoneGenerator.Zone z) => Enumerable.Range(0, z.Heights.Length).Count(p => z.Heights[p] > 200 && ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) > 30) / (float)Math.Max(1, z.Heights.Count(v => v > 200));
        Assert.True(SteepTops(ridged) > SteepTops(round), $"{SteepTops(ridged)} of the tops steep against {SteepTops(round)}");
    }

    [Fact]
    public void ExpandBroadensTheHighGroundOrTheLow()
    {
        var plain = Make(Steep);
        var up = Make(Steep with { Expand = 30 });
        var down = Make(Steep with { Expand = -30 });
        var land = Enumerable.Range(0, plain.Heights.Length).Where(p => plain.Heights[p] > 5).ToList();
        int Flat(ZoneGenerator.Zone z) => land.Count(p => ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) < 5);
        Assert.True(land.Average(p => up.Heights[p]) > land.Average(p => plain.Heights[p]) + 5, "the high ground broadened");
        Assert.True(land.Average(p => down.Heights[p]) < land.Average(p => plain.Heights[p]) - 5, "the low ground broadened");
        Assert.True(Flat(up) > 2 * Flat(plain), $"{Flat(up)} flat against {Flat(plain)}: plateaus");
        Assert.DoesNotContain(land, p => up.Heights[p] < 1 - 1e-3f || down.Heights[p] < 1 - 1e-3f);   // no land under the sea
    }

    [Fact]
    public void TheWeatherPaintsWhatItLaidDownAndTheCrestsRock()
    {
        var plain = Make(Steep);
        var worn = Make(Steep with { Erosion = 1 });
        // where the rain laid ground down, more earth (the second layer) than there was before
        var laid = Enumerable.Range(0, plain.Heights.Length).Where(p => worn.Heights[p] > plain.Heights[p] + 1.5f && plain.Heights[p] > 5).ToList();
        Assert.True(laid.Count > 100, $"{laid.Count}");
        Assert.True(laid.Average(p => (float)worn.Mask[(p * 4) + 1]) > laid.Average(p => (float)plain.Mask[(p * 4) + 1]) + 30, "earth on what was laid down");
        // on rolling ground (little of it steep enough for rock) the crests and knolls are rocked
        var rolling = Steep with { Low = 20, High = 80, Coast = 0 };
        long Rock(ZoneGenerator.Zone z) => Enumerable.Range(0, z.Heights.Length).Sum(p => (long)z.Mask[(p * 4) + 2]);
        long bare = Rock(Make(rolling)), crested = Rock(Make(rolling with { Crests = 1 }));
        Assert.True(crested > bare * 3 / 2, $"{crested} rock against {bare}");
    }

    [Fact]
    public void ValleysCutDownFromTheHighGroundLayItLowerAndLeaveTheSea()
    {
        var plain = Make(Steep, 800);
        var cut = Make(Steep with { Valleys = 2 }, 800);
        int n = plain.Heights.Length;
        Assert.All(Enumerable.Range(0, n).Where(p => plain.Heights[p] <= 1), p => Assert.Equal(plain.Heights[p], cut.Heights[p]));
        // cut deep in places (valleys), barely on most of the ground (the hillsides between them), and some of it laid
        // down again lower (fans, valley floors), less than was cut (the rest carried to the sea)
        float[] lost = [.. Enumerable.Range(0, n).Select(p => plain.Heights[p] - cut.Heights[p]).Order()];
        Assert.True(lost[n * 99 / 100] > 6, $"deepest hundredth {lost[n * 99 / 100]}");
        Assert.True(MathF.Abs(lost[n / 2]) < lost[n * 99 / 100] / 5, $"middle {lost[n / 2]}");
        double laid = lost.Where(v => v < 0).Sum(v => -(double)v), gone = lost.Where(v => v > 0).Sum(v => (double)v);
        Assert.True(laid > gone * 0.05 && laid < gone, $"{laid:F0} m³ laid of {gone:F0} cut");
        int raised = Enumerable.Range(0, n).Count(p => cut.Heights[p] > plain.Heights[p] + 1);
        Assert.True(raised > 0 && Enumerable.Range(0, n).Where(p => cut.Heights[p] > plain.Heights[p] + 1).Average(p => (double)plain.Heights[p])
            < Enumerable.Range(0, n).Where(p => plain.Heights[p] > 1).Average(p => (double)plain.Heights[p]), "laid on the lower ground");
    }

    [Fact]
    public void StreamsPaintEarthWhereTheWaterGathersAndSnowSlidesOffTheSteep()
    {
        var rolling = Steep with { Low = 20, High = 80, Coast = 0, Woods = 0, OpenDirt = 0 };
        var dry = Make(rolling);
        var wet = Make(rolling with { Streams = 1 });
        long Earth(ZoneGenerator.Zone z) => Enumerable.Range(0, z.Heights.Length).Sum(p => (long)z.Mask[(p * 4) + 1]);
        Assert.True(Earth(wet) > Earth(dry) + (100L * 255), $"{Earth(wet)} against {Earth(dry)}");
        Assert.True(Earth(wet) < 255L * wet.Heights.Length / 3, "streams, not the whole map");
        // snow on the steep heights gone, and some in hollows below the line
        var snowy = Steep with { SnowLine = 150 };
        var held = Make(snowy);
        var slid = Make(snowy with { SnowSlip = 25 });
        long Snow(ZoneGenerator.Zone z, Func<int, bool> where) => Enumerable.Range(0, z.Heights.Length).Where(where).Sum(p => (long)z.Mask[p * 4]);
        bool Steepest(int p) => held.Heights[p] > 190 && p % 400 > 0 && p % 400 < 399 && p > 400 && p < held.Heights.Length - 400
            && MathF.Abs(held.Heights[p + 1] - held.Heights[p - 1]) + MathF.Abs(held.Heights[p + 400] - held.Heights[p - 400]) > 2.5f;
        Assert.True(Snow(slid, Steepest) < Snow(held, Steepest) / 2, "off the steep");
        Assert.True(Snow(slid, p => held.Heights[p] is > 115 and < 135) > Snow(held, p => held.Heights[p] is > 115 and < 135), "in the hollows below");
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
        Assert.True(length > 100, $"{length} m");   // from 90 m to 500 over the first eight seeds
    }

    [Fact]
    public void MeandersWindTheRiversFromSideToSide()
    {
        static float Length(ZoneGenerator.Zone z) => z.Rivers.Sum(r => r.Points.Zip(r.Points.Skip(1), (a, b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y))).Sum());
        var spec = new ZoneGenerator.Spec(Low: 5, High: 120, RimHeight: 150, LakeHectares: [], Seed: 4, Valleys: 1, Rivers: 3);
        var straight = Make(spec, 1500);
        var winding = Make(spec with { Meander = 1 }, 1500);
        var (a, b) = (Length(straight), Length(winding));
        // the same rivers, longer for their bends (though a bend climbing a valley's side can end one sooner, in a pond),
        // still falling all the way and inside the map
        Assert.True(b > a * 1.08f, $"{a} m straight, {b} m winding");
        Assert.All(winding.Rivers, r => Assert.True(r.Top >= r.End - 0.01f));
        Assert.All(winding.Rivers.SelectMany(r => r.Points), p => Assert.True(p.X >= -200 && p.X <= 1300 && p.Y >= -200 && p.Y <= 1300));
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
    public void CliffPiecesKeepToLongCliffLinesNotRoughBumps()
    {
        var cliff = new MapTools.Kind("Cliff_0", "Cliff", CliffPiece.Category, 1, 1, null);
        var kinds = Kinds with { Cliffs = [cliff] };
        List<(string Model, Vector3 At, float Yaw, float Scale)> Pieces(ZoneGenerator.Spec s) =>
            [.. ZoneGenerator.Generate(s, 500, 500, -200, -200, kinds).Objects.Where(o => o.Model == "Cliff_0")];
        // rough bumps on gentle hills: steep here and there, but no cliff anywhere
        var bumpy = Pieces(new ZoneGenerator.Spec(Low: 0, High: 40, HillSize: 300, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Rough: 8, CliffSlope: 35));
        Assert.True(bumpy.Count <= 10, $"{bumpy.Count} pieces on rough ground");
        // the rim's mountains: painted rock, no steps (terraced ground under them or not)
        var rim = Pieces(new ZoneGenerator.Spec(Low: 0, High: 50, HillSize: 250, RimHeight: 300, RimWidth: 200, LakeHectares: [], Woods: 0, Small: 0, Rough: 3, Terraces: 20, CliffSlope: 35));
        Assert.DoesNotContain(rim, p => MathF.Min(MathF.Min(p.At.X + 200, 299 - p.At.X), MathF.Min(-p.At.Z + 200, 299 + p.At.Z)) < 200 * 0.4f * 0.8f);
        // terraces: lines of rock along each step, no piece alone
        var stepped = Pieces(new ZoneGenerator.Spec(Low: 0, High: 50, HillSize: 250, RimHeight: 0, LakeHectares: [], Woods: 0, Small: 0, Rough: 3, Terraces: 20, CliffSlope: 35));
        Assert.True(stepped.Count > 20, $"{stepped.Count} pieces on terraces");
        int alone = stepped.Count(p => !stepped.Any(q => q != p && Vector3.Distance(q.At, p.At) < CliffPiece.Width * MathF.Max(p.Scale, q.Scale) * 1.5f));
        Assert.True(alone <= stepped.Count / 10, $"{alone} of {stepped.Count} pieces with no neighbour along their line");
    }

    [Fact]
    public void FloatersHangHighAboveTheGroundAndSnowLiesHigh()
    {
        var z = Make(new ZoneGenerator.Spec(Low: 20, High: 200, RimHeight: 0, LakeHectares: [], Woods: 0, OpenTrees: 0, Small: 0, Floaters: 6, SnowLine: 150));
        var up = z.Objects.Where(o => o.Model == "Rock").ToList();
        Assert.Equal(6, up.Count);
        Assert.All(up, o => Assert.True(o.At.Y - Height(z, new Vector2(o.At.X, -o.At.Z)) > 40));
        int high = Enumerable.Range(0, z.Heights.Length).First(p => z.Heights[p] > 180 && ZoneGenerator.Slope(z.Heights, 400, 400, p % 400, p / 400) < 20);
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
        Assert.InRange(wet, 0.02f, 0.45f);   // a share that changes much from seed to seed (2 to 18% over the first eight)
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
