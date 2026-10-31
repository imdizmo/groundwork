using Avalonia.Headless.XUnit;
using Xunit;
using static Groundwork.Desktop.Tests.SelectToolRun;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Select tool's double clicks: a whole building, a whole ship (the example vehicle mod's, its parts moved and
/// deleted together), and a chest on a building's floor.
/// </summary>
[Collection("World files")]
public class SelectToolBuildingTests
{
    [AvaloniaFact]
    public void ADoubleClickSelectsTheWholeBuilding()
    {
        // Two walls end to end and a floor against them: one building; a wall further off is another.
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Wall, 55, 30, 55), T(5, Beech, 43.5f, 30, 40, false));
        v.Click(40, 40, clicks: 2);
        Assert.Equal([0, 1, 2], v.Selected);
        // From the button too, adding to what is selected.
        v.Click(55, 55);
        v.Tool.WholeBuilding();
        Assert.Equal([3], v.Selected);
        // A tree is not a building.
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.W.View.Select([4]);
        v.Tool.WholeBuilding();
        Assert.StartsWith("Select a building first", said);
    }
}
