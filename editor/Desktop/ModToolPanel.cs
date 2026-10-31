using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// The card of a mod's own tool (ModExtensions.Tool), next to the rail: its name and help, then its controls as the
/// tool lists them (sliders, choices, toggles, buttons, lines of text), built again for each tool chosen. The values are
/// remembered between runs (Prefs, "mod.&lt;tool&gt;.&lt;control&gt;").
/// </summary>
public sealed class ModToolPanel
{
    private readonly Prefs? _prefs;
    private readonly StackPanel _body = new() { Width = 300, Spacing = 6 };
    private readonly Dictionary<string, float> _numbers = [];
    private readonly Dictionary<string, int> _choices = [];
    private readonly Dictionary<string, bool> _toggles = [];
    private readonly Dictionary<string, TextBlock> _texts = [];
    private readonly Dictionary<string, List<int>> _picked = [];
    private readonly Dictionary<string, Slider> _sliders = [];
    private readonly Dictionary<string, CheckBox> _toggleBoxes = [];
    private readonly Dictionary<string, (StackPanel Cards, List<(string, string, string)> Items)> _lists = [];
    private bool _setting;

    /// <summary>An empty card.</summary>
    /// <param name="prefs">Where the values are remembered (null: not remembered).</param>
    public ModToolPanel(Prefs? prefs)
    {
        _prefs = prefs;
        Card = Ui.AsCard(new Border { VerticalAlignment = VerticalAlignment.Top, Child = _body });
    }

    /// <summary>The card.</summary>
    public Control Card { get; }

    /// <summary>The tool shown, null before one is.</summary>
    public ModExtensions.Tool? Tool { get; private set; }

    /// <summary>A control was changed or a button pressed: its id.</summary>
    public event Action<string>? Changed;

    /// <summary>A slider's value (its first value, or its lowest, for one the card does not have).</summary>
    public float Number(string id) => _numbers.GetValueOrDefault(id);

    /// <summary>A choice's index (0 for one the card does not have).</summary>
    public int Choice(string id) => _picked.TryGetValue(id, out var p) ? p.FirstOrDefault(-1) : _choices.GetValueOrDefault(id);

    /// <summary>A palette's picked items (indices).</summary>
    public IReadOnlyList<int> Picked(string id) => _picked.TryGetValue(id, out var p) ? p : [];

    /// <summary>A control of the window's put at the end of the card (the map's Mask under Brush and Biome).</summary>
    public Control? Tail
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }
            if (field?.Parent is Panel old)
            {
                _ = old.Children.Remove(field);
            }
            field = value;
            AddTail();
        }
    }

    private void AddTail()
    {
        if (Tail is not { } tail || Tool == null || ReferenceEquals(tail.Parent, _body))
        {
            return;
        }
        if (tail.Parent is Panel p)
        {
            _ = p.Children.Remove(tail);
        }
        _body.Children.Add(tail);
    }

    /// <summary>Palettes are picked elsewhere (the map's drawer): kept, not shown on the card.</summary>
    public bool PalettesElsewhere { get; set; }

    /// <summary>Picks a palette's items from outside the card (raising Changed unless quiet; nothing for one it does not have).</summary>
    public void SetPicked(string id, IReadOnlyList<int> picked, bool quiet = false)
    {
        if (!_picked.TryGetValue(id, out var list) || list.SequenceEqual(picked))
        {
            return;
        }
        list.Clear();
        list.AddRange(picked);
        _prefs?.Set(Key(id), string.Join(",", list));
        if (!quiet)
        {
            Changed?.Invoke(id);
        }
    }

    /// <summary>A toggle's value (false for one the card does not have).</summary>
    public bool IsOn(string id) => _toggles.GetValueOrDefault(id);

    /// <summary>Changes a text line of the card (nothing for one it does not have).</summary>
    public void SetText(string id, string text)
    {
        if (_texts.TryGetValue(id, out var t))
        {
            t.Text = text;
        }
        else if (_toggleBoxes.TryGetValue(id, out var box))
        {
            box.Content = text;   // a switch's label (Layers: the group and its count)
        }
    }

    /// <summary>Moves a slider to a value without raising Changed (nothing for one the card does not have).</summary>
    public void SetNumber(string id, float value)
    {
        if (_sliders.TryGetValue(id, out var slider))
        {
            _setting = true;
            slider.Value = value;
            _setting = false;
            _numbers[id] = (float)slider.Value;
        }
    }

    /// <summary>Moves a slider by an amount as if dragged (Changed raised, remembered).</summary>
    public void Nudge(string id, float by)
    {
        if (_sliders.TryGetValue(id, out var slider))
        {
            slider.Value = Math.Clamp(slider.Value + by, slider.Minimum, slider.Maximum);
        }
    }

    /// <summary>Fills a list with cards and picks one (nothing when it already shows them so).</summary>
    public void SetList(string id, IReadOnlyList<(string Title, string Sub, string Tag)> items, int picked)
    {
        if (!_lists.TryGetValue(id, out var list) || (list.Items.SequenceEqual(items) && _choices.GetValueOrDefault(id) == picked))
        {
            return;
        }
        list.Items.Clear();
        list.Items.AddRange(items);
        _choices[id] = picked;
        list.Cards.Children.Clear();
        foreach (var ((title, sub, tag), i) in items.Select((x, i) => (x, i)))
        {
            var face = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            face.Children.Add(new StackPanel { Children = { new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = sub, FontSize = 10.5, Foreground = Ui.Muted } } });
            var tagText = new TextBlock { Text = tag, FontSize = 10.5, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tagText, 1);
            face.Children.Add(tagText);
            var card = new Avalonia.Controls.Primitives.ToggleButton { Content = face, IsChecked = i == picked, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Avalonia.Thickness(10, 5) };
            card.Click += (_, _) =>
            {
                _choices[id] = i;
                foreach (var (c, j) in list.Cards.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Select((c, j) => (c, j)))
                {
                    c.IsChecked = j == i;
                }
                Changed?.Invoke(id);
            };
            list.Cards.Children.Add(card);
        }
    }

    /// <summary>Builds the card for a tool, its controls with the values remembered for it.</summary>
    /// <param name="tool">The tool.</param>
    public void Show(ModExtensions.Tool tool)
    {
        if (ReferenceEquals(Tool, tool))
        {
            return;
        }
        Tool = tool;
        _body.Children.Clear();
        _numbers.Clear();
        _choices.Clear();
        _toggles.Clear();
        _texts.Clear();
        _picked.Clear();
        _sliders.Clear();
        _toggleBoxes.Clear();
        _lists.Clear();
        // The name, a "?" with all the help, and the help's first sentence under them.
        var more = new TextBlock { Text = "?", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Help) };
        ToolTip.SetTip(more, tool.Info.Help);
        var name = new TextBlock { Text = tool.Info.Name, FontSize = 14, FontWeight = FontWeight.SemiBold };
        Grid.SetColumn(more, 1);
        _body.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { name, more } });
        int stop = tool.Info.Help.IndexOf(". ", StringComparison.Ordinal);
        _body.Children.Add(new TextBlock { Text = stop > 0 ? tool.Info.Help[..(stop + 1)] : tool.Info.Help, FontSize = 11.5, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        Panel into = _body;
        foreach (var c in tool.Controls)
        {
            if (c is SectionControl section)
            {
                into = Section(section);
                continue;
            }
            var made = Make(c);
            if (c.Help != null)
            {
                ToolTip.SetTip(made, c.Help);
            }
            into.Children.Add(made);
        }
        if (tool.Mod != MapTools.Own)
        {
            _body.Children.Add(new TextBlock { Text = Lang.T("mods.tool.from", tool.Mod), FontSize = 10, Foreground = Ui.Muted });
        }
        AddTail();
    }

    /// <summary>One control on the card.</summary>
    private Control Make(ToolControl c) => c switch
    {
        SliderControl s => Slider(s),
        ChoiceControl ch => Choices(ch),
        ToggleControl t => Toggle(t),
        ButtonControl b => Button(b),
        PaletteControl p => Palette(p),
        ListControl l => List(l),
        _ => Text((TextControl)c),
    };

    private string Key(string id) => "mod." + Tool!.Name + "." + id;

    private Grid Slider(SliderControl s)
    {
        float value = Math.Clamp(_prefs?.Get(Key(s.Id), s.Value) ?? s.Value, s.Min, s.Max);
        _numbers[s.Id] = value;
        var shown = new TextBlock();
        string Format(double v) => v.ToString(s.Step >= 1 || (s.Step == 0 && s.Max - s.Min > 50) ? "0" : "0.##", CultureInfo.InvariantCulture) + (s.Unit != null ? " " + s.Unit : "");
        var slider = Ui.Slide(s.Min, s.Max, s.Step > 0 ? s.Step : (s.Max - s.Min) / 100, value, shown, Format, v =>
        {
            _numbers[s.Id] = (float)v;
            if (_setting)
            {
                return;
            }
            _prefs?.Set(Key(s.Id), (float)v);
            Changed?.Invoke(s.Id);
        });
        slider.IsSnapToTickEnabled = s.Step > 0;
        _sliders[s.Id] = slider;
        // The value can be typed (Enter or leaving the box sets it; a unit typed after it is ignored).
        var typed = new TextBox { Text = shown.Text, FontSize = 12, MinWidth = 0, MinHeight = 0, Padding = new Avalonia.Thickness(4, 2), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(typed, $"{s.Label}: type a value from {s.Min} to {s.Max}");
        shown.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty && !typed.IsFocused)
            {
                typed.Text = shown.Text;
            }
        };
        void Take()
        {
            string digits = new([.. (typed.Text ?? "").TakeWhile(ch => char.IsDigit(ch) || ch is '.' or '-' or ',')]);
            if (float.TryParse(digits.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                slider.Value = Math.Clamp(v, s.Min, s.Max);
            }
            typed.Text = shown.Text;
        }
        typed.LostFocus += (_, _) => Take();
        typed.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Take();
                e.Handled = true;
            }
        };
        return Ui.Row(s.Label, slider, typed, "124,*,64");
    }

    private Grid Choices(ChoiceControl ch)
    {
        int value = Math.Clamp(_prefs?.Get(Key(ch.Id), 0) ?? 0, 0, ch.Options.Count - 1);
        _choices[ch.Id] = value;
        var box = new ComboBox { ItemsSource = ch.Options, SelectedIndex = value, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) =>
        {
            _choices[ch.Id] = Math.Max(0, box.SelectedIndex);
            _prefs?.Set(Key(ch.Id), _choices[ch.Id]);
            Changed?.Invoke(ch.Id);
        };
        return Ui.Row(ch.Label, box, null, "70,*,0");
    }

    private CheckBox Toggle(ToggleControl t)
    {
        bool value = _prefs?.Get(Key(t.Id), t.Value) ?? t.Value;
        _toggles[t.Id] = value;
        var box = new CheckBox { Content = t.Label, IsChecked = value, FontSize = 12 };
        _toggleBoxes[t.Id] = box;
        box.IsCheckedChanged += (_, _) =>
        {
            _toggles[t.Id] = box.IsChecked == true;
            _prefs?.Set(Key(t.Id), _toggles[t.Id]);
            Changed?.Invoke(t.Id);
        };
        return box;
    }

    private Button Button(ButtonControl b)
    {
        var button = new Button { Content = b.Label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => Changed?.Invoke(b.Id);
        return button;
    }

    private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap?> Pictures = [];

    internal static Avalonia.Media.Imaging.Bitmap? Picture(string? file)
    {
        if (file == null)
        {
            return null;
        }
        if (!Pictures.TryGetValue(file, out var b))
        {
            try
            {
                b = File.Exists(file) ? new Avalonia.Media.Imaging.Bitmap(file) : null;
            }
            catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException)
            {
                b = null;
            }
            Pictures[file] = b;
        }
        return b;
    }

    /// <summary>
    /// A palette : a tab per group (All: every one), a search box over the items'
    /// pictures, the picked ones as chips under them; a click picks an item (one, or toggles it in a multi palette).
    /// </summary>
    private StackPanel Palette(PaletteControl p)
    {
        string saved = _prefs?.Get(Key(p.Id), "") ?? "";
        var picked = saved.Length > 0
            ? [.. saved.Split(',').Select(s => int.TryParse(s, out int i) ? i : -1).Where(i => i >= 0 && i < p.Items.Count)]
            : p.Picked.Where(i => i >= 0 && i < p.Items.Count).ToList();
        if (!p.Multi && picked.Count == 0 && p.Items.Count > 0)
        {
            picked.Add(0);
        }
        _picked[p.Id] = picked;
        if (PalettesElsewhere)
        {
            return new StackPanel { IsVisible = false };
        }
        var search = new TextBox { PlaceholderText = "Search " + p.Label.ToLowerInvariant(), FontSize = 12 };
        var wrap = new WrapPanel();
        var tabs = new WrapPanel();
        var chips = new WrapPanel();
        var buttons = new List<(int Index, string Group, Avalonia.Controls.Primitives.ToggleButton Button)>();
        var tabButtons = new List<(string Group, Avalonia.Controls.Primitives.ToggleButton Button)>();
        string tab = _prefs?.Get(Key(p.Id) + ".tab", "All") ?? "All";
        void Pick(int i)
        {
            var list = _picked[p.Id];
            if (p.Multi)
            {
                if (!list.Remove(i))
                {
                    list.Add(i);
                }
            }
            else
            {
                list.Clear();
                list.Add(i);
            }
            Mark();
            _prefs?.Set(Key(p.Id), string.Join(",", list));
            Changed?.Invoke(p.Id);
        }
        // The picked items as chips (a multi palette): a click takes one out.
        void Mark()
        {
            foreach (var (i, _, b) in buttons)
            {
                b.IsChecked = _picked[p.Id].Contains(i);
            }
            chips.Children.Clear();
            if (!p.Multi)
            {
                return;
            }
            foreach (int i in _picked[p.Id])
            {
                var chip = new Button { Content = p.Items[i].Name + "  ×", FontSize = 10.5, Padding = new Avalonia.Thickness(7, 1), Margin = new Avalonia.Thickness(0, 0, 3, 3), CornerRadius = new Avalonia.CornerRadius(10) };
                ToolTip.SetTip(chip, "Take " + p.Items[i].Name + " out");
                chip.Click += (_, _) => Pick(i);
                chips.Children.Add(chip);
            }
        }
        // One group at a time (All: every one); a search looks in all of them.
        void Filter()
        {
            string q = search.Text?.Trim() ?? "";
            foreach (var (_, g, b) in buttons)
            {
                b.IsVisible = q.Length > 0 ? ((string)b.Tag!).Contains(q, StringComparison.OrdinalIgnoreCase) : tab == "All" || g == tab;
            }
            foreach (var (g, b) in tabButtons)
            {
                b.IsChecked = g == tab;
            }
        }
        var groupNames = p.Items.Select(x => x.Group).Distinct().ToList();
        if (!groupNames.Contains(tab))
        {
            tab = "All";
        }
        foreach (string g in groupNames.Count > 1 ? ["All", .. groupNames] : groupNames)
        {
            var b = new Avalonia.Controls.Primitives.ToggleButton { Content = g == "All" ? "All" : $"{g} {p.Items.Count(x => x.Group == g)}", FontSize = 11, Padding = new Avalonia.Thickness(8, 2), Margin = new Avalonia.Thickness(0, 0, 3, 3) };
            b.Click += (_, _) =>
            {
                tab = g;
                search.Text = "";
                _prefs?.Set(Key(p.Id) + ".tab", g);
                Filter();
            };
            tabButtons.Add((g, b));
            tabs.Children.Add(b);
        }
        foreach (var (item, i) in p.Items.Select((item, i) => (item, i)))
        {
            var content = new StackPanel { Width = 58 };
            if (Picture(item.Picture) is { } bmp)
            {
                content.Children.Add(new Image { Source = bmp, Width = 48, Height = 48 });
            }
            content.Children.Add(new TextBlock { Text = item.Name, FontSize = 9.5, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxHeight = 26 });
            var b = new Avalonia.Controls.Primitives.ToggleButton { Content = content, Padding = new Avalonia.Thickness(2), Margin = new Avalonia.Thickness(1), Tag = item.Name };
            ToolTip.SetTip(b, item.Name + " (" + item.Group + ")");
            b.Click += (_, _) => Pick(i);
            buttons.Add((i, item.Group, b));
            wrap.Children.Add(b);
        }
        search.TextChanged += (_, _) => Filter();
        Mark();
        Filter();
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = p.Label, FontSize = 12, FontWeight = FontWeight.SemiBold },
                tabs,
                search,
                new ScrollViewer { MaxHeight = 300, Content = wrap },
                chips,
            },
        };
    }

    /// <summary>A heading that folds what follows it: returns where the controls under it go.</summary>
    private StackPanel Section(SectionControl s)
    {
        bool folded = _prefs?.Get(Key(s.Id), s.Folded) ?? s.Folded;
        var under = new StackPanel { Spacing = 6, IsVisible = !folded };
        var head = new Button
        {
            Content = (folded ? "▸ " : "▾ ") + s.Label.ToUpperInvariant(),
            FontSize = 10.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Ui.Muted,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        head.Click += (_, _) =>
        {
            under.IsVisible = !under.IsVisible;
            head.Content = (under.IsVisible ? "▾ " : "▸ ") + s.Label.ToUpperInvariant();
            _prefs?.Set(Key(s.Id), !under.IsVisible);
        };
        _body.Children.Add(head);
        _body.Children.Add(under);
        return under;
    }

    /// <summary>A list: its heading, then the cards the tool puts in it (SetList).</summary>
    private StackPanel List(ListControl l)
    {
        var cards = new StackPanel { Spacing = 4 };
        _lists[l.Id] = (cards, []);
        _choices[l.Id] = 0;
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = l.Label, FontSize = 12, FontWeight = FontWeight.SemiBold }, cards } };
    }

    private TextBlock Text(TextControl t)
    {
        var block = new TextBlock { Text = t.Label, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _texts[t.Id] = block;
        return block;
    }
}
