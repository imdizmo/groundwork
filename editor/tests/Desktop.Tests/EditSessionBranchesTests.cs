using System.Numerics;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// An edit session's remaining branches: the history's one-line description, which objects count as natural, a masked
/// shape, moving copies (their data kept) and a step that changes nothing.
/// </summary>
public class EditSessionBranchesTests
{
    [Fact]
    public void AChangeSaysHowManyPointsAndObjects()
    {
        var none = new Ground.State([], [], [], [], []);
        var one = new EditSession.Change("a", [5, 6], none, none);
        Assert.Equal("2 ground points", one.Describe());
        var two = one with { Things = [(4, true, false), (7, false, true)] };
        Assert.Equal("2 ground points · 1 new · 1 removed", two.Describe());
        Assert.Equal("", (one with { Points = [] }).Describe());
    }

    [Fact]
    public void OnlyWildObjectsAreNatural()
    {
        var tree = new WorldScene.Thing(1, AssetIds.Of("Beech1"), Vector3.Zero, Vector3.Zero, 1, false);
        Assert.True(EditSession.Natural(tree));
        Assert.False(EditSession.Natural(tree with { Piece = true }));
        Assert.False(EditSession.Natural(new WorldScene.Thing(2, AssetIds.Of("stone_wall_4x2"), Vector3.Zero, Vector3.Zero, 1, false)));
    }

    [Fact]
    public void AMaskedShapeAsksTheMaskAtEveryPoint()
    {
        var s = TestScenes.Flat(1);
        s.Mask.On = true;
        s.Mask.HeightMax = 1000;
        var (touched, bad) = s.Shape(32, 32, _ => 1, 4, 0, "Shape");
        Assert.True(touched > 0);
        Assert.Equal(0, bad);
    }

    [Fact]
    public void AMovedObjectKeepsItsData()
    {
        var s = TestScenes.Flat(1);
        var data = new Dictionary<string, string> { ["route"] = "north" };
        int placed = s.Commit("Place", null, [], [(new NewObject(0, AssetIds.Of("Rock_4"), new Vector3(5, 30, 5), Vector3.Zero, 1, data), false)])[0];
        int moved = s.Move([(placed, new Vector3(8, 30, 8), Vector3.Zero)], "Moved")[0];
        var n = s.Edits.FindAdded(s.Scene.Things[moved].Id);
        Assert.NotNull(n);
        Assert.Equal("north", n.Data!["route"]);
        Assert.NotEqual(s.Scene.Things[placed].Id, s.Scene.Things[moved].Id);
        Assert.True(s.Scene.Things[placed].Gone);
        Assert.Equal("Moved", s.UndoLabel);
        // A thing the session did not place (no data): moved with none.
        s.Scene.Things.Add(new WorldScene.Thing(-999, AssetIds.Of("Rock_4"), new Vector3(2, 30, 2), Vector3.Zero, 1, false));
        int other = s.Move([(s.Scene.Things.Count - 1, new Vector3(3, 30, 3), Vector3.Zero)])[0];
        Assert.Null(s.Edits.FindAdded(s.Scene.Things[other].Id)!.Data);
    }

    [Fact]
    public void AStepThatChangesNothingIsNotKept()
    {
        var s = TestScenes.Flat(1);
        Assert.Empty(s.Commit("Nothing", gr => ([], (0, 0, 0, 0)), [], []));
        Assert.Empty(s.UndoList);
    }
}
