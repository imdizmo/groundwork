using System.Numerics;
using Avalonia.Headless.XUnit;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The History panel's remaining branches: taking Claude's changes back when refused, with nobody listening and with
/// none left.
/// </summary>
public class HistoryPanelBranchesTests
{
    /// <summary>A map with one step of Claude's (a hill) and one of the user's taken out.</summary>
    private static EditSession Carried()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: hill");
        s.Shape(20, 20, _ => 1, 3, 0, "Raise");
        s.RemoveChange(s.UndoList[1]);
        return s;
    }

    [AvaloniaFact]
    public async Task ClaudesChangesGoBackOnlyWhenAllowed()
    {
        await new HistoryPanel(() => null).TakeBackClaudes();
        var t = Carried();
        var p = new HistoryPanel(() => t) { Allow = _ => Task.FromResult(false) };
        await p.TakeBackClaudes();
        Assert.False(t.UndoList[0].Removed);
        // Allowed, with nobody listening for the message.
        p.Allow = _ => Task.FromResult(true);
        await p.TakeBackClaudes();
        Assert.True(t.UndoList[0].Removed);
        int steps = t.UndoList.Count;
        // None left: nothing more.
        await p.TakeBackClaudes();
        Assert.Equal(steps, t.UndoList.Count);
    }

    // During a brush stroke nothing can be taken out of the history: the message says so instead of counting the
    // steps as built on.
    [AvaloniaFact]
    public async Task NothingIsTakenBackDuringAStroke()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: raised");
        s.BeginStroke(BrushTool.Raise, 40, 40);
        string? said = await HistoryPanelRun.TakeBackClaudesSaid(s);
        Assert.False(s.UndoList[0].Removed);
        Assert.Equal("Nothing taken back: finish the brush stroke first.", said);
    }

    // Several taken back: each removal is a step of its own, and the message says Ctrl+Z brings them back one at a time.
    [AvaloniaFact]
    public async Task SeveralTakenBackAreSaidToComeBackOneAtATime()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: a hill");
        s.Shape(40, 40, _ => 1, 3, 0, "Claude: another");
        string? said = await HistoryPanelRun.TakeBackClaudesSaid(s);
        Assert.Equal("Took back 2 change(s) Claude made; yours are kept. Each is its own step: Ctrl+Z brings them back one at a time.", said);
        Assert.Equal(4, s.UndoList.Count);
    }

    // A change of Claude's that a later step of the user's built on (its copy moved again) stays, and the message
    // says so.
    [AvaloniaFact]
    public async Task AChangeOfClaudesTheUserMovedOnIsNotTakenBack()
    {
        var s = TestScenes.Flat(1, new WorldScene.Thing(3, AssetIds.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        int copy = s.Move([(0, new Vector3(4, 30, 0), Vector3.Zero)], "Claude: moved")[0];
        s.Move([(copy, new Vector3(8, 30, 0), Vector3.Zero)]);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: raised");
        string? said = await HistoryPanelRun.TakeBackClaudesSaid(s);
        Assert.False(s.UndoList[0].Removed);
        Assert.True(s.UndoList[2].Removed);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
        Assert.Equal("Took back 1 change(s) Claude made; yours are kept. 1 could not be taken back: a later step moved or deleted what they added (undo instead).", said);
    }
}
