using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Paste panel in another language (the made-up one: PseudoLang): with nothing copied, then with objects copied,
/// stacked, turned and mirrored, it shows only catalogue texts; and the window's copy, cut and paste messages and its
/// Paste History steps are in that language, while Claude reads the messages in English.
/// </summary>
[Collection("DataDir")]
public sealed class PastePanelLangTests
{
    [AvaloniaFact]
    public void PastePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = TestWindows.Editing(TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(40, 30, 40))));
        var p = w.PastePanel;
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.Copy();
        w.StartPaste();
        p.CopiesBox.Value = 3;
        p.MirrorButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        p.TurnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.View.Paste.Clip!.Name = "My hut";
        p.Refresh();
        Assert.StartsWith(PseudoLang.Open + "My hut: ", p.Info.Text, StringComparison.Ordinal);
        Assert.Empty(PseudoLang.Untranslated(p.Card, "My hut"));
    }
}
