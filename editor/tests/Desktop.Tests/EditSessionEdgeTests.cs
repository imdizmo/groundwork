using System.Numerics;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Edge cases of the edit session: the history's step limit and memory budget, calls outside a stroke, and what a
/// step's memory counts.
/// </summary>
[Collection("World files")]
public class EditSessionEdgeTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [Fact]
    public void TheHistoryKeepsTheStepsChosen()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 200;
        for (int i = 0; i < 205; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        Assert.Equal(200, s.UndoList.Count);
        Assert.Equal("change 5", s.UndoList[0].Label);
        // Deleting (another way into the history) keeps the limit too.
        var t = TestScenes.Flat(2, new WorldScene.Thing(1, AssetIds.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        t.KeepSteps = 200;
        for (int i = 0; i < 200; i++)
        {
            t.Shape(20, 20, Two, 1, 0, "x");
        }
        t.Delete([0]);
        Assert.Equal(200, t.UndoList.Count);
        Assert.Equal("Deleted 1", t.UndoLabel);
        // Fewer chosen since: the oldest go at the next change.
        t.KeepSteps = 3;
        t.Shape(20, 20, Two, 1, 0, "last");
        Assert.Equal(["x", "Deleted 1", "last"], t.UndoList.Select(c => c.Label));
    }

    [Fact]
    public void WithNoLimitEveryStepIsKept()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 0;
        for (int i = 0; i < 250; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        Assert.Equal(250, s.UndoList.Count);
        Assert.Equal("change 0", s.UndoList[0].Label);
    }

    [Fact]
    public void PastTheMemoryBudgetTheOldestGoButTheNewestStays()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 0;
        s.Shape(30, 30, Two, 3, 0, "one");
        long one = s.UndoList[0].Bytes;
        s.KeepBytes = one * 5 / 2;
        s.Shape(60, 60, Two, 3, 0, "two");
        s.Shape(90, 90, Two, 3, 0, "three");
        Assert.Equal(["two", "three"], s.UndoList.Select(c => c.Label));
        // A budget smaller than one step: that step still stays, and undoes.
        s.KeepBytes = 1;
        s.Shape(30, 90, Two, 3, 0, "four");
        Assert.Equal("four", Assert.Single(s.UndoList).Label);
        Assert.True(s.Undo());
    }

    [Fact]
    public void AStepsBytesCountItsPointsAndThings()
    {
        var s = TestScenes.Flat(2);
        s.Shape(30, 30, Two, 3, 0, "one");
        var c = s.UndoList[0];
        Assert.NotEmpty(c.Points);
        Assert.Equal(56L * c.Points.Length, c.Bytes);
        var more = c with { Things = [(0, true, false), (1, false, true)] };
        Assert.Equal(c.Bytes + 16, more.Bytes);
    }
}
