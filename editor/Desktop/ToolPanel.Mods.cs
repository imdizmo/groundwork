using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

// Mods' own tools on the rail (ModExtensions.Tools): a section of their own, last.
public sealed partial class ToolPanel
{
    /// <summary>The rail's Mods section: a caption, then a button per mod's tool (empty: nothing shows).</summary>
    private readonly StackPanel _modRail = new() { Spacing = 0 };

    /// <summary>The editor's own tools on the rail, after the paints.</summary>
    private readonly StackPanel _ownRail = new() { Spacing = 0 };

    /// <summary>The rail's section headings (maps rename and hide some).</summary>
    private readonly TextBlock _capSculpt = null!, _capTools = null!, _capPaint = null!;

    /// <summary>The brush card's body (the MapOpen's switcher goes in it).</summary>
    private readonly StackPanel _optionsBody = null!;

    // The MapOpen's rail: one Sculpt and one Paint button in place of the seven ground tools and the
    // four colours; the brush's card then starts with a row of them to switch between (the keys 1-9 as ever).
    private Button? _sculptButton, _paintButton;
    private readonly WrapPanel _switcher = new() { IsVisible = false };
    private BrushTool _lastSculpt = BrushTool.Raise, _lastPaint = Keys.First(k => IsPaint(k.Tool)).Tool;

    /// <summary>The MapOpen's Sculpt and Paint buttons, and the switcher in the brush card, made once.</summary>
    private void MakeMapRail()
    {
        if (_sculptButton != null)
        {
            return;
        }
        _sculptButton = RailButton("Sculpt", "1", Icons.Make("raise", 16));
        ToolTip.SetTip(_sculptButton, "Sculpt the ground: Raise, Lower, Flatten, Smooth, Natural, Erode, Restore (keys 1-5)");
        _sculptButton.Click += (_, _) => Choose(_lastSculpt);
        var (_, swatch) = Look(_lastPaint);
        _paintButton = RailButton("Paint", "6", new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(5), Background = swatch });
        ToolTip.SetTip(_paintButton, "Paint the ground's colour (keys 6-9)");
        _paintButton.Click += (_, _) => Choose(_lastPaint);
        _rail.Children.Insert(_rail.Children.IndexOf(_capSculpt) + 1, _paintButton);
        _rail.Children.Insert(_rail.Children.IndexOf(_capSculpt) + 1, _sculptButton);
        _optionsBody.Children.Insert(1, _switcher);
    }

    /// <summary>The MapOpen's rail and switcher follow the brush chosen.</summary>
    private void MarkMapRail(BrushTool? t)
    {
        if (_sculptButton == null || _paintButton == null)
        {
            return;
        }
        if (t is BrushTool b)
        {
            if (IsPaint(b))
            {
                _lastPaint = b;
            }
            else
            {
                _lastSculpt = b;
            }
        }
        _sculptButton.Classes.Set("on", t is BrushTool s && !IsPaint(s));
        _paintButton.Classes.Set("on", t is BrushTool p && IsPaint(p));
        _switcher.IsVisible = MapOpen && t != null;
        _switcher.Children.Clear();
        if (!_switcher.IsVisible)
        {
            return;
        }
        bool paints = IsPaint(t!.Value);
        foreach (var (tool, key) in Keys.Where(k => IsPaint(k.Tool) == paints && Offered(k.Tool)))
        {
            var (icon, swatch) = Look(tool);
            var face = new Grid
            {
                Width = 30,
                Height = 28,
                Children =
                {
                    icon != null ? Icons.Make(icon, 16) : new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5), Background = swatch },
                    new TextBlock { Text = key, FontSize = 9, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top },
                },
            };
            foreach (var c in face.Children)
            {
                c.HorizontalAlignment = c is TextBlock ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                c.VerticalAlignment = c is TextBlock ? VerticalAlignment.Top : VerticalAlignment.Center;
            }
            var button = new Button { Content = face, Padding = new Thickness(2), Margin = new Thickness(0, 0, 3, 3) }.Classed("rail");
            button.Classes.Set("on", tool == t);
            ToolTip.SetTip(button, key.Length > 0 ? $"{Brush.Label(tool)} ({key})" : Brush.Label(tool));
            button.Click += (_, _) => Choose(tool);
            _switcher.Children.Add(button);
        }
        // Painting needs no height stamp.
        if (LoadStampButton.Parent is Control stampRow)
        {
            stampRow.IsVisible = !paints;
        }
    }

    /// <summary>The heading over Select when it leads the rail (maps).</summary>
    private readonly TextBlock _capSelect = new() { Text = "SELECT", FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false };

    /// <summary>Whether a map is open: its rail (Select first; ground, paint, place, water, map).</summary>
    public bool MapOpen { get; private set; }

    /// <summary>The rail for a map, or the game's again.</summary>
    public void SetMap(bool on)
    {
        if (MapOpen == on)
        {
            return;
        }
        MapOpen = on;
        _capSculpt.Text = on ? "GROUND" : Lang.T("rail.cap.sculpt").ToUpperInvariant();
        MakeMapRail();
        foreach (var (t, b) in _buttons)
        {
            b.IsVisible = !on && Offered(t);
        }
        _capPaint.IsVisible = !on;
        _sculptButton!.IsVisible = _paintButton!.IsVisible = on;
        if (!on)
        {
            _switcher.IsVisible = false;
            if (LoadStampButton.Parent is Control stampRow)
            {
                stampRow.IsVisible = true;
            }
        }
        _ = _rail.Children.Remove(SelectButton);
        _ = _rail.Children.Remove(_capSelect);
        if (on)
        {
            _rail.Children.Insert(0, SelectButton);
            _rail.Children.Insert(0, _capSelect);
            _capSculpt.Margin = new Thickness(0, 5, 0, 1);
        }
        else
        {
            _rail.Children.Insert(_rail.Children.IndexOf(_capTools) + 1, SelectButton);
            _capSculpt.Margin = new Thickness(0, 0, 0, 1);
        }
        _capSelect.IsVisible = on;
    }

    /// <summary>An own tool's rail section (by its id).</summary>
    private static string OwnSection(string id) => id switch
    {
        "river" => "WATER",
        "tunnel" => "CAVES",
        "path" => "PATHS",
        "areas" => "GAME",
        "grow" or "layers" or "checks" or "notes" => "MAP",
        _ => "PLACE",
    };

    private readonly Dictionary<string, Button> _modButtons = [];

    /// <summary>The mod's tool in use (with <see cref="ToolMode.Mod"/>), null otherwise.</summary>
    public ModExtensions.Tool? ModTool { get; private set; }

    /// <summary>The mods' tools on the rail.</summary>
    public IReadOnlyList<ModExtensions.Tool> ModTools { get; private set; } = [];

    /// <summary>
    /// Puts the running extensions' tools on the rail (as a world or area opens); the one in use, gone now, gives way to
    /// View.
    /// </summary>
    /// <param name="tools">The tools.</param>
    public void SetModTools(IReadOnlyList<ModExtensions.Tool> tools)
    {
        ModTools = tools;
        _modRail.Children.Clear();
        _ownRail.Children.Clear();
        _modButtons.Clear();
        if (tools.Any(t => t.Mod != MapTools.Own))
        {
            _modRail.Children.Add(new TextBlock { Text = Lang.T("rail.cap.mods").ToUpperInvariant(), FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 1) });
        }
        string? section = null;
        foreach (var t in tools)
        {
            Control picture = TryIcon(t.Info.Icon) ?? new TextBlock { Text = t.Info.Name[..1].ToUpperInvariant(), FontSize = 13, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            var b = RailButton(t.Info.Name, "", picture);
            ToolTip.SetTip(b, t.Info.Name + " (" + t.Mod + ")\n" + t.Info.Help);
            b.Click += (_, _) => ChooseMod(t);
            if (t.Mod == MapTools.Own && OwnSection(t.Info.Id) != section)
            {
                section = OwnSection(t.Info.Id);
                _ownRail.Children.Add(new TextBlock { Text = section, FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 1) });
            }
            (t.Mod == MapTools.Own ? _ownRail : _modRail).Children.Add(b);
            _modButtons[t.Name] = b;
        }
        if (Mode == ToolMode.Mod && (ModTool == null || !_modButtons.ContainsKey(ModTool.Name)))
        {
            Choose(null);
        }
    }

    /// <summary>A mod's tool's icon from its path data; null when there is none or it cannot be read (logged).</summary>
    private static Control? TryIcon(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }
        try
        {
            var icon = Icons.FromPath(data, 16);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            return icon;
        }
        catch (FormatException e)
        {
            Log.Caught(e, "reading a mod's tool's icon (its first letter is shown instead)");
            return null;
        }
    }

    /// <summary>Chooses a mod's tool.</summary>
    /// <param name="tool">The tool.</param>
    public void ChooseMod(ModExtensions.Tool tool)
    {
        ModTool = tool;
        Choose(null, ToolMode.Mod);
    }

    /// <summary>The "on" look on the mod's tool in use, off on the others.</summary>
    private void MarkModTool()
    {
        foreach (var (name, b) in _modButtons)
        {
            b.Classes.Set("on", Mode == ToolMode.Mod && ModTool?.Name == name);
        }
    }

    /// <summary>A rail button: its picture, the name under it and the key in its corner.</summary>
    private static Button RailButton(string label, string key, Control picture)
    {
        picture.HorizontalAlignment = HorizontalAlignment.Center;
        return new Button
        {
            Content = new Grid
            {
                Children =
                {
                    new StackPanel { Spacing = 1, Children = { picture, new TextBlock { Text = label, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    new TextBlock { Text = key, FontSize = 9, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -1, 0), FontFamily = new FontFamily("monospace") },
                },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(2, 3, 3, 2),
        }.Classed("rail");
    }
}
