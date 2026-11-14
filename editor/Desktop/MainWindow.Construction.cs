using Avalonia.Controls;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{

    /// <summary>
    /// What is remembered between runs (the tests' windows: in memory only, unless a test gives one).
    /// </summary>
    internal Prefs Prefs { get; }

    /// <summary>
    /// The window, its panels wired to each other; with <paramref name="load"/> it opens on the start page (or, with
    /// --world, straight into that area).
    /// </summary>
    /// <param name="load">False opens the window without a world (tests), its prefs in memory.</param>
    /// <param name="prefs">What is remembered between runs; null for the default.</param>
    public MainWindow(bool load = true, Prefs? prefs = null)
    {
        Prefs = prefs ?? (load ? Prefs.Load() : Prefs.InMemory());
        SelectPanel = new SelectPanel(View.SelectTool);
        History = new HistoryPanel(() => Session);
        WireSelectPanel();
        History.Message += t => { Msg(t); UpdateSaveBar(); };
        History.ShowAsked += (x, z) => View.Orbit(x, z, View.WorldCamera is { } c ? c.Yaw * 180 / MathF.PI : 30, 55, 70);
        MaskPanel = new MaskPanel(Mask);
        WireGroundTools();
        MeasurePanel = new MeasurePanel(View);
        ModToolPanel = new ModToolPanel(Prefs);
        WireModTools();
        PastePanel = new PastePanel(View.Paste);
        PastePanel.Done += Tools.ChooseSelect;
        View.PasteClicked += PasteAt;
        Title = $"Groundwork {BuildInfo.Version}";
        Icon = AppIcon();
        Width = Options.WindowWidth > 0 ? Options.WindowWidth : 1500;
        Height = Options.WindowHeight > 0 ? Options.WindowHeight : 950;
        Background = Ui.Bg;
        HelpCard = Help(RecordBox());
        ConfirmSave = text => Dialogs.Ask(this, Lang.T("save.ask.save.title"), text, Lang.T("common.save"));
        Ask = (title, text, yes, no) => Dialogs.Ask(this, title, text, yes, no);
        Tell = text => Dialogs.Tell(this, "Groundwork", text);
        var tools = _toolColumn = BuildToolColumn();
        Tools.ToolChanged += OnToolChanged;
        View.SelectTool.Message += (said, _) => Msg(said);
        View.StrokeEnded += OnStrokeEnded;
        View.ObjectMiddleClicked += PickKindOfObject;
        View.ObjectShiftRightClicked += DeleteObject;
        WireKeys();
        Closing += async (_, e) => await OnClosingAsync(e);
        // Takes the mouse for the 3D view (see GlView.Attach).
        var surface = new Border { Background = Brushes.Transparent };
        Surface = surface;
        ViewPanelCard = ViewPanel();
        LayOutRightPanels();
        _editorPage = new Grid { Children = { View, surface, tools, ViewPanelCard, History.Card, HelpCard, TopBar(), StatusBar() } };
        _busy.Child = _busyText;
        _pages.Content = _editorPage;
        Content = new Grid { Children = { _pages, _busy } };
        View.Attach(surface, this);
        View.AltWheel = AltWheel;
        View.CtrlWheel = CtrlWheel;
        TrackCursor(surface);
        View.Perf = _perf;
        WireViewReports();
        // Once really closed (Closing also comes when "Keep editing" keeps the window open).
        Closed += (_, _) => { _perf.Flush(); Prefs.Flush(); HelpWindow.Current?.Close(); };
        RememberPrefs();
        LoadedInfo.Text = Lang.T("world.msg.loading");
        Opened += async (_, _) => await OnOpenedAsync(load);
    }

    /// <summary>The rail, the chosen tool's card, and the Select, Measure, mod tool, Paste and Mask cards, left of the view.</summary>
    private StackPanel BuildToolColumn()
    {
        var tools = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = Ui.Gap,
            // Under the top bar, above the status bar.
            Margin = new Avalonia.Thickness(10, 70, 10, 58),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ModToolPanel.Card, PastePanel.Card, MaskPanel.Card },
        };
        // Every panel of the column scrolls when the window is too short for it (a bar only then).
        foreach (var card in tools.Children.OfType<Border>())
        {
            if (card.Child is Control inner and not ScrollViewer)
            {
                card.Child = null;
                card.Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = inner };
            }
        }
        SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ModToolPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = false;
        Tools.Options.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        Tools.Rail.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        return tools;
    }

    /// <summary>The Help card's switch that records the frame times (PerfLog).</summary>
    private CheckBox RecordBox()
    {
        var record = new CheckBox { Content = Lang.T("help.label.record"), FontSize = 12.5 };
        record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) { _perf.Flush(); } };
        ToolTip.SetTip(record, Lang.T("view.perfFile", Tips.Of("view.perf"), Ui.Tilde(PerfLog.FilePath)));
        return record;
    }

    /// <summary>The app's icon, for its windows (the main one, Help).</summary>
    internal static WindowIcon AppIcon() => new(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://{typeof(MainWindow).Assembly.GetName().Name}/Assets/icon.png")));

}
