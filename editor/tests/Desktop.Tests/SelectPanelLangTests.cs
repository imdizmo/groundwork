using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Select tool in another language (the made-up one: PseudoLang): its panel shows only catalogue texts with nothing
/// selected, two objects selected, Move by on and a selection kept; the questions it asks (a selection's name,
/// forgetting it, a builder's id) too; a tool's message is shown in that language while Claude reads its English; and
/// so are the window's Built by choices and messages, Make player built, a runestone's Inspect and Replace's, and the
/// name the Replace pick button gives the eyedropper.
/// </summary>
[Collection("DataDir")]
public sealed class SelectPanelLangTests : SavedSelectionsFolder
{

    /// <summary>A shown window, made in the pseudo language, editing a flat area with two trees, in the Select tool.</summary>
    private static MainWindow Window()
    {
        var w = TestWindows.Editing(TestScenes.Flat(
            2,
            TestScenes.Beech(1, new Vector3(40, 30, 40)),
            TestScenes.Beech(2, new Vector3(44, 30, 40))));
        w.Tools.ChooseSelect();
        w.SelectPanel.WorldName = () => "World one";
        return w;
    }

    /// <summary>The kinds Replace with lists: game data, shown as the game names them.</summary>
    private static string[] Kinds(SelectPanel p) => [.. p.ReplaceBox.Items.OfType<string>()];

    [AvaloniaFact]
    public async Task SelectPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = Window();
        var p = w.SelectPanel;
        p.FillSaved();
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));

        w.View.Select([0, 1]);
        p.Refresh();
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));
        p.ByButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));

        p.AskName = Task.FromResult<string?>;
        await p.Keep();
        Assert.Equal(1, p.SavedBox.ItemCount);
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));
    }

    [AvaloniaFact]
    public void ASelectMessageIsShownInTheLanguageAndClaudeReadsEnglish()
    {
        var w = Window();
        using var pseudo = new PseudoLang();
        w.View.Select([0]);

        w.SelectPanel.DeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text, StringComparison.Ordinal);
        Assert.Equal("Deleted 1 object(s). Ctrl+Z brings them back; they leave the map when it is saved.", w.LastMessageEn);
    }

    [AvaloniaFact]
    public void ThePickButtonNamesReplaceInTheLanguage()
    {
        var w = Window();
        using var pseudo = new PseudoLang();
        string? asked = null;
        w.SelectPanel.PickKindAsked += (label, _) => asked = label;

        w.SelectPanel.ReplacePickButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(PseudoLang.Wrap("Replace"), asked);
    }
}
