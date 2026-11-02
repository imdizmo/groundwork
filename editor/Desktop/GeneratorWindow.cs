using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Groundwork.App;
using Spec = Groundwork.Desktop.ZoneGenerator.Spec;

namespace Groundwork.Desktop;

/// <summary>
/// The generator's window: a style and its numbers as sliders on the left, the ground they make in 3D on the right (made
/// again a moment after a slider stops). For a new map (its name and size too, or flat or gently rolling ground), or for
/// a generated map not yet worked on, made again.
/// </summary>
public sealed partial class GeneratorWindow : Window
{
    /// <summary>A new map asked for: its name and size, and its numbers (null: no zone, flat or gently rolling ground with the seed).</summary>
    public sealed record NewMapAsked(string Name, int Chunks, Spec? Spec, bool Rolling, int Seed);

    private static readonly ZoneGenerator.Planting Nothing = new([], [], [], []);
    private readonly IReadOnlyList<MapProject.Layer> _layers;
    private readonly TerrainPreview _preview = new();
    private readonly TextBlock _status = new() { FontSize = 12, Foreground = Ui.Muted, Margin = new Thickness(12, 8), TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _knobs = new() { Spacing = 4 };
    private readonly List<Action> _show = [];
    private readonly DispatcherTimer _later = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private Spec _spec;
    private int _chunks, _style;
    private bool _setting, _busy, _again;

    /// <summary>The styles offered, then (new maps only) flat and gently rolling ground.</summary>
    private static int StyleCount => ZoneGenerator.Styles.Count;

    private bool Zone => _style < StyleCount;

    private int Metres => (_chunks * 63) + 1;

    private GeneratorWindow(bool isNew, Spec spec, int chunks, IReadOnlyList<MapProject.Layer> layers, string? name)
    {
        (_spec, _chunks, _layers) = (spec, chunks, layers);
        Title = isNew ? "New map" : $"Make {name} again";
        (Width, Height, MinWidth, MinHeight) = (1180, 800, 820, 560);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(24, 28, 34));
        _later.Tick += (_, _) => { _later.Stop(); _ = Preview(); };
        Closed += (_, _) => _later.Stop();
    }

    /// <summary>Asks for a new map (null: cancelled).</summary>
    public static async Task<NewMapAsked?> NewMap(Window owner)
    {
        var w = new GeneratorWindow(true, Styled(0, 8, Random.Shared.Next(1, 10000)), 8, MapProject.NewLayers, null);
        var name = new TextBox { Text = "Map", FontSize = 13 };
        var size = new TextBlock();
        var sizeSlider = Ui.Slide(1, 64, 1, 8, size, v => $"{(I(v) * 63) + 1} m", v => { w._chunks = I(v); w.Changed(); });
        NewMapAsked? result = null;
        var make = new Button { Content = "Make the map", IsDefault = true }.Classed("primary");
        make.Click += (_, _) =>
        {
            if (name.Text is { Length: > 0 } n)
            {
                result = new NewMapAsked(n.Trim(), w._chunks, w.Zone ? w.Asked() : null, w._style == StyleCount + 1, w._spec.Seed);
                w.Close();
            }
        };
        w.Build([Ui.Row("Name", name, null, "110,*"), Ui.Row("Size", sizeSlider, size, "110,*,64")], make, ["Flat ground", "Gently rolling ground"]);
        await w.ShowDialog(owner);
        return result;
    }

    /// <summary>Asks for new numbers to make a generated map again with (null: cancelled).</summary>
    public static async Task<Spec?> Again(Window owner, MapProject m, Spec spec)
    {
        var w = new GeneratorWindow(false, spec, (m.ReadRaster().Cols - 1) / 63, m.Layers, m.Name) { _style = -1 };
        Spec? result = null;
        var make = new Button { Content = "Make it again", IsDefault = true }.Classed("primary");
        make.Click += (_, _) => { result = w.Asked(); w.Close(); };
        w.Build([new TextBlock { Text = "Everything on the map is made again from these numbers. It is still as it was made, so nothing of yours is lost.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Ui.Muted }], make, []);
        await w.ShowDialog(owner);
        return result;
    }

    /// <summary>A style's numbers for a map so many chunks across: its rim no wider than a fifth of it, lakes for its size when it has none of its own.</summary>
    private static Spec Styled(int style, int chunks, int seed)
    {
        int metres = (chunks * 63) + 1;
        var s = ZoneGenerator.Styles[style].Spec;
        return s with
        {
            Seed = seed,
            RimWidth = MathF.Min(s.RimWidth, metres * 0.2f),
            LakeHectares = s.LakeHectares ?? (metres >= 800 ? [MathF.Round(metres * metres / 1e6f * 4)] : []),
        };
    }

    /// <summary>The numbers as asked (the highest ground not under the lowest).</summary>
    private Spec Asked() => _spec with { High = MathF.Max(_spec.Low, _spec.High) };

    private void Build(Control[] top, Button make, string[] more)
    {
        var styles = new ComboBox
        {
            ItemsSource = ZoneGenerator.Styles.Select(s => s.Name).Concat(more).ToList(),
            SelectedIndex = _style,
            PlaceholderText = "As it was made",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 13,
        };
        var about = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Ui.Muted, Text = _style >= 0 ? ZoneGenerator.Styles[_style].About : "" };
        styles.SelectionChanged += (_, _) =>
        {
            _style = styles.SelectedIndex;
            about.Text = Zone ? ZoneGenerator.Styles[_style].About : _style == StyleCount ? "Flat ground, a metre above the sea, nothing on it." : "Gentle rolling ground a few metres high, nothing on it.";
            if (Zone)
            {
                _spec = Styled(_style, _chunks, _spec.Seed);
            }
            _knobs.IsEnabled = Zone;
            Shown();
            Changed();
        };
        AddKnobs();
        var cancel = new Button { Content = Lang.T("common.cancel"), IsCancel = true };
        cancel.Click += (_, _) => Close();
        var left = new DockPanel { Margin = new Thickness(16, 14, 8, 14), LastChildFill = true };
        var head = new StackPanel { Spacing = 8 };
        head.Children.AddRange(top);
        head.Children.Add(Ui.Row("Style", styles, null, "110,*"));
        head.Children.Add(about);
        DockPanel.SetDock(head, Dock.Top);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0), Children = { make, cancel } };
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(head);
        left.Children.Add(buttons);
        left.Children.Add(new ScrollViewer { Content = _knobs, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(0, 0, 12, 0) });
        var right = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(8, 14, 16, 14) };
        right.Children.Add(new Border { Child = _preview, CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Ui.Line, BorderThickness = new Thickness(1) });
        Grid.SetRow(_status, 1);
        right.Children.Add(_status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("460,*") };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Content = grid;
        Shown();
        Opened += (_, _) => _ = Preview();
    }

    /// <summary>The sliders, by group, and the few switches between them.</summary>
    private void AddKnobs()
    {
        string? group = null;
        foreach (var k in Knobs)
        {
            if (k.Group != group)
            {
                group = k.Group;
                _knobs.Children.Add(Ui.Heading(group, _knobs.Children.Count == 0 ? 0 : 14));
                if (group == "Camps & roads")
                {
                    AddSwitch("Roads between the camps", s => s.Roads, (s, on) => s with { Roads = on });
                }
            }
            AddKnob(k);
            if (k.Label == "Sea depth")
            {
                var side = new ComboBox { ItemsSource = new[] { "west", "east", "north", "south" }, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
                side.SelectionChanged += (_, _) => { if (!_setting && side.SelectedItem is string v) { _spec = _spec with { CoastSide = v }; Changed(); } };
                _show.Add(() => side.SelectedItem = _spec.CoastSide);
                _knobs.Children.Add(Ui.Row("Coast side", side, null, "150,*"));
                AddSwitch("A drop into nothing (no sea)", s => s.CoastDry, (s, on) => s with { CoastDry = on });
            }
        }
        _knobs.Children.Add(Ui.Heading("Seed", 14));
        var seed = new TextBox { FontSize = 12 };
        var dice = new Button { Content = "Another", FontSize = 12 };
        ToolTip.SetTip(dice, "The same numbers with another seed make other ground of the same kind.");
        dice.Click += (_, _) => { _spec = _spec with { Seed = Random.Shared.Next(1, 100000) }; Shown(); Changed(); };
        void Typed()
        {
            if (!_setting && int.TryParse(seed.Text, out int v) && v != _spec.Seed)
            {
                _spec = _spec with { Seed = v };
                Changed();
            }
        }
        seed.LostFocus += (_, _) => Typed();
        seed.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Typed(); } };
        _show.Add(() => seed.Text = _spec.Seed.ToString(CultureInfo.InvariantCulture));
        _knobs.Children.Add(Ui.Row("Seed", seed, dice, "150,*,80"));
    }

    private void AddKnob(Knob k)
    {
        string format = k.Step < 1 ? "0.#" : "0";
        var slider = new Slider { Minimum = k.Min, Maximum = k.Max, SmallChange = k.Step, TickFrequency = k.Step, IsSnapToTickEnabled = true };
        var box = new TextBox { FontSize = 12, MinWidth = 0, MinHeight = 0, Height = 26, Padding = new Thickness(4, 2), HorizontalContentAlignment = HorizontalAlignment.Right, VerticalContentAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(box, "Type a number (beyond the slider's ends too), Enter to use it");
        if (k.Tip.Length > 0)
        {
            ToolTip.SetTip(slider, k.Tip);
        }
        slider.ValueChanged += (_, e) =>
        {
            if (!_setting)
            {
                _spec = k.Set(_spec, e.NewValue);
                box.Text = e.NewValue.ToString(format, CultureInfo.InvariantCulture);
                Changed();
            }
        };
        void Typed()
        {
            if (!_setting && double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && Math.Abs(v - k.Get(_spec)) > 1e-6)
            {
                _spec = k.Set(_spec, v);
                Shown();
                Changed();
            }
        }
        box.LostFocus += (_, _) => Typed();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Typed(); } };
        _show.Add(() =>
        {
            double v = k.Get(_spec);
            slider.Value = Math.Clamp(v, k.Min, k.Max);
            box.Text = v.ToString(format, CultureInfo.InvariantCulture);
        });
        var unit = new TextBlock { Text = k.Unit, FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*,58,46"), ColumnSpacing = 6 };
        row.Children.Add(new TextBlock { Text = k.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Ui.Col(slider, 1));
        row.Children.Add(Ui.Col(box, 2));
        row.Children.Add(Ui.Col(unit, 3));
        _knobs.Children.Add(row);
    }

    private void AddSwitch(string label, Func<Spec, bool> get, Func<Spec, bool, Spec> set)
    {
        var box = new CheckBox { Content = label, FontSize = 12 };
        box.IsCheckedChanged += (_, _) => { if (!_setting) { _spec = set(_spec, box.IsChecked == true); Changed(); } };
        _show.Add(() => box.IsChecked = get(_spec));
        _knobs.Children.Add(box);
    }

    /// <summary>Every control shows the numbers as they are now.</summary>
    private void Shown()
    {
        _setting = true;
        foreach (var show in _show)
        {
            show();
        }
        _setting = false;
    }

    /// <summary>A number changed: the preview made again once the sliders rest a moment.</summary>
    private void Changed()
    {
        _later.Stop();
        _later.Start();
    }

    /// <summary>Makes the preview (one at a time; asked again while one is made, made again after it).</summary>
    private async Task Preview()
    {
        if (_busy)
        {
            _again = true;
            return;
        }
        _busy = true;
        do
        {
            _again = false;
            var (spec, metres, style, seed) = (Zone ? Asked() : null, Metres, _style, _spec.Seed);
            _status.Text = "Making the preview…";
            try
            {
                var mesh = await Task.Run(() => Make(spec, metres, style == StyleCount + 1, seed, _layers));
                if (!_again)
                {
                    _preview.Show(mesh);
                }
                _status.Text = "Drag to turn round it, the wheel to come nearer. Trees, rocks, camps and floating islands are not shown here.";
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidOperationException or OutOfMemoryException)
            {
                _status.Text = $"These numbers make no ground: {e.Message}";
            }
        }
        while (_again);
        _busy = false;
    }

    /// <summary>The preview of the ground the numbers make on a map so many metres across (no numbers: flat or gently rolling ground).</summary>
    internal static TerrainPreview.Mesh Make(Spec? spec, int metres, bool rolling, int seed, IReadOnlyList<MapProject.Layer> layers)
    {
        int x0 = -(metres / 2);
        if (spec != null)
        {
            return TerrainPreview.Of(ZoneGenerator.Generate(spec, metres, metres, x0, x0, Nothing), metres, metres, x0, x0, layers, spec.CoastDry);
        }
        float[] h = new float[metres * metres];
        byte[] mask = new byte[h.Length * 4];
        Parallel.For(0, metres, r =>
        {
            for (int c = 0; c < metres; c++)
            {
                int p = (r * metres) + c;
                h[p] = rolling ? 3 + MapProject.RollingAt(seed, c + x0, r + x0) : 1;
                mask[p * 4] = (byte)(h[p] < 1.5f ? 255 : 0);
            }
        });
        return TerrainPreview.Of(new ZoneGenerator.Zone(h, mask, [], [], new float[h.Length]), metres, metres, x0, x0, layers, false);
    }
}
