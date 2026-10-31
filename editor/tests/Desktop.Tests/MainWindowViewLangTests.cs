using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The View panel, the help card and the status bar's readouts in another language (the made-up one: PseudoLang),
/// with an area open and objects selected: they show only catalogue texts but for the world's builders (data); the
/// 3D resolution's message is shown in the language while Claude reads its English; the question before a save is
/// asked in it; and the walking and flying lines, a kind switched on, picking a kind and the paste height are said in
/// it with English for Claude; and so is the help card's frame rate line, given a report as the 3D view gives it.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowViewLangTests
{

    [AvaloniaFact]
    public void TheFrameRateLineIsInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        const string Rate = "60 frames/s · 4.5 ms of work each · 1,234 objects (5,678 model parts in 90 draws)";
        string Line() => Assert.Single(w.HelpCard.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("frames/s", StringComparison.Ordinal) == true).Text!;

        // Model kinds still loading: said after the rates, in the language too.
        w.ShowStats(new GlView.Stats(60, 4.5, 1234, 90, 5678, 3));
        Assert.Equal(PseudoLang.Wrap(Rate) + " " + PseudoLang.Wrap("· 3 kinds loading"), Line());
        Assert.Empty(PseudoLang.Untranslated(w.HelpCard));
        // Everything loaded: the rates alone.
        w.ShowStats(new GlView.Stats(60, 4.5, 1234, 90, 5678, 0));
        Assert.Equal(PseudoLang.Wrap(Rate), Line());
        w.Close();
    }
}
