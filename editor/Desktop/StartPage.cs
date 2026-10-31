using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The start page: the maps there are (each opens with a click) and New map, with Settings, the documentation and the
/// log at the side.
/// </summary>
public sealed class StartPage
{
    private readonly WrapPanel _maps = new() { Orientation = Orientation.Horizontal };
    private readonly Func<IEnumerable<string>> _mapFolders;

    /// <summary>The page, with why the window came back here (null: nothing) at the top.</summary>
    /// <param name="lastError">Why the window came back here; null for nothing.</param>
    /// <param name="maps">The maps to list (folders, the last opened first).</param>
    public StartPage(string? lastError = null, Func<IEnumerable<string>>? maps = null)
    {
        _mapFolders = maps ?? (() => []);
        var lastErrorText = new TextBlock { Text = lastError, Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = lastError != null };
        var settings = SideButton(Icons.With("settings", Lang.T("start.label.settings")), "start.settings", () => SettingsRequested?.Invoke());
        var docs = SideButton(Icons.With("help", Lang.T("start.label.docs")), "start.docs", () => HelpWindow.Open());
        var log = SideButton(Lang.T("start.label.log"), "start.log", () => _ = OpenUrl(new Uri(Path.GetFullPath(Log.FilePath))));
        var footer = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                settings, docs, log,
                new TextBlock { Text = Lang.T("start.version", BuildInfo.Version), Foreground = Ui.Muted, FontSize = 11, Margin = new Thickness(12, 8, 0, 0) },
                new TextBlock { Text = Lang.T("start.dataDir", Ui.Tilde(AppSettings.DataDir)), Foreground = Ui.Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 2, 8, 0) },
            },
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var side = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 27, 33)),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(14, 26, 14, 18),
            Child = new DockPanel
            {
                Children =
                {
                    footer,
                    new StackPanel { Spacing = 6, Children = { Ui.BrandTitle(19), new TextBlock { Text = Lang.T("start.tagline"), Foreground = Ui.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } },
                },
            },
        };
        var main = new ScrollViewer
        {
            Content = new StackPanel
            {
                MaxWidth = 1040,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(40, 32, 30, 50),
                Spacing = 10,
                Children =
                {
                    lastErrorText,
                    new TextBlock { Text = Lang.T("start.page.maps"), FontSize = 22, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = Lang.T("start.page.mapsText"), Foreground = Ui.Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left },
                    _maps,
                },
            },
        };
        Grid.SetColumn(main, 1);
        View = new Grid { Background = Ui.Bg, ColumnDefinitions = new ColumnDefinitions("260,*"), Children = { side, main } };
        ShowMaps();
    }

    /// <summary>The page itself.</summary>
    public Control View { get; }

    /// <summary>Open map folder clicked.</summary>
    public event Action? OpenFolderRequested;

    /// <summary>A map chosen: its folder.</summary>
    public event Action<string>? MapRequested;

    /// <summary>New map clicked.</summary>
    public event Action? NewMapRequested;

    /// <summary>Settings clicked.</summary>
    public event Action? SettingsRequested;

    /// <summary>Opens an address or a file with the system (the log).</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

    /// <summary>The maps' buttons made again (after a new map).</summary>
    public void ShowMaps()
    {
        _maps.Children.Clear();
        foreach (string folder in _mapFolders().Where(MapProject.IsMap))
        {
            string name;
            try
            {
                name = MapProject.Open(folder).Name;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                name = Path.GetFileName(folder) + " (map.json unreadable)";
            }
            var b = new Button
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = Ui.Tilde(folder), FontSize = 11, Foreground = Ui.Muted },
                    },
                },
                MinWidth = 220,
                Padding = new Thickness(14, 10),
                Margin = new Thickness(0, 0, 10, 10),
            }.Tip("start.openMap");
            b.Click += (_, _) => MapRequested?.Invoke(folder);
            _maps.Children.Add(b);
        }
        var add = new Button { Content = Lang.T("start.label.newMap"), FontSize = 13, Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10), VerticalAlignment = VerticalAlignment.Stretch }.Classed("primary").Tip("start.newMap");
        add.Click += (_, _) => NewMapRequested?.Invoke();
        _maps.Children.Add(add);
        var open = new Button { Content = Lang.T("start.label.openMap"), FontSize = 13, Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10), VerticalAlignment = VerticalAlignment.Stretch }.Tip("start.openMapFolder");
        open.Click += (_, _) => OpenFolderRequested?.Invoke();
        _maps.Children.Add(open);
    }

    private static Button SideButton(object content, string tip, Action click)
    {
        var b = new Button { Content = content, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7) }.Classed("ghost").Tip(tip);
        b.Click += (_, _) => click();
        return b;
    }
}
