using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The small window shown while the editor starts: the icon and name, and along the bottom what is being done and how
/// far along it is, until the main window takes over (App).
/// </summary>
public sealed class Splash : Window
{
    private readonly TextBlock _what = new() { FontSize = 12.5, Foreground = Tokens.Dim };
    private readonly TextBlock _percent = new() { FontSize = 12.5, Foreground = Tokens.Dim, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 1, Height = 4, MinHeight = 4, IsIndeterminate = true, Foreground = Tokens.Accent, Background = Tokens.Line };

    /// <summary>The window, centred, with no frame.</summary>
    public Splash()
    {
        Title = "Groundwork";
        Width = 520;
        Height = 260;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Tokens.Raised;
        Icon = MainWindow.AppIcon();
        var logo = new Image { Width = 64, Height = 64, Source = new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://{typeof(MainWindow).Assembly.GetName().Name}/Assets/icon.png"))) };
        var status = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        status.Children.Add(_what);
        Grid.SetColumn(_percent, 1);
        status.Children.Add(_percent);
        Content = new Border
        {
            BorderBrush = Tokens.LineStrong,
            BorderThickness = new Thickness(1),
            Child = new DockPanel
            {
                Children =
                {
                    new StackPanel { [DockPanel.DockProperty] = Dock.Bottom, Margin = new Thickness(24, 0, 24, 20), Spacing = 8, Children = { status, _bar } },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 16,
                        Margin = new Thickness(28, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            logo,
                            new StackPanel
                            {
                                VerticalAlignment = VerticalAlignment.Center,
                                Children =
                                {
                                    Ui.BrandTitle(26, VerticalAlignment.Center),
                                    new TextBlock { Text = Lang.T("start.tagline"), FontSize = 13, Foreground = Tokens.Dim },
                                },
                            },
                        },
                    },
                },
            },
        };
        Show(Lang.T("splash.starting"), null);
    }

    /// <summary>Shows what is being done and a fraction from 0 to 1 (null: how long it takes is unknown).</summary>
    public void Show(string what, double? fraction)
    {
        _what.Text = what;
        _bar.IsIndeterminate = fraction is null;
        _bar.Value = fraction ?? 0;
        _percent.Text = fraction is { } f ? $"{f:P0}" : "";
    }
}
