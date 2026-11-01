using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>How many steps are not saved: where the history stands against where it stood at the save.</summary>
public class UnsavedStepsTests
{
    private static void Raise(EditSession s, int p) => s.EditGround("Raise", g =>
    {
        g.SetHeight(p, g.HeightOf(p) + 1);
        return ([p], (0, 0, 2, 2));
    });

    [Fact]
    public void StepsMadeAfterTheSaveCount()
    {
        var s = TestScenes.Flat(1);
        var (top, count) = (s.TopStep, s.UndoList.Count);
        Raise(s, 70);
        Raise(s, 71);
        Assert.Equal(2, EditSession.UnsavedSteps(s.UndoList, top, count));
    }

    [Fact]
    public void UndoingBackToTheSaveLeavesNothingUnsaved()
    {
        var s = TestScenes.Flat(1);
        var (top, count) = (s.TopStep, s.UndoList.Count);
        Raise(s, 70);
        Raise(s, 71);
        _ = s.Undo();
        _ = s.Undo();
        Assert.Equal(0, EditSession.UnsavedSteps(s.UndoList, top, count));
    }

    [Fact]
    public void UndoingPastTheSaveIsUnsaved()
    {
        var s = TestScenes.Flat(1);
        Raise(s, 70);
        var (top, count) = (s.TopStep, s.UndoList.Count);
        _ = s.Undo();
        Assert.Equal(1, EditSession.UnsavedSteps(s.UndoList, top, count));
    }
}
