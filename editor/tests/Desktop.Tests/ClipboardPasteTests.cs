using System.Numerics;
using Xunit;
using static Groundwork.Desktop.Tests.ClipboardSetup;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Paste: where a paste puts the copy and its ground, the ghost, repeats, the site cleared and the limit.
/// </summary>
public class ClipboardPasteTests
{

    // The ground goes in a whole metre at a time: the point pasted at moves (half a metre at most) so
    // that the copy's ground points land on the ground's, turned too, and its objects stand where they did.
    [Fact]
    public void ThePastePointLinesTheCopysGroundUp()
    {
        var clip = new CopyData { W = 2, H = 1, Rel = [0, 0], Wt = [1, 1], Pnt = [-1, -1, -1, -1, -1, -1, -1, -1], Objects = [], Poly = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)] };
        var paste = new PasteTool { Clip = clip };
        Assert.Equal(new Vector2(10.5f, 20), paste.CopyAt(new Vector2(10.3f, 20.2f), 0).At);
        paste.TurnBy(90);
        Assert.Equal(new Vector2(10, 20.5f), Round(paste.CopyAt(new Vector2(10.3f, 20.2f), 0).At));
    }
}
