using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;
using static Groundwork.Desktop.Tests.DialogsTests;

namespace Groundwork.Desktop.Tests;

/// <summary>The generator's window: a style and its sliders give a new map's numbers, or nothing; flat ground has none.</summary>
public class GeneratorWindowTests
{
    private static Window Owner()
    {
        var w = new Window { Width = 800, Height = 600 };
        w.Show();
        return w;
    }

    private static TextBox Value(Window d, string label) =>
        d.GetLogicalDescendants().OfType<Grid>().First(g => g.Children.OfType<TextBlock>().Any(t => t.Text == label)).Children.OfType<TextBox>().Single();

    [AvaloniaFact]
    public async Task ANewMapGetsItsStylesNumbersAndWhatIsTyped()
    {
        var owner = Owner();
        var asked = GeneratorWindow.NewMap(owner);
        var d = TestWindows.Shown(owner);
        var style = d.GetLogicalDescendants().OfType<ComboBox>().First(c => c.PlaceholderText == "As it was made");
        style.SelectedIndex = ZoneGenerator.Styles.ToList().FindIndex(s => s.Name == "Mesas and canyons");
        var box = Value(d, "Highest ground");
        box.Text = "2500";   // past the slider's end: kept
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        Press(d, "Make the map");
        var made = await asked;
        Assert.NotNull(made);
        var mesas = ZoneGenerator.Styles.Single(s => s.Name == "Mesas and canyons").Spec;
        Assert.Equal((8, mesas.Low, 2500f, mesas.Terraces), (made.Chunks, made.Spec!.Low, made.Spec.High, made.Spec.Terraces));
        Assert.True(made.Spec.RimWidth <= 505 * 0.2f + 0.01f, $"rim {made.Spec.RimWidth} on a 505 m map");

        asked = GeneratorWindow.NewMap(owner);
        Press(TestWindows.Shown(owner), "Cancel");
        Assert.Null(await asked);

        asked = GeneratorWindow.NewMap(owner);
        d = TestWindows.Shown(owner);
        d.GetLogicalDescendants().OfType<ComboBox>().First(c => c.PlaceholderText == "As it was made").SelectedIndex = ZoneGenerator.Styles.Count;   // flat
        Press(d, "Make the map");
        made = await asked;
        Assert.NotNull(made);
        Assert.Null(made.Spec);
        Assert.False(made.Rolling);
    }

    [Fact]
    public void ThePreviewIsTheGroundTheNumbersMake()
    {
        var mesh = GeneratorWindow.Make(new ZoneGenerator.Spec(Low: 10, High: 80, RimHeight: 0, LakeHectares: [2]), 505, false, 1, MapProject.NewLayers);
        Assert.Equal(505, mesh.Size);
        Assert.Equal(0, mesh.V.Length % 9);
        var heights = Enumerable.Range(0, mesh.V.Length / 9).Select(i => mesh.V[(i * 9) + 1]).ToList();
        Assert.InRange(heights.Max() - heights.Min(), 30, 90);
        Assert.Equal(0, mesh.I.Length % 3);
        Assert.True(mesh.I.Max() < mesh.V.Length / 9);
    }
}
