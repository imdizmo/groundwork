using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The driver: the right-hand panel by name (view, history, help or none).
    /// </summary>
    internal void ShowRightPanel(string name) => ShowRight(name switch { "view" => ViewPanelCard, "history" => History.Card, "help" => HelpCard, _ => null });

    /// <summary>
    /// The right-hand panel chosen last (remembered between runs; null: none): what comes back when the Inspector, which
    /// takes its place for a while, closes.
    /// </summary>
    internal Control? ChosenRight() => Prefs.Get("panel.right", "view") switch { "help" => HelpCard, "history" => History.Card, "none" => null, _ => ViewPanelCard };

    /// <summary>
    /// The right-hand panels share the place under the top bar: one at a time (null: none). The choice is remembered.
    /// </summary>
    internal void ShowRight(Control? panel)
    {
        if (panel == ViewPanelCard || panel == HelpCard || panel == History.Card || panel == null)
        {
            Prefs.Set("panel.right", panel == ViewPanelCard ? "view" : panel == HelpCard ? "help" : panel == History.Card ? "history" : "none");
        }
        ViewPanelCard.IsVisible = panel == ViewPanelCard;
        HelpCard.IsVisible = panel == HelpCard;
        if (History.Card.IsVisible != (panel == History.Card))
        {
            History.Toggle();
        }
        ViewButton.Classes.Set("on", ViewPanelCard.IsVisible);
        HistoryButton.Classes.Set("on", Strip.Card.IsVisible);
        HelpButton.Classes.Set("on", HelpCard.IsVisible);
    }

    /// <summary>The space between the cards over the view and its edges.</summary>
    internal const double Edge = Tokens.S2;

    /// <summary>Where the cards under the top bar start (the bar is 44 high).</summary>
    internal const double PanelsTop = Edge + 44 + Edge;

    /// <summary>The history as a strip along the top of the view (the History button shows it).</summary>
    internal HistoryStrip Strip { get; private set; } = null!;

    /// <summary>Shows or hides the history strip (remembered), the cards under it moving down.</summary>
    internal void ToggleStrip()
    {
        Strip.Toggle();
        Prefs.Set("history.strip", Strip.Card.IsVisible);
        HistoryButton.Classes.Set("on", Strip.Card.IsVisible);
        AboveDrawer();
    }

    /// <summary>
    /// A thin upright line between groups of the top bar.
    /// </summary>
    private static Border Sep() => new() { Width = 1, Height = 26, Background = Tokens.Line, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The top bar: back to the start page, the map's name, undo and redo, what is waiting to be saved and Save, and
    /// the right-hand panels.
    /// </summary>
    private Border TopBar()
    {
        UndoButton.Click += async (_, _) => await Undo();
        MapButton.Click += async (_, _) => await LeaveMap();
        MapButton.Tip("top.map");
        ToolTip.SetTip(UndoButton, Lang.T("top.undo"));
        ToolTip.SetTip(RedoButton, Lang.T("top.redo"));
        Strip = new HistoryStrip(() => Session, steps => History.Allow(steps));
        Strip.Moved += () => { UpdateSaveBar(); History.Refresh(); };
        Strip.ListAsked += () => ShowRight(History.Card.IsVisible ? null : History.Card);
        Strip.Card.SizeChanged += (_, _) => AboveDrawer();
        HistoryButton.Click += (_, _) => ToggleStrip();
        HistoryButton.Tip("top.history");
        ViewButton.Click += (_, _) => ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard);
        ViewButton.Tip("top.view");
        HelpButton.Click += (_, _) => ShowRight(HelpCard.IsVisible ? null : HelpCard);
        HelpButton.Tip("top.help");
        RedoButton.Click += async (_, _) => await Redo();
        SaveButton.Click += async (_, _) => await Save();
        SaveButton.Tip("top.save");
        _pendingPill.Child = PendingText;
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { TitleText, SubtitleText } };
        // The names take what the buttons leave and end in "…" when that is too little: the buttons on the right always
        // fit. Kept to the left, the names' column is only as wide as they are when there is room.
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        var left = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, HorizontalAlignment = HorizontalAlignment.Left, Children = { MapButton, Ui.Col(names, 1) } };
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { UndoButton, RedoButton, Sep(), _pendingPill, SaveButton, Sep(), HistoryButton, ViewButton, HelpButton },
        };
        Grid.SetColumn(right, 1);
        bar.Children.Add(left);
        bar.Children.Add(right);
        // Too narrow even with the names ended in "…": the buttons drop their words (FitTopBar).
        bar.LayoutUpdated += (_, _) => FitTopBar(bar.Bounds.Width,
            right.DesiredSize.Width + MapButton.DesiredSize.Width + bar.ColumnSpacing + 2 * left.ColumnSpacing);
        var card = Ui.Card(bar);
        card.Height = 44;
        card.Padding = new Thickness(Ui.Pad.Left, 0);
        card.Margin = new Thickness(Edge, Edge, Edge, 0);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    /// <summary>
    /// The status bar's cursor readout: where the pointer is on the ground (see CursorReadout).
    /// </summary>
    internal TextBlock CursorText { get; } = new() { FontSize = 12, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };

    /// <summary>
    /// Where the pointer last was on the 3D view (null: off it), so the readout can follow a stroke's changes.
    /// </summary>
    private Point? _cursorAt;
}
