using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Copy: what a copy takes from the ground and the objects, what a copy keeps and how a copy of no outline repeats.
/// </summary>
public class ClipboardCopyTests
{
    // A copy without an outline (objects only) is pasted and repeated by its grid's size: the step
    // between copies took the outline's and threw on an empty one.
    [Fact]
    public void ACopyWithoutAnOutlineRepeatsByItsSize()
    {
        var paste = new PasteTool { Clip = new CopyData { W = 3, H = 2, Rel = [.. Enumerable.Repeat(0f, 6)], Wt = [.. Enumerable.Repeat(1f, 6)], Pnt = [.. Enumerable.Repeat(-1f, 24)], Objects = [], Poly = [] } };
        Assert.Equal(paste.CopyAt(new Vector2(40, 40), 0).At + new Vector2(2 + paste.Gap, 0), paste.CopyAt(new Vector2(40, 40), 1).At);
    }
}
