using Avalonia;
using Avalonia.Controls;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The map's Brush and Biome: the Mask is the last section of their card ("Only where…"), not a card beside it;
    /// for every other tool it is its own card again.
    /// </summary>
    private void MaskInCard(bool inside)
    {
        var card = (Border)MaskPanel.Card;
        if (inside == ReferenceEquals(ModToolPanel.Tail, card))
        {
            return;
        }
        if (inside)
        {
            ModToolPanel.Tail = card;
            card.Background = null;
            card.BorderThickness = new Thickness(0, 1, 0, 0);
            card.Padding = new Thickness(0, 8, 0, 0);
            card.CornerRadius = default;
            card.BoxShadow = default;
            MaskPanel.OnBox.Content = "Only where… (height, slope, ground colour)";
            return;
        }
        ModToolPanel.Tail = null;
        _toolColumn.Children.Add(card);
        Ui.AsCard(card);
        MaskPanel.OnBox.Content = Lang.T("mask.label.on");
    }

    /// <summary>
    /// Another tool chosen: the 3D view takes it, its panel shows and the others hide.
    /// </summary>
    /// <param name="t">The brush chosen, if the tool is a brush.</param>
    private void OnToolChanged(BrushTool? t)
    {
        MapHints();
        View.Tool = t;
        View.Mode = Tools.Mode;
        ModToolChanged();
        SelectPanel.Card.IsVisible = Tools.SelectMode;
        MeasurePanel.Card.IsVisible = Tools.Mode == ToolMode.Measure;
        if (Tools.Mode == ToolMode.Measure)
        {
            // The ground may have changed since the tape was drawn.
            MeasurePanel.Refresh();
        }
        PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
        // The Mask limits the brushes; the map's Brush and Biome have it as the last section of their card.
        bool maskInCard = InMap && Tools.Mode == ToolMode.Mod && Tools.ModTool?.Info.Id is "brush" or "biome";
        MaskPanel.Card.IsVisible = Tools.Mode == ToolMode.Brush || maskInCard;
        MaskInCard(maskInCard);
    }
}
