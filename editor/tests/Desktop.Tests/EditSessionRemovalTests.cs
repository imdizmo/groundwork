using System.Numerics;
using Xunit;
using static Groundwork.Desktop.Tests.TestThings;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Taking steps out of the history: what a removal keeps of later steps (their modified flags, their marks on zones
/// and their moves), and the refusals when a later step depends on it.
/// </summary>
public class EditSessionRemovalTests
{
    // Taking out a step keeps the modified flag a later step set at its points: here a paint, then a flatten back to
    // the original height (modified, no level). Removing the paint cleared the flatten's flag.
    [Fact]
    public void RemovingAStepKeepsALaterStepsModifiedFlag()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        s.EditGround("Paint", g =>
        {
            g.PMod[p] = 1;
            g.Paint[p * 4] = 1;
            return ([p], (31, 31, 33, 33));
        });
        s.EditGround("Flatten", g =>
        {
            g.SetHeight(p, g.HeightOf(p));
            return ([p], (31, 31, 33, 33));
        });
        Assert.Equal(1, s.Ground.Mod[p]);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[0]).Shown);
        Assert.Equal(1, s.Ground.Mod[p]);
    }

    // A move replaces its object by a copy. Removing the move while a later step moved the copy again would bring the
    // original back beside the later copy (two rocks): it is refused, and neither rock changes.
    [Fact]
    public void RemovingAMoveWhoseCopyMovedAgainIsRefused()
    {
        var s = TestScenes.Flat(1, Rock());
        int first = s.Move([(0, new Vector3(4, 30, 0), Vector3.Zero)], "Claude: moved")[0];
        int second = s.Move([(first, new Vector3(8, 30, 0), Vector3.Zero)])[0];
        Assert.StartsWith("That change cannot be removed:", s.RemoveChange(s.UndoList[^2]).Shown);
        Assert.False(s.UndoList[^2].Removed);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.True(s.Scene.Things[first].Gone);
        Assert.False(s.Scene.Things[second].Gone);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
        // Taking the later move out first makes the first one removable.
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]).Shown);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^3]).Shown);
        Assert.False(s.Scene.Things[0].Gone);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
    }
}
