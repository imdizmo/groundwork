using System.Numerics;
using Groundwork.App;
using Groundwork.Sdk;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// An extension's tool's edit made one step of a map's history (ToolCommit): objects added with their data, a change a
/// copy of the object with the tool's values merged into its data (a null value taking a key away), a removal, and an
/// object the editor did not hand over left alone and said.
/// </summary>
public sealed class ToolCommitTests
{
    [Fact]
    public void AnEditsObjectsComeWithTheirDataAsOneStep()
    {
        var s = TestScenes.Flat(1);
        var edit = new ToolEdit().Add("Canal", new Vector3(5, 30, 5), Vector3.Zero, new Dictionary<string, string> { ["path"] = "0,0;10,0", ["width"] = "3" });
        var plan = ToolCommit.Apply(s, "Dig", edit);

        Assert.Empty(plan.Skipped);
        var t = s.Scene.Things[^1];
        Assert.Equal("Canal", AssetIds.NameOf(t.Prefab));
        Assert.Equal("0,0;10,0", s.Edits.FindAdded(t.Id)!.Data!["path"]);
        Assert.Equal("Dig", s.UndoLabel);
        Assert.True(s.Undo());
        Assert.True(s.Scene.Things[^1].Gone);
    }

    [Fact]
    public void AChangeIsACopyWithTheToolsValuesMergedIn()
    {
        var s = TestScenes.Flat(1);
        ToolCommit.Apply(s, "Dig", new ToolEdit().Add("Canal", new Vector3(5, 30, 5), Vector3.Zero, new Dictionary<string, string> { ["path"] = "a", ["width"] = "3" }));
        var canal = s.ViewOf(s.Scene.Things[^1]);

        ToolCommit.Apply(s, "Widen", new ToolEdit().Change(canal, new Dictionary<string, string?> { ["width"] = "5", ["path"] = null, ["depth"] = "2" }));

        Assert.True(s.Scene.Things[^2].Gone);
        var data = s.Edits.FindAdded(s.Scene.Things[^1].Id)!.Data!;
        Assert.Equal("5", data["width"]);
        Assert.Equal("2", data["depth"]);
        Assert.False(data.ContainsKey("path"));
        Assert.Equal(new Vector3(5, 30, 5), s.Scene.Things[^1].Position);
    }

    [Fact]
    public void ARemovalTakesTheObjectAwayAndAnObjectNotHandedOverIsSaid()
    {
        var s = TestScenes.Flat(1);
        ToolCommit.Apply(s, "Dig", new ToolEdit().Add("Canal", new Vector3(5, 30, 5), Vector3.Zero));
        var canal = s.ViewOf(s.Scene.Things[^1]);
        var stranger = new ObjectView("Stray", Vector3.Zero, Vector3.Zero);

        var plan = ToolCommit.Apply(s, "Fill", new ToolEdit().Remove(canal).Remove(stranger));

        Assert.True(s.Scene.Things[^1].Gone);
        Assert.Equal(["Stray"], plan.Skipped);
    }
}
