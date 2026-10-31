using Avalonia.Headless.XUnit;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The View panel: objects sorted into kinds by the map's catalog, and switches that hide them.
/// </summary>
public class MainWindowViewPanelTests
{
    [Theory]
    [InlineData("Beech1", false, ObjectKind.Trees)]
    [InlineData("Rock_4", false, ObjectKind.Rocks)]
    [InlineData("Pickable_Mushroom", false, ObjectKind.Plants)]
    [InlineData("Bush01", false, ObjectKind.Bushes)]
    [InlineData("Player_tombstone", false, ObjectKind.Other)]
    [InlineData("Beech1", true, ObjectKind.Buildings)]
    [InlineData("woodwall", false, ObjectKind.Buildings)]
    [InlineData(null, false, ObjectKind.Other)]
    public void KindsAreTheCatalogs(string? name, bool built, ObjectKind want) => Assert.Equal(want, ObjectKinds.Of(name, built));

    [AvaloniaFact]
    public void SwitchesStartOnAndHideKinds()
    {
        var w = new MainWindow(load: false);
        w.Show();
        foreach (var k in ObjectKinds.All)
        {
            Assert.Equal(ObjectKinds.ShownAtFirst(k), w.KindBoxes[k].IsChecked);
            Assert.Equal(ObjectKinds.ShownAtFirst(k), w.View.IsShown(k));
        }
        w.KindBoxes[ObjectKind.Trees].IsChecked = false;
        Assert.False(w.View.IsShown(ObjectKind.Trees));
        w.KindBoxes[ObjectKind.Rocks].IsChecked = false;
        Assert.False(w.View.IsShown(ObjectKind.Rocks));
        w.WaterBox.IsChecked = false;
        Assert.False(w.View.ShowWater);
    }
}
