using Xunit;
using static Groundwork.Desktop.Tests.StrokeRun;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Undo and redo of strokes: the ground back and forth, what changed counted on the edits' version, a new stroke dropping
/// the redo.
/// </summary>
public class SculptHistoryTests
{
    [Fact]
    public void UndoAndRedoPutTheGroundBackAndForth()
    {
        var s = TestScenes.Flat();
        Stroke(s, BrushTool.Raise, 64, 64);
        float raised = At(s, 64, 64);
        Assert.True(s.CanUndo);
        Assert.Equal("Raise", s.UndoLabel);
        int version = s.Edits.Version;
        Assert.True(version > 0);
        Assert.True(s.Undo());
        Assert.Equal(30, At(s, 64, 64), 4);
        Assert.True(s.Edits.Version > version);
        Assert.True(s.CanRedo);
        Assert.True(s.Redo());
        Assert.Equal(raised, At(s, 64, 64), 4);
        // A new stroke after an undo drops the redo.
        s.Undo();
        Stroke(s, BrushTool.Lower, 20, 20);
        Assert.False(s.CanRedo);
    }
}
