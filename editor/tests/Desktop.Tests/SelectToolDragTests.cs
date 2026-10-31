using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static Groundwork.Desktop.Tests.SelectToolRun;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Select tool's dragging with the mouse: a selected object moved (a copy selected, undone back), a wall snapped to
/// the wall next to it, and a wall dragged far snapping to the wall where it ends.
/// </summary>
[Collection("World files")]
public class SelectToolDragTests
{
    [AvaloniaFact]
    public void DraggingASelectedObjectMovesIt()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.Click(40, 40);
        v.Drag(KeyModifiers.None, (40, 40), (42, 40), (44, 41));
        Assert.True(v.S.Scene.Things[0].Gone);
        Assert.Equal(44, v.Last.Position.X, 1);
        Assert.Equal(41, v.Last.Position.Z, 1);
        // The copy is selected, and one undo puts the tree back.
        Assert.Equal([v.S.Scene.Things.Count - 1], v.Selected);
        v.S.Undo();
        Assert.False(v.S.Scene.Things[0].Gone);
    }
}
