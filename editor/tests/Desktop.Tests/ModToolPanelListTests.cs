using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Groundwork.App;
using Groundwork.Sdk;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A tool card's list and its sliders set by the tool (the map's rivers and lakes): the cards it is
/// given, one picked, a click picking another and telling the tool; a slider moved by the tool not counted as a change.
/// </summary>
public sealed class ModToolPanelListTests
{
    private sealed class Lister : IEditorTool
    {
        public ToolInfo Info { get; } = new("lister", "Lister", "Lists.");

        public IReadOnlyList<ToolControl> Controls { get; init; } = [new ListControl("body", "Bodies"), new SliderControl("width", "Width", 0, 40, 6)];

        public void OnChosen(IToolContext ctx)
        {
        }

        public void OnLeft(IToolContext ctx)
        {
        }

        public void OnPointer(IToolContext ctx, PointerInput input)
        {
        }

        public bool OnKey(IToolContext ctx, string key, KeyModifiers mods) => false;

        public void OnControl(IToolContext ctx, string id)
        {
        }
    }

    [AvaloniaFact]
    public void TheListPicksItsCardsAndASliderSetByTheToolIsNotAChange()
    {
        var panel = new ModToolPanel(null);
        panel.Show(ModExtensions.BuiltIn("Test", new Lister()));
        var changed = new List<string>();
        panel.Changed += changed.Add;

        panel.SetList("body", [("Creek", "16 points", "River"), ("Hill tarn", "9 points", "Lake")], 1);
        panel.SetNumber("width", 4.5f);

        var cards = panel.Card.GetLogicalDescendants().OfType<ToggleButton>().ToList();
        Assert.Equal([false, true], cards.Select(c => c.IsChecked == true));
        Assert.Equal((1, 4.5f), (panel.Choice("body"), panel.Number("width")));
        Assert.Empty(changed);

        cards[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["body"], changed);
        Assert.Equal(0, panel.Choice("body"));
        Assert.Equal([true, false], panel.Card.GetLogicalDescendants().OfType<ToggleButton>().Select(c => c.IsChecked == true));
    }

    [AvaloniaFact]
    public void APalettePickedElsewhereIsHiddenAndFollowsItsPicks()
    {
        var panel = new ModToolPanel(null) { PalettesElsewhere = true };
        panel.Show(ModExtensions.BuiltIn("Test", new Lister { Controls = [new PaletteControl("kinds", "Kinds", [("Trees", "Pine", null), ("Trees", "Birch", null)]) { Multi = true }] }));
        var changed = new List<string>();
        panel.Changed += changed.Add;

        Assert.DoesNotContain(panel.Card.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Pine" && t.IsEffectivelyVisible);
        panel.SetPicked("kinds", [0, 1], quiet: true);
        Assert.Empty(changed);
        panel.SetPicked("kinds", [1]);

        Assert.Equal([1], panel.Picked("kinds"));
        Assert.Equal(["kinds"], changed);
    }
}
