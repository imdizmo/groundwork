using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{

    /// <summary>
    /// A small heading of the View panel.
    /// </summary>
    private static TextBlock Heading(string t) => Ui.Heading(t, 12);

    // View, Look: the game's look or plain colours and boxes, see-through buildings, and the 3D
    // resolution (GlView). Presets switch every kind and overlay to the defaults, all on, or only the
    // ground (the web editor's buttons).
    /// <summary>
    /// The game's look, or plain colours and boxes.
    /// </summary>
    internal CheckBox ModelsBox { get; } = new() { Content = Lang.T("view.label.models"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// Draw buildings see-through.
    /// </summary>
    internal CheckBox SeeThroughBox { get; } = new() { Content = Lang.T("view.label.seeThrough"), FontSize = 12 };

    /// <summary>Shows the map's names and regions.</summary>
    internal CheckBox NamesBox { get; } = new() { Content = Lang.T("view.label.names"), IsChecked = true, FontSize = 12 };

    /// <summary>Draws the map as a block (MapBlock).</summary>
    internal CheckBox BlockBox { get; } = new() { Content = Lang.T("view.label.block"), FontSize = 12 };

    /// <summary>Steep ground: the ground steeper than SteepBox's degrees drawn red.</summary>
    internal CheckBox SteepCheck { get; } = new() { Content = Lang.T("view.label.steep"), FontSize = 12 };

    /// <summary>Steep ground's angle.</summary>
    internal NumericUpDown SteepBox { get; } = new() { Value = 35, Minimum = 5, Maximum = 85, Increment = 5, FormatString = "0", FontSize = 12, MinWidth = 80 };

    /// <summary>
    /// The 3D view's resolution: the screen's, balanced, or fast.
    /// </summary>
    internal ComboBox ResolutionBox { get; } = new() { ItemsSource = new[] { Lang.T("view.res.sharp"), Lang.T("view.res.balanced"), Lang.T("view.res.fast") }, SelectedIndex = 0, FontSize = 12, MinWidth = 150 };

    /// <summary>
    /// Every kind, the water and the overlays as they start.
    /// </summary>
    internal Button DefaultsButton { get; } = new() { Content = Lang.T("view.label.defaults"), FontSize = 12 };

    /// <summary>
    /// Every kind, the water and the overlays on.
    /// </summary>
    internal Button AllButton { get; } = new() { Content = Lang.T("view.label.all"), FontSize = 12 };

    /// <summary>
    /// Only the ground: every kind, the water and the overlays off.
    /// </summary>
    internal Button GroundButton { get; } = new() { Content = Lang.T("view.label.ground"), FontSize = 12 };

    /// <summary>
    /// Each preset switch and whether it starts on (the defaults).
    /// </summary>
    private readonly Dictionary<CheckBox, bool> _viewDefaults = [];

    /// <summary>
    /// The View panel's presets.
    /// </summary>
    internal enum ViewPreset { Defaults, All, Ground }

    /// <summary>
    /// Sets every kind, the water and the overlays as the preset says.
    /// </summary>
    internal void ShowPreset(ViewPreset preset)
    {
        foreach (var (box, on) in _viewDefaults)
        {
            box.IsChecked = preset switch { ViewPreset.All => true, ViewPreset.Ground => false, _ => on };
        }
    }

    /// <summary>
    /// Builds the View panel and wires it to the 3D view.
    /// </summary>
    private Border ViewPanel()
    {
        var card = BuildViewPanel();
        WireViewPanel();
        return card;
    }

    /// <summary>
    /// Lays out the View panel: its headings, switches, lists and preset buttons with their tips; the switches'
    /// starting states are kept as the presets' defaults.
    /// </summary>
    private Border BuildViewPanel()
    {
        var list = new StackPanel { Spacing = 2 };
        var viewHead = Ui.Heading(Lang.T("rail.label.view"), 0);
        viewHead.VerticalAlignment = VerticalAlignment.Center;
        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { viewHead, Ui.Col(Ui.HelpButton(() => HelpTopics.ViewPanel), 1) } });
        list.Children.Add(Heading(Lang.T("view.head.look")));
        ModelsBox.Tip("view.models");
        WaterBox.Tip("view.water");
        DefaultsButton.Tip("view.defaults");
        AllButton.Tip("view.all");
        SeeThroughBox.Tip("view.seeThrough");
        ResolutionBox.Tip("view.res3d");
        list.Children.Add(ModelsBox);
        list.Children.Add(SeeThroughBox);
        NamesBox.Tip("view.names");
        list.Children.Add(NamesBox);
        BlockBox.Tip("view.block");
        list.Children.Add(BlockBox);
        SteepCheck.Tip("view.steep");
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { SteepCheck, SteepBox, new TextBlock { Text = "°", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = Lang.T("view.label.res3d"), FontSize = 12 }, ResolutionBox } });
        list.Children.Add(Heading(Lang.T("view.head.objects")));
        foreach (var k in ObjectKinds.All)
        {
            var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = View.IsShown(k), FontSize = 12 };
            ToolTip.SetTip(box, Tips.Kind(k));
            _kindBoxes[k] = box;
            list.Children.Add(box);
        }
        list.Children.Add(WaterBox);
        // The presets cover what is shown: kinds and water (as they are now: the defaults).
        foreach (var box in _kindBoxes.Values.Append(WaterBox))
        {
            _viewDefaults[box] = box.IsChecked == true;
        }
        GroundButton.Tip("view.ground");
        list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0), Children = { DefaultsButton, AllButton, GroundButton } });
        // The web editor's switches.
        foreach (var box in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(list).OfType<CheckBox>())
        {
            box.Classes.Add("switch");
            box.FontSize = 12.5;
        }
        list.Width = 240;
        return Ui.AsCard(new Border
        {
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // Scrolls when the window is too short for it (a bar only then).
            Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = list },
        });
    }

    /// <summary>
    /// Wires the View panel (built first) to the 3D view: each switch and list sets what is drawn, the presets set
    /// the switches, and the view's counts go into the switches' labels.
    /// </summary>
    private void WireViewPanel()
    {
        ModelsBox.IsCheckedChanged += (_, _) => View.ModelsOn = ModelsBox.IsChecked == true;
        SeeThroughBox.IsCheckedChanged += (_, _) => View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        NamesBox.IsCheckedChanged += (_, _) => View.ShowNames = NamesBox.IsChecked == true;
        BlockBox.IsCheckedChanged += (_, _) => MapBlock.On = BlockBox.IsChecked == true;
        void Steep() => View.SteepAngle = SteepCheck.IsChecked == true ? (float)(SteepBox.Value ?? 35) : 0;
        SteepCheck.IsCheckedChanged += (_, _) => Steep();
        SteepBox.ValueChanged += (_, _) => Steep();
        ResolutionBox.SelectionChanged += (_, _) =>
        {
            View.Resolution3D = (GlView.Resolution)Math.Max(0, ResolutionBox.SelectedIndex);
            var (w, h) = View.RenderSize();
            var name = View.Resolution3D switch
            {
                GlView.Resolution.Sharp => Said.Of("view.res.sharp"),
                GlView.Resolution.Balanced => Said.Of("view.res.balanced"),
                _ => Said.Of("view.res.fast"),
            };
            Msg("view.msg.resolution", name, w, h);
        };
        foreach (var (k, box) in _kindBoxes)
        {
            box.IsCheckedChanged += (_, _) => View.SetShown(k, box.IsChecked == true);
        }
        WaterBox.IsCheckedChanged += (_, _) => View.ShowWater = WaterBox.IsChecked == true;
        UnsavedBox.IsCheckedChanged += (_, _) => View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        View.KindCounts += counts =>
        {
            foreach (var (k, box) in _kindBoxes)
            {
                box.Content = Ui.Counted(ObjectKinds.Label(k), counts.GetValueOrDefault(k));
                // Only the kinds the map's catalog has.
                box.IsVisible = !InMap || counts.GetValueOrDefault(k) > 0;
            }
        };
    }
}
