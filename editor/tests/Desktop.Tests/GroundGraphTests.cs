using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Ground graph: worked out, kept as JSON, its wires checked, its nodes' fields kept and reused.</summary>
public class GroundGraphTests
{
    private static readonly GroundNodes.Where At = new(300, 200, 0, 0);

    [Fact]
    public void TheStarterMakesWornHills()
    {
        var g = GroundGraph.Starter();
        var (field, problem) = GroundNodes.Run(g, At);
        Assert.Null(problem);
        Assert.Equal(300 * 200, field!.Length);
        var (hills, _) = GroundNodes.Run(g, At, upTo: g.Nodes.Single(n => n.Kind == "hills").Id);
        Assert.NotEqual(hills, field);
        Assert.InRange(field.Average(), 10, 130);
    }

    [Fact]
    public void ItIsKeptAsJson()
    {
        var g = GroundGraph.Starter();
        g.Nodes[0].Values["high"] = 222;
        var formula = g.Add("formula", 5, 6);
        formula.Text = "h * sin(x / 10)";
        var back = GroundGraph.FromJson(g.ToJson());
        Assert.Equal(g.ToJson().ToJsonString(), back.ToJson().ToJsonString());
        Assert.Equal(222, GroundNodes.Value(back.Nodes[0], "high"));
    }

    [Fact]
    public void ALoopIsRefusedAndAnUnjoinedInputSaysSo()
    {
        var g = GroundGraph.Starter();
        int hills = g.Nodes.Single(n => n.Kind == "hills").Id, erosion = g.Nodes.Single(n => n.Kind == "erosion").Id;
        var smooth = g.Add("smooth", 0, 0);
        Assert.True(g.Join(erosion, smooth.Id, 0));
        Assert.False(g.Join(smooth.Id, g.Nodes.Single(n => n.Kind == "valleys").Id, 0) && g.Feeds(erosion, erosion));
        g.Wires.RemoveAll(w => w.From == hills);
        var (field, problem) = GroundNodes.Run(g, At);
        Assert.Null(field);
        Assert.Contains("not joined", problem);
    }

    [Fact]
    public void AChangeLateInTheGraphReusesWhatCameBefore()
    {
        var g = GroundGraph.Starter();
        var cache = new FieldCache();
        _ = GroundNodes.Run(g, At, cache);
        int kept = cache.Count;
        g.Nodes.Single(n => n.Kind == "erosion").Values["strength"] = 0.3f;
        var (again, _) = GroundNodes.Run(g, At, cache);
        // only the erosion and the output made anew
        Assert.Equal(kept + 2, cache.Count);
        Assert.Equal(GroundNodes.Run(g, At).Field, again);
    }

    [Fact]
    public void BlendTakesTheMasksShare()
    {
        var g = new GroundGraph();
        var low = g.Add("flat", 0, 0);
        low.Values["height"] = 0;
        var high = g.Add("flat", 0, 0);
        high.Values["height"] = 100;
        var heights = g.Add("formula", 0, 0);
        heights.Text = "x";   // -150 in the west to 150 in the east
        var mask = g.Add("heightmask", 0, 0);
        (mask.Values["from"], mask.Values["to"], mask.Values["soft"]) = (0, 1000, 0);
        var blend = g.Add("blend", 0, 0);
        var output = g.Add("output", 0, 0);
        Assert.True(g.Join(heights.Id, mask.Id, 0) && g.Join(low.Id, blend.Id, 0) && g.Join(high.Id, blend.Id, 1) && g.Join(mask.Id, blend.Id, 2) && g.Join(blend.Id, output.Id, 0));
        var (field, problem) = GroundNodes.Run(g, At);
        Assert.Null(problem);
        Assert.Equal(0, field![10]);   // the west: under 0
        Assert.Equal(100, field[290]);   // the east
    }

    [Fact]
    public void AMountainStandsWhereItIsPutNorthUp()
    {
        var g = new GroundGraph();
        var m = g.Add("mountain", 0, 0);
        (m.Values["z"], m.Values["radius"]) = (50, 40);
        var output = g.Add("output", 0, 0);
        _ = g.Join(m.Id, output.Id, 0);
        float[] f = GroundNodes.Run(g, At).Field!;
        // 50 m north of the middle: row 50 (the first row is the north edge), column 150
        Assert.True(f[(50 * 300) + 150] > f[(150 * 300) + 150] + 20);
    }
}
