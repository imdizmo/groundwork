using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The start page: the maps there are as cards (a picture of the ground, its size, its objects, when it was last
/// saved), grouped by the folder they are in, the last opened first, with New map and Open map folder above them and a
/// search box once there are many; each card's menu shows, copies or forgets its map. A map folder or a heightmap
/// dropped on the page opens it or starts New map over it; Enter opens the first map, Ctrl+N and Ctrl+O the two
/// buttons. Settings, the documentation and the log are at the side.
/// </summary>
public sealed partial class StartPage
{
    /// <summary>A card's width: the cards make a grid whatever their names' lengths.</summary>
    internal const double CardWidth = 232;

    private readonly StackPanel _groups = new() { Spacing = 18 };
    private readonly TextBox _search;
    private readonly Border _empty;
    private readonly Func<IEnumerable<string>> _mapFolders;
    private readonly List<Card> _cards = [];

    /// <summary>The page, with why the window came back here (null: nothing) at the top.</summary>
    /// <param name="lastError">Why the window came back here; null for nothing.</param>
    /// <param name="maps">The maps to list (folders, the last opened first).</param>
    public StartPage(string? lastError = null, Func<IEnumerable<string>>? maps = null)
    {
        _mapFolders = maps ?? (() => []);
        var lastErrorText = new TextBlock { Text = lastError, Foreground = Tokens.Danger, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = lastError != null };
        // The side: the brand, the links under it, the version and the data folder at the bottom.
        var settings = SideButton(Icons.With("settings", Lang.T("start.label.settings")), "start.settings", () => SettingsRequested?.Invoke());
        var docs = SideButton(Icons.With("help", Lang.T("start.label.docs")), "start.docs", () => HelpWindow.Open());
        var log = SideButton(Icons.With("script", Lang.T("start.label.log")), "start.log", () => _ = OpenUrl(new Uri(Path.GetFullPath(Log.FilePath))));
        var notes = SideButton(Icons.With("external", Lang.T("start.versionNotes", BuildInfo.Version), 14), "start.releases", () => _ = OpenUrl(new Uri(ReleasesUrl)));
        notes.FontSize = 11.5;
        var footer = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                notes,
                new TextBlock { Text = Lang.T("start.dataDir", Ui.Tilde(AppSettings.DataDir)), Foreground = Tokens.Dim, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 8, 0) },
            },
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var side = new Border
        {
            Background = Tokens.Raised,
            BorderBrush = Tokens.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(14, 26, 14, 18),
            Child = new DockPanel
            {
                Children =
                {
                    footer,
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 8,
                                Children = { new Image { Width = 24, Height = 24, Source = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://{typeof(MainWindow).Assembly.GetName().Name}/Assets/icon.png"))) }, Ui.BrandTitle(19, VerticalAlignment.Center) },
                            },
                            new TextBlock { Text = Lang.T("start.tagline"), Foreground = Tokens.Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 18) },
                            settings, docs, log,
                        },
                    },
                },
            },
        };
        // The main part: the heading with the two buttons beside it, the search, the cards by folder.
        var add = new Button { Content = Lang.T("start.label.newMap"), FontSize = 13, Padding = new Thickness(14, 8) }.Classed("primary").Tip("start.newMap");
        add.Click += (_, _) => NewMapRequested?.Invoke();
        var open = new Button { Content = Lang.T("start.label.openMap"), FontSize = 13, Padding = new Thickness(14, 8) }.Tip("start.openMapFolder");
        open.Click += (_, _) => OpenFolderRequested?.Invoke();
        // from the team server: shown when one is set (TeamShown)
        _fromTeam = new Button { Content = "From the team", FontSize = 13, Padding = new Thickness(14, 8), IsVisible = false };
        ToolTip.SetTip(_fromTeam, "Open a map your team published to its team server");
        _fromTeam.Click += (_, _) => TeamRequested?.Invoke();
        _search = new TextBox { PlaceholderText = Lang.T("start.search"), Width = 240, FontSize = 12.5, IsVisible = false }.Tip("start.search");
        _search.TextChanged += (_, _) => Filter();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top, Children = { _search, add, open, _fromTeam } };
        Grid.SetColumn(actions, 1);
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = Lang.T("start.page.maps"), FontSize = 22, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = Lang.T("start.page.mapsText"), Foreground = Tokens.Dim, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left },
                    },
                },
                actions,
            },
        };
        _empty = EmptyState();
        var main = new ScrollViewer
        {
            Content = new StackPanel
            {
                MaxWidth = 1040,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(40, 32, 30, 50),
                Spacing = 16,
                Children = { lastErrorText, heading, _empty, _groups },
            },
        };
        Grid.SetColumn(main, 1);
        View = new Grid { Background = Tokens.Bg, ColumnDefinitions = new ColumnDefinitions("260,*"), Children = { side, main }, Focusable = true };
        View.KeyDown += (_, e) => e.Handled = Press(e.Key, e.KeyModifiers);
        // Shown: the keys work at once (the search box when there is one, else the page).
        View.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = _search.IsVisible ? _search.Focus() : View.Focus());
        DragDrop.SetAllowDrop(View, true);
        View.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = Dropped(e) != null ? DragDropEffects.Copy : DragDropEffects.None);
        View.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (Dropped(e) is { } path)
            {
                e.Handled = true;
                if (Directory.Exists(path))
                {
                    MapRequested?.Invoke(path);
                }
                else
                {
                    HeightmapDropped?.Invoke(path);
                }
            }
        });
        ShowMaps();
    }

    /// <summary>Where a version's notes are.</summary>
    internal const string ReleasesUrl = "https://github.com/imdizmo/groundwork/releases";

    /// <summary>The page itself.</summary>
    public Control View { get; }

    /// <summary>Open map folder clicked.</summary>
    public event Action? OpenFolderRequested;

    /// <summary>Asks for a map from the team server.</summary>
    public event Action? TeamRequested;

    private readonly Button _fromTeam;

    /// <summary>Whether a team server is set (its button shown).</summary>
    public bool TeamShown { get => _fromTeam.IsVisible; set => _fromTeam.IsVisible = value; }

    /// <summary>A map chosen: its folder.</summary>
    public event Action<string>? MapRequested;

    /// <summary>New map clicked.</summary>
    public event Action? NewMapRequested;

    /// <summary>Settings clicked.</summary>
    public event Action? SettingsRequested;

    /// <summary>A map's card asked to be taken off the list: its folder (the folder stays).</summary>
    public event Action<string>? RemoveRequested;

    /// <summary>A map's card asked for a copy of it: its folder.</summary>
    public event Action<string>? DuplicateRequested;

    /// <summary>A heightmap file dropped on the page: New map over it.</summary>
    public event Action<string>? HeightmapDropped;

    /// <summary>Opens an address or a file with the system (the log, a map's folder).</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

    /// <summary>The cards, in the list's order.</summary>
    internal IReadOnlyList<Card> Cards => _cards;

    /// <summary>Whether the first steps show (no maps listed).</summary>
    internal bool EmptyShown => _empty.IsVisible;

    /// <summary>The sample map that comes with the editor's sources, or null when not beside it.</summary>
    internal static string? SampleMap()
    {
        string? dir = AppContext.BaseDirectory;
        for (int up = 0; up < 7 && dir != null; up++)
        {
            string sample = Path.Combine(dir, "samples", "Meadow");
            if (MapProject.IsMap(sample))
            {
                return sample;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>The maps' cards made again (after a map was opened, made, copied or forgotten).</summary>
    public void ShowMaps()
    {
        _groups.Children.Clear();
        _cards.Clear();
        // Grouped by the folder they are in, the groups and the cards in them in the list's order: the last opened first.
        foreach (var group in _mapFolders().Distinct().GroupBy(f => Path.GetDirectoryName(f) ?? f))
        {
            var cards = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (string folder in group)
            {
                var card = new Card(this, folder);
                _cards.Add(card);
                cards.Children.Add(card.Button);
            }
            _groups.Children.Add(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Ui.Tilde(group.Key), Foreground = Tokens.Dim, FontSize = 11.5 },
                    cards,
                },
            });
        }
        _empty.IsVisible = _cards.Count == 0;
        _search.IsVisible = _cards.Count > 4;
        if (!_search.IsVisible)
        {
            _search.Text = "";
        }
        Filter();
    }

    /// <summary>Narrows the cards to the names holding the search's text; a group with none left hides.</summary>
    internal void Search(string text)
    {
        _search.Text = text;
        Filter();
    }

    private void Filter()
    {
        string wanted = (_search.Text ?? "").Trim();
        foreach (var card in _cards)
        {
            card.Button.IsVisible = wanted.Length == 0 || card.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var group in _groups.Children.OfType<StackPanel>())
        {
            group.IsVisible = group.Children.OfType<WrapPanel>().Any(p => p.Children.Any(c => c.IsVisible));
        }
    }

    /// <summary>
    /// The page's keys: Enter opens the first map shown, Ctrl+N makes one, Ctrl+O opens a folder, Escape clears the
    /// search.
    /// </summary>
    /// <returns>Whether the key was taken.</returns>
    internal bool Press(Key key, KeyModifiers modifiers)
    {
        switch (key, modifiers)
        {
            case (Key.Enter, KeyModifiers.None) when _cards.FirstOrDefault(c => c.Button.IsVisible && c.Found) is { } first:
                MapRequested?.Invoke(first.Folder);
                return true;
            case (Key.N, KeyModifiers.Control):
                NewMapRequested?.Invoke();
                return true;
            case (Key.O, KeyModifiers.Control):
                OpenFolderRequested?.Invoke();
                return true;
            case (Key.Escape, KeyModifiers.None) when _search.Text is { Length: > 0 }:
                _search.Text = "";
                return true;
            default:
                return false;
        }
    }

    /// <summary>What a drag holds that the page takes: a map folder (or its map.json), a heightmap file; else null.</summary>
    private static string? Dropped(DragEventArgs e)
    {
        foreach (var item in e.DataTransfer.TryGetFiles() ?? [])
        {
            if (item.TryGetLocalPath() is not { } path)
            {
                continue;
            }
            if (File.Exists(path) && Path.GetFileName(path) == MapProject.FileName)
            {
                path = Path.GetDirectoryName(path) ?? path;
            }
            if (MapProject.IsMap(path) || (File.Exists(path) && HeightmapImport.IsHeightmap(path)))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>The first steps, shown while no map is listed.</summary>
    private Border EmptyState()
    {
        var sample = new Button { Content = Lang.T("start.empty.sample"), FontSize = 12.5, IsVisible = SampleMap() != null }.Tip("start.sample");
        sample.Click += (_, _) => MapRequested?.Invoke(SampleMap()!);
        var unreal = new Button { Content = Lang.T("start.empty.unreal"), FontSize = 12.5 }.Classed("ghost");
        unreal.Click += (_, _) => HelpWindow.Open(HelpTopics.Live);
        return Ui.AsCard(new Border
        {
            MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = Lang.T("start.empty.title"), FontSize = 15, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = Lang.T("start.empty.steps"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, LineHeight = 20 },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { sample, unreal } },
                },
            },
        });
    }

    private static Button SideButton(object content, string tip, Action click)
    {
        var b = new Button { Content = content, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7) }.Classed("ghost").Tip(tip);
        b.Click += (_, _) => click();
        return b;
    }
}
