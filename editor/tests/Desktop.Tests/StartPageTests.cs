using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The start page's cards: grouped by folder in the list's order, a gone map greyed, the search narrowing them, Enter
/// and the shortcuts, and the first steps while there are none.
/// </summary>
public sealed class StartPageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gw-start-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string Map(string under, string name) => MapProject.Create(name, 1, generated: false, height: 10, seed: 1, under: Path.Combine(_dir, under)).Folder;

    [AvaloniaFact]
    public void CardsAreGroupedByFolderInTheListsOrderAndAGoneMapIsGreyed()
    {
        string a = Map("one", "Alpha"), b = Map("two", "Beta"), c = Map("one", "Gamma"), gone = Path.Combine(_dir, "one", "Gone");
        var page = new StartPage(null, () => [a, b, c, gone]);

        // The first folder's maps first (in the list's order), then the second's.
        Assert.Equal([a, c, gone, b], page.Cards.Select(x => x.Folder));
        Assert.Equal(["Alpha", "Gamma", "Gone", "Beta"], page.Cards.Select(x => x.Name));
        Assert.Equal([true, true, false, true], page.Cards.Select(x => x.Found));
        Assert.True(page.Cards[2].Button.Opacity < 1);
        Assert.False(page.EmptyShown);
        // The card's menu asks the window to forget the map.
        string? removed = null;
        page.RemoveRequested += f => removed = f;
        var remove = Assert.IsType<Avalonia.Controls.MenuItem>(page.Cards[2].Button.ContextMenu!.Items[^1]);
        remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.MenuItem.ClickEvent));
        Assert.Equal(gone, removed);
    }

    [AvaloniaFact]
    public void TheSearchNarrowsTheCardsAndEnterOpensTheFirstShown()
    {
        string a = Map("one", "Alpha"), b = Map("one", "Beta");
        var page = new StartPage(null, () => [a, b]);
        string? opened = null;
        page.MapRequested += f => opened = f;

        page.Search("bet");
        Assert.Equal([false, true], page.Cards.Select(x => x.Button.IsVisible));
        Assert.True(page.Press(Key.Enter, KeyModifiers.None));
        Assert.Equal(b, opened);

        page.Search("");
        Assert.True(page.Press(Key.Enter, KeyModifiers.None));
        Assert.Equal(a, opened);
    }

    [AvaloniaFact]
    public void CtrlNAndCtrlOAreTheTwoButtons()
    {
        var page = new StartPage(null, () => []);
        int made = 0, opened = 0;
        page.NewMapRequested += () => made++;
        page.OpenFolderRequested += () => opened++;

        Assert.True(page.Press(Key.N, KeyModifiers.Control));
        Assert.True(page.Press(Key.O, KeyModifiers.Control));
        Assert.False(page.Press(Key.Enter, KeyModifiers.None));   // no map to open
        Assert.Equal((1, 1), (made, opened));
    }

    [AvaloniaFact]
    public void NoMapsShowsTheFirstSteps()
    {
        var page = new StartPage(null, () => []);
        Assert.True(page.EmptyShown);
        Assert.Empty(page.Cards);
    }

    [Fact]
    public void ATimeIsSaidAsTodayYesterdayDaysAgoOrTheDate()
    {
        var now = DateTime.Now;
        Assert.StartsWith("today", StartPage.Card.When(now));
        Assert.Equal("yesterday", StartPage.Card.When(now.AddDays(-1)));
        Assert.Equal("3 days ago", StartPage.Card.When(now.AddDays(-3)));
        Assert.Equal(now.AddDays(-30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StartPage.Card.When(now.AddDays(-30)));
    }
}
