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

    /// <summary>The kinds with no icon, and where their own picture goes once it is drawn.</summary>
    private readonly List<(string Model, Image Image)> _unpictured = [];
    private readonly WrapPanel _wrap = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly TextBox _search = new() { PlaceholderText = "Search objects", Width = 170, FontSize = 12 };
    private readonly TextBlock _pickedText = new() { FontSize = 12, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _brushWith = new() { Content = "Brush with these", FontSize = 12 };
    private readonly Button _clear = new() { Content = "Clear", FontSize = 12 };
    private readonly ToggleButton _open = new() { FontSize = 12, FontWeight = FontWeight.SemiBold };
    private readonly ScrollViewer _scroll;
    private readonly Button _dock = new Button { FontSize = 12, Padding = new Thickness(8, 2) }.Classed("ghost");
    private readonly DockPanel _head = new() { LastChildFill = true };
    private readonly StackPanel _middle = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly WrapPanel _right = new() { ItemSpacing = 6, LineSpacing = 6 };

    /// <summary>The edge dragged to make the drawer taller (at the bottom) or wider (on the left).</summary>
    private readonly Border _grip = new() { Background = Brushes.Transparent, Focusable = true };
    private double? _gripFrom;
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
        _scroll = new ScrollViewer { Content = _wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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
        _right.Children.AddRange([_pickedText, _clear, _brushWith, _dock]);
        _pickedText.VerticalAlignment = VerticalAlignment.Center;
        _middle.Children.AddRange([_search, new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _tabs }]);
        _head.Children.AddRange([_open, _right, _middle]);
        _dock.Click += (_, _) => DockOn(!Left);
        Left = w.Prefs.Get("map.drawer.side", "bottom") == "left";
        var body = new DockPanel { Children = { _head, _scroll } };
        DockPanel.SetDock(_head, Dock.Top);
        _head.Margin = new Thickness(0, 0, 0, 8);
        Card = Ui.AsCard(new Border { Child = new Panel { Children = { body, _grip } } });
        Card.Margin = new Thickness(8, 0, 8, 44);
        Grip();
        Tabs();
        Docked();
        Marked();
        Pictures();
    }

    /// <summary>The drawer.</summary>
    public Border Card { get; }

    /// <summary>Opens it on All and what the search finds (models just sent in).</summary>
    public void Reveal(string search)
    {
        _open.IsChecked = true;
        _tab = "All";
        Tabs();
        _search.Text = search;
    }

    /// <summary>Opens or closes it (Ctrl+Space).</summary>
    public void Toggle() => _open.IsChecked = _open.IsChecked != true;

    /// <summary>A tool's card shown: its palettes take the drawer's picks (one for a one-pick palette).</summary>
    public void Apply(ModExtensions.Tool tool, ModToolPanel panel)
    {
        foreach (var p in tool.Controls.OfType<PaletteControl>().Where(ModToolPanel.OfObjects))
        {
            panel.SetPicked(p.Id, p.Multi ? _picked : [.. _picked.Take(1)], quiet: true);
        }
    }

    private void Opened()
    {
        bool open = _open.IsChecked == true;
        _w.Prefs.Set("map.drawer.open", open);
        _open.Content = (open ? Left ? "◂ " : "▾ " : Left ? "▸ " : "▴ ") + $"Objects {_kinds.Count}";
        _scroll.IsVisible = _grip.IsVisible = open;
        _middle.IsVisible = _right.IsVisible = open;
        // Shut, it is only its head: on the left, at the top of the column.
        Card.Height = !open || Left ? double.NaN : Size;
        Card.Width = open && Left ? Size : double.NaN;
        Card.VerticalAlignment = Left ? open ? VerticalAlignment.Stretch : VerticalAlignment.Top : VerticalAlignment.Bottom;
    }

    /// <summary>Whether it is docked on the left of the view (else along the bottom).</summary>
    public bool Left { get; private set; }

    /// <summary>Docks it on the left or along the bottom, open (remembered).</summary>
    internal void DockOn(bool left)
    {
        Left = left;
        _w.Prefs.Set("map.drawer.side", left ? "left" : "bottom");
        _open.IsChecked = true;
        Docked();
    }

    /// <summary>Its height at the bottom (110 to 60% of the window), or its width on the left (220 to 560), remembered for each.</summary>
    internal double Size
    {
        get => Left ? Math.Clamp(_w.Prefs.Get("map.drawer.width", 300.0), 220, 560) : Math.Clamp(_w.Prefs.Get("map.drawer.height", 190.0), 110, Math.Max(110, _w.Bounds.Height * 0.6));
        set
        {
            _w.Prefs.Set(Left ? "map.drawer.width" : "map.drawer.height", Math.Round(value));
            Opened();
        }
    }

    /// <summary>Lays it out for its side: along the bottom its head is one row; on the left, rows one under the other.</summary>
    private void Docked()
    {
        Card.HorizontalAlignment = Left ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        DockPanel.SetDock(_open, Left ? Dock.Top : Dock.Left);
        DockPanel.SetDock(_right, Left ? Dock.Top : Dock.Right);
        _right.Margin = Left ? new Thickness(0, 6) : default;
        _middle.Orientation = Left ? Orientation.Vertical : Orientation.Horizontal;
        _middle.Margin = Left ? default : new Thickness(10, 0);
        _search.Width = Left ? double.NaN : 170;
        _dock.Content = Left ? "Dock at the bottom" : "Dock on the left";
        ToolTip.SetTip(_dock, Left ? "Put the objects back along the bottom of the view" : "Put the objects in a column on the left of the view, beside the tools");
        _grip.Cursor = new Cursor(Left ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth);
        // On the card's edge, over its padding.
        double pad = Card.Padding.Left, gripSize = 6;
        _grip.Margin = Left ? new Thickness(0, -pad, -pad - 1, -pad) : new Thickness(-pad, -pad - 1, -pad, 0);
        _grip.Width = Left ? gripSize : double.NaN;
        _grip.Height = Left ? double.NaN : gripSize;
        _grip.HorizontalAlignment = Left ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _grip.VerticalAlignment = Left ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        Opened();
    }

    /// <summary>The edge's drag and keys (arrows: 16 at a time): taller at the bottom, wider on the left.</summary>
    private void Grip()
    {
        ToolTip.SetTip(_grip, "Drag to make the objects bigger or smaller");
        double At(PointerEventArgs e) => Left ? e.GetPosition(_w).X : -e.GetPosition(_w).Y;
        _grip.PointerEntered += (_, _) => _grip.Background = Tokens.AccentLine;
        _grip.PointerExited += (_, _) => _grip.Background = _gripFrom == null ? Brushes.Transparent : Tokens.AccentLine;
        _grip.PointerPressed += (_, e) =>
        {
            _gripFrom = At(e) - Size;
            e.Pointer.Capture(_grip);
            e.Handled = true;
        };
        _grip.PointerMoved += (_, e) =>
        {
            if (_gripFrom is { } from)
            {
                Size = At(e) - from;
            }
        };
        _grip.PointerReleased += (_, e) =>
        {
            _gripFrom = null;
            e.Pointer.Capture(null);
            _grip.Background = Brushes.Transparent;
        };
        _grip.KeyDown += (_, e) =>
        {
            int d = e.Key switch { Key.Up or Key.Right => 16, Key.Down or Key.Left => -16, _ => 0 };
            if (d != 0)
            {
                Size += d;
                e.Handled = true;
            }
        };
    }

    /// <summary>One kind's picture, its name under it (all of it), picked ones outlined.</summary>
    private Border Tile(MapTools.Kind k, int i)
    {
        var content = new StackPanel { Width = 84, Spacing = 2 };
        var image = new Image { Source = ModToolPanel.Picture(k.Icon), Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(image);
        if (image.Source == null)
        {
            _unpictured.Add((k.Model, image));
        }
        content.Children.Add(new TextBlock { Text = k.Label, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        var star = new TextBlock { Text = "★", FontSize = 11, Foreground = Tokens.Accent, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsVisible = Favourite(k.Model) };
        var tile = new Border
        {
            Child = new Grid { Children = { content, star } },
            Padding = new Thickness(3),
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            Background = Tokens.Raised,
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

    /// <summary>
    /// Draws the kinds with no icon from their models, one after another off the UI thread, each shown as it is done.
    /// A store of its own (the view's isn't for other threads), each mesh let go once drawn.
    /// </summary>
    private void Pictures()
    {
        if (_unpictured.Count == 0 || ModelStore.Of(ModelStore.MapModels) is not { } store)
        {
            return;
        }
        var todo = _unpictured.ToList();
        _ = Task.Run(() =>
        {
            foreach (var (model, image) in todo)
            {
                byte[]? png = null;
                try
                {
                    png = ModelThumb.Png(store, model);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    Log.CaughtBrief(e, $"drawing the picture of {model}");
                }
                foreach (var part in store.LoadModel(model)?.Parts ?? [])
                {
                    store.Forget(part.Mesh);
                }
                if (png != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => image.Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png)));
                }
            }
        });
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

    /// <summary>
    /// Picks the kind of an object in the map (a middle click on it): the Object tool takes it unless the Brush is in
    /// use. Its name; null when it is no kind of the catalog.
    /// </summary>
    public string? PickKindOf(int prefab)
    {
        int i = _kinds.FindIndex(k => AssetIds.Of(k.Model) == prefab);
        if (i < 0)
        {
            return null;
        }
        Pick([i]);
        if (_w.Tools.ModTool?.Info.Id != "brush")
        {
            Use("object");
        }
        return _kinds[i].Label;
    }

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
            foreach (var p in tool.Controls.OfType<PaletteControl>().Where(ModToolPanel.OfObjects))
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
            tile.BorderBrush = on ? Tokens.Accent : Brushes.Transparent;
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
        List<MenuItem> items =
        [

                Item(Favourite(k.Model) ? "Take out of Favourites" : "Add to Favourites", () => Favour(k.Model, i)),
                Item($"Select all {count:N0} on the map", () =>
                {
                    _w.Tools.ChooseMode(ToolMode.Select);
                    _w.View.Select([.. Enumerable.Range(0, things!.Count).Where(t => things[t].Prefab == prefab)]);
                }, count > 0),
                Item("Brush with this", () => { Pick([i]); Use("brush"); }),
        ];
        if (k.Library is var (source, id) && Inbox.Library(source) is { } exe)
        {
            items.Add(Item($"Show in {source}", () => _w.Msg(Said.Raw(Inbox.Show(exe, id) is { } why ? $"{source} could not be opened: {why}" : $"Opening {k.Label} in {source}."))));
        }
        return new ContextMenu { ItemsSource = items };
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
