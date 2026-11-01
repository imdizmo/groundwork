using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// The map's object drawer, along the bottom of the view: every kind of the catalog as a picture,
/// in tabs (All, Recent, Favourites, each group) with a search over all of them. A click picks one kind (for the Object
/// tool, or the Brush while it is in use), Ctrl+click adds one or takes it out, "Brush with these" hands the picked
/// ones to the Brush, and a picture dragged onto the ground puts one there (the Object tool's ghost shows where).
/// Right click: favourite, select every one of the kind on the map. Ctrl+Space opens and closes it. It is the one
/// place kinds are picked: the Object and Brush cards keep their palettes hidden and follow it (Apply).
/// </summary>
internal sealed class ObjectsDrawer
{
    private const string BrushKey = "mod.Groundwork/brush.kinds";

    /// <summary>The kinds picked, by model name (a list position would pick another kind in another map).</summary>
    private const string PickedKey = "map.drawer.picked";
    private readonly MainWindow _w;
    private readonly List<MapTools.Kind> _kinds;
    private readonly List<int> _picked = [];
    private readonly List<(int Index, Border Tile)> _tiles = [];
    private readonly WrapPanel _wrap = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly TextBox _search = new() { PlaceholderText = "Search objects", Width = 170, FontSize = 12 };
    private readonly TextBlock _pickedText = new() { FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _brushWith = new() { Content = "Brush with these", FontSize = 12 };
    private readonly Button _clear = new() { Content = "Clear", FontSize = 12 };
    private readonly ToggleButton _open = new() { FontSize = 12, FontWeight = FontWeight.SemiBold };
    private readonly ScrollViewer _scroll;
    private string _tab;
    private (int Index, Point From)? _press;
    private bool _dragging;

    /// <summary>A drawer for the window's map.</summary>
    public ObjectsDrawer(MainWindow w)
    {
        _w = w;
        _kinds = MapTools.Catalog().Kinds;
        _tab = w.Prefs.Get("map.drawer.tab", "All");
        var names = w.Prefs.Get(PickedKey, "").Split('|').ToHashSet(StringComparer.Ordinal);
        _picked.AddRange(Enumerable.Range(0, _kinds.Count).Where(i => names.Contains(_kinds[i].Model)));
        _scroll = new ScrollViewer { Height = 124, Content = _wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        foreach (var (k, i) in _kinds.Select((k, i) => (k, i)))
        {
            _tiles.Add((i, Tile(k, i)));
        }
        _wrap.Children.AddRange(_tiles.Select(t => t.Tile));
        _search.TextChanged += (_, _) => Filter();
        _brushWith.Click += (_, _) => Use("brush");
        ToolTip.SetTip(_brushWith, "The Brush, with the picked kinds (Ctrl+click pictures to pick several)");
        _clear.Click += (_, _) => Pick([]);
        _open.IsChecked = w.Prefs.Get("map.drawer.open", true);
        _open.IsCheckedChanged += (_, _) => Opened();
        ToolTip.SetTip(_open, "Open or close the objects (Ctrl+Space)");
        var head = new DockPanel { LastChildFill = true };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _pickedText, _clear, _brushWith } };
        DockPanel.SetDock(_open, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        head.Children.Add(_open);
        head.Children.Add(right);
        head.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(10, 0), Children = { _search, new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _tabs } } });
        Card = Ui.AsCard(new Border { Child = new StackPanel { Spacing = 8, Children = { head, _scroll } } });
        Card.VerticalAlignment = VerticalAlignment.Bottom;
        Card.HorizontalAlignment = HorizontalAlignment.Stretch;
        Card.Margin = new Thickness(10, 0, 10, 58);
        Tabs();
        Opened();
        Marked();
    }

    /// <summary>The drawer.</summary>
    public Border Card { get; }

    /// <summary>Opens or closes it (Ctrl+Space).</summary>
    public void Toggle() => _open.IsChecked = _open.IsChecked != true;

    /// <summary>A tool's card shown: its palettes take the drawer's picks (one for a one-pick palette).</summary>
    public void Apply(ModExtensions.Tool tool, ModToolPanel panel)
    {
        foreach (var p in tool.Controls.OfType<PaletteControl>())
        {
            panel.SetPicked(p.Id, p.Multi ? _picked : [.. _picked.Take(1)], quiet: true);
        }
    }

    private void Opened()
    {
        bool open = _open.IsChecked == true;
        _w.Prefs.Set("map.drawer.open", open);
        _open.Content = (open ? "▾ " : "▴ ") + $"Objects {_kinds.Count}";
        _scroll.IsVisible = open;
        _search.IsVisible = _tabs.IsVisible = open;
    }

    /// <summary>One kind's picture, its name under it (all of it), picked ones outlined.</summary>
    private Border Tile(MapTools.Kind k, int i)
    {
        var content = new StackPanel { Width = 84, Spacing = 2 };
        if (ModToolPanel.Picture(k.Icon) is { } bmp)
        {
            content.Children.Add(new Image { Source = bmp, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center });
        }
        content.Children.Add(new TextBlock { Text = k.Label, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        var star = new TextBlock { Text = "★", FontSize = 11, Foreground = Ui.Accent, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsVisible = Favourite(k.Model) };
        var tile = new Border
        {
            Child = new Grid { Children = { content, star } },
            Padding = new Thickness(3),
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            Background = Ui.Panel2,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = star,
        };
        ToolTip.SetTip(tile, $"{k.Label} ({k.Category})\nClick: place it · Ctrl+click: pick several · drag onto the ground: put one there · right click: more");
        tile.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed)
            {
                _press = (i, e.GetPosition(_w.Surface));
                _dragging = false;
                e.Pointer.Capture(tile);
                e.Handled = true;
            }
        };
        tile.PointerMoved += (_, e) => Moved(e);
        tile.PointerReleased += (_, e) => Released(e);
        tile.ContextRequested += (_, _) => tile.ContextMenu = Menu(k, i);
        return tile;
    }

    /// <summary>A picture held: once it leaves the drawer it is dragged, the Object tool's ghost following it.</summary>
    private void Moved(PointerEventArgs e)
    {
        if (_press is not var (i, from))
        {
            return;
        }
        var at = e.GetPosition(_w.Surface);
        if (!_dragging && Point.Distance(at, from) < 8)
        {
            return;
        }
        if (!_dragging)
        {
            _dragging = true;
            Pick([i]);
            Use("object");
        }
        if (OverView(at))
        {
            _w.View.PointModAt(at, _w.Surface.Bounds.Size, PointerAction.Move, e.KeyModifiers);
        }
    }

    private void Released(PointerReleasedEventArgs e)
    {
        if (_press is not var (i, _))
        {
            return;
        }
        _press = null;
        e.Pointer.Capture(null);
        var at = e.GetPosition(_w.Surface);
        if (_dragging)
        {
            _dragging = false;
            if (OverView(at))
            {
                _w.View.PointModAt(at, _w.Surface.Bounds.Size, PointerAction.Press, e.KeyModifiers);
                _w.View.PointModAt(at, _w.Surface.Bounds.Size, PointerAction.Release, e.KeyModifiers);
                Recent(_kinds[i].Model);
            }
            return;
        }
        // A click: one kind (Ctrl: one more, or one less).
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            Pick(_picked.Contains(i) ? [.. _picked.Where(p => p != i)] : [.. _picked, i]);
            return;
        }
        Pick([i]);
        Recent(_kinds[i].Model);
        if (_w.Tools.ModTool?.Info.Id != "brush")
        {
            Use("object");
        }
    }

    /// <summary>Whether a point of the view's surface is on the view, not on the drawer.</summary>
    private bool OverView(Point at) =>
        at.X >= 0 && at.Y >= 0 && at.X < _w.Surface.Bounds.Width && at.Y < (Card.TranslatePoint(default, _w.Surface)?.Y ?? _w.Surface.Bounds.Height);

    /// <summary>The kinds picked: outlined here, and given to the Object or Brush card in use.</summary>
    private void Pick(List<int> picked)
    {
        _picked.Clear();
        _picked.AddRange(picked);
        _w.Prefs.Set(BrushKey, string.Join(",", _picked));
        _w.Prefs.Set(PickedKey, string.Join("|", _picked.Select(i => _kinds[i].Model)));
        _w.Prefs.Set("mod.Groundwork/object.kind", string.Join(",", _picked.Take(1)));
        Marked();
        if (_w.Tools.ModTool is { } tool && tool.Info.Id is "object" or "brush")
        {
            foreach (var p in tool.Controls.OfType<PaletteControl>())
            {
                _w.ModToolPanel.SetPicked(p.Id, p.Multi ? _picked : [.. _picked.Take(1)]);
            }
        }
    }

    private void Marked()
    {
        foreach (var (i, tile) in _tiles)
        {
            bool on = _picked.Contains(i);
            tile.BorderBrush = on ? Ui.Accent : Brushes.Transparent;
        }
        _pickedText.Text = _picked.Count switch
        {
            0 => "Nothing picked",
            1 => _kinds[_picked[0]].Label,
            _ => $"{_picked.Count} picked",
        };
        _brushWith.IsEnabled = _clear.IsEnabled = _picked.Count > 0;
    }

    /// <summary>Chooses an own tool by id (its card then takes the picks).</summary>
    private void Use(string id)
    {
        if (_w.Tools.ModTools.FirstOrDefault(t => t.Mod == MapTools.Own && t.Info.Id == id) is { } tool && !ReferenceEquals(_w.Tools.ModTool, tool))
        {
            _w.Tools.ChooseMod(tool);
        }
    }

    private ContextMenu Menu(MapTools.Kind k, int i)
    {
        int prefab = AssetIds.Of(k.Model);
        var things = _w.Session?.Scene.Things;
        int count = things?.Count(t => t.Prefab == prefab) ?? 0;
        MenuItem Item(string text, Action click, bool enabled = true)
        {
            var m = new MenuItem { Header = text, IsEnabled = enabled };
            m.Click += (_, _) => click();
            return m;
        }
        return new ContextMenu
        {
            ItemsSource = new[]
            {
                Item(Favourite(k.Model) ? "Take out of Favourites" : "Add to Favourites", () => Favour(k.Model, i)),
                Item($"Select all {count:N0} on the map", () =>
                {
                    _w.Tools.ChooseMode(ToolMode.Select);
                    _w.View.Select([.. Enumerable.Range(0, things!.Count).Where(t => things[t].Prefab == prefab)]);
                }, count > 0),
                Item("Brush with this", () => { Pick([i]); Use("brush"); }),
            },
        };
    }

    private List<string> Listed(string key) => [.. _w.Prefs.Get(key, "").Split('|', StringSplitOptions.RemoveEmptyEntries)];

    private bool Favourite(string model) => Listed("map.favourites").Contains(model);

    private void Favour(string model, int i)
    {
        var list = Listed("map.favourites");
        if (!list.Remove(model))
        {
            list.Add(model);
        }
        _w.Prefs.Set("map.favourites", string.Join("|", list));
        ((TextBlock)_tiles[i].Tile.Tag!).IsVisible = list.Contains(model);
        Tabs();
    }

    /// <summary>A kind used: first in Recent (twelve kept).</summary>
    private void Recent(string model)
    {
        var list = Listed("map.recent");
        list.Remove(model);
        list.Insert(0, model);
        _w.Prefs.Set("map.recent", string.Join("|", list.Take(12)));
        if (_tab == "Recent")
        {
            Filter();
        }
    }

    /// <summary>The tabs: All, Recent, Favourites, then each group with its count.</summary>
    private void Tabs()
    {
        _tabs.Children.Clear();
        var groups = _kinds.Select(k => k.Category).Distinct().ToList();
        foreach (string g in (string[])["All", "Recent", "Favourites", .. groups])
        {
            int n = g switch { "All" => _kinds.Count, "Recent" => Listed("map.recent").Count, "Favourites" => Listed("map.favourites").Count, _ => _kinds.Count(k => k.Category == g) };
            var b = new ToggleButton { Content = g == "All" ? "All" : $"{g} {n}", FontSize = 11.5, Padding = new Thickness(9, 3), IsChecked = g == _tab };
            b.Click += (_, _) =>
            {
                _tab = g;
                _search.Text = "";
                _w.Prefs.Set("map.drawer.tab", g);
                Tabs();
            };
            _tabs.Children.Add(b);
        }
        Filter();
    }

    private void Filter()
    {
        string q = _search.Text?.Trim() ?? "";
        var recent = Listed("map.recent");
        var favourites = Listed("map.favourites");
        foreach (var (i, tile) in _tiles)
        {
            var k = _kinds[i];
            tile.IsVisible = q.Length > 0
                ? k.Label.Contains(q, StringComparison.OrdinalIgnoreCase) || k.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
                : _tab switch { "All" => true, "Recent" => recent.Contains(k.Model), "Favourites" => favourites.Contains(k.Model), _ => k.Category == _tab };
        }
        if (_tab == "Recent" && q.Length == 0)
        {
            // Most recent first.
            _wrap.Children.Clear();
            _wrap.Children.AddRange(_tiles.OrderBy(t => recent.IndexOf(_kinds[t.Index].Model) is int r && r >= 0 ? r : int.MaxValue).Select(t => t.Tile));
        }
        else if (_tiles.Count > 0 && !ReferenceEquals(_wrap.Children.FirstOrDefault(), _tiles[0].Tile))
        {
            _wrap.Children.Clear();
            _wrap.Children.AddRange(_tiles.Select(t => t.Tile));
        }
    }
}
