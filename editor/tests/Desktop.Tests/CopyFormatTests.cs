using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Copies kept as files (in the web editor's format): to JSON and back, heights past the old limit kept, an object's
/// data going with its copy, and the web editor's files read.
/// </summary>
public class CopyFormatTests
{
    /// <summary>
    /// A small 3 × 2 copy with ground, an empty cell, paint and objects.
    /// </summary>
    private static CopyData Sample() => new()
    {
        W = 3,
        H = 2,
        Rel = [0f, 0.5f, float.NaN, -1.25f, 2, 0],
        Wt = [1f, 1, 0, 0.5f, 1, 1],
        Pnt = [-1f, -1, -1, -1, 1, 0, 0, 1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 1, 1, -1, -1, -1, -1],
        Objects = [new(AssetIds.Of("woodwall"), "woodwall", 1, -0.5f, 0.25f, new Vector3(0, 90, 0), 0), new(AssetIds.Of("Beech1"), "Beech1", -1, 0, 0, Vector3.Zero, 1.1f, Follow: true)],
        Poly = [new(-2, -1), new(2, -1), new(2, 1), new(-2, 1)],
        Name = "Sample",
    };

    [Fact]
    public void ACopyGoesToJsonAndBack()
    {
        var c = Sample();
        var back = CopyFormat.FromJson(CopyFormat.ToJson(c));
        Assert.Equal(c.W, back.W);
        Assert.True(float.IsNaN(back.Rel[2]));
        Assert.Equal(-1.25f, back.Rel[3], 3);
        Assert.Equal(0.5f, back.Wt[3], 2);
        Assert.Equal(-1, back.Pnt[0]);
        Assert.Equal(1, back.Pnt[4], 2);
        Assert.Equal(c.Objects, back.Objects);
        Assert.Equal(c.Poly, back.Poly);
        Assert.StartsWith("data:image/png;base64,", CopyFormat.Thumb(c));
    }

    // Heights past ±327.67 m (a copy off a tall No-limit mountain) are kept as they are, not cut there; only the "no
    // ground" marker's value is moved a centimetre.
    [Fact]
    public void HeightsPastTheOldLimitAreKept()
    {
        var c = Sample();
        c.Rel[0] = 500f;
        c.Rel[1] = -400f;
        c.Rel[3] = -327.68f;
        var back = CopyFormat.FromJson(CopyFormat.ToJson(c));
        Assert.Equal(500f, back.Rel[0], 2);
        Assert.Equal(-400f, back.Rel[1], 2);
        Assert.Equal(-327.67f, back.Rel[3], 2);
    }

    // What extensions keep on an object goes with its copy, text by key, and comes back as it was.
    [Fact]
    public void AnObjectsDataGoesWithItsCopy()
    {
        var c = Sample();
        c.Objects[0] = c.Objects[0] with { Data = new Dictionary<string, string> { ["route"] = "north", ["n"] = "3" } };
        var back = CopyFormat.FromJson(CopyFormat.ToJson(c)).Objects[0].Data!;
        Assert.Equal("north", back["route"]);
        Assert.Equal("3", back["n"]);
        Assert.Null(CopyFormat.FromJson(CopyFormat.ToJson(c)).Objects[1].Data);
    }

    [Fact]
    public void TheWebEditorsCopyFilesAreRead()
    {
        // As the web editor's encodeClip writes it.
        var json = JsonNode.Parse("""{"w":1,"h":1,"rel":[-32768],"wt":[0],"pnt":[-255,-255,-255,-255],"objects":[{"name":"stone_wall_2x1","dx":0.5,"dz":0,"dy":1,"rx":0,"ry":180,"rz":0,"scale":0,"sourceId":null}],"poly":[{"gx":-1,"gz":-1},{"gx":1,"gz":-1},{"gx":1,"gz":1}]}""")!.AsObject();
        var c = CopyFormat.FromJson(json, "Wall");
        var o = Assert.Single(c.Objects);
        Assert.Equal(AssetIds.Of("stone_wall_2x1"), o.Prefab);
        Assert.Equal(180, o.Rotation.Y);
        Assert.False(o.Follow);
        Assert.Equal(3, c.Poly.Count);
        Assert.Equal("Wall", c.Name);
    }

    // A hand-edited or older copy file whose ground arrays are not as long as its size says (or missing), whose size
    // is not a grid or too large to paste, with a nameless object or an outline point that is not one is refused as it is read: pasted, it
    // stopped half-way through.
    [Theory]
    [InlineData("""{"w":2,"h":2,"rel":[0,0,0],"wt":[0,0,0,0],"pnt":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"objects":[]}""", "\"rel\" list holds 3 values, not the 4")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0,0],"pnt":[0,0,0,0],"objects":[]}""", "\"wt\" list holds 2 values, not the 1")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"objects":[]}""", "no \"pnt\" list")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0],"objects":[]}""", "\"pnt\" list holds 3 values, not the 4")]
    [InlineData("""{"w":0,"h":1,"rel":[],"wt":[],"pnt":[],"objects":[]}""", "size (0 × 1)")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0]}""", "no objects list")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[{"dx":1}]}""", "has no name")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[{"name":" "}]}""", "has no name")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":0,"gz":0},null]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":0}]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":"a","gz":0}]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":1e40,"gz":0}]}""", "outline is not a point")]
    [InlineData("""{"w":50000,"h":10}""", "size (50000 × 10) is too large")]
    public void ACopyThatDoesNotHoldTogetherIsRefused(string text, string why)
    {
        var e = Assert.Throws<FormatException>(() => CopyFormat.FromJson(JsonNode.Parse(text)!.AsObject()));
        Assert.Contains(why, e.Message, StringComparison.Ordinal);
    }
}
