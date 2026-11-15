using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Path = Avalonia.Controls.Shapes.Path;

namespace Groundwork.Desktop;

/// <summary>
/// The Ground graph's window: the nodes on a canvas (drag a card's title to move it, drag from an output's dot to an
/// input's dot to join them, click a joined input's dot to unjoin it, drag the empty canvas to look round), the picked
/// node's numbers and its ground in 3D on the right, and the Output applied to the map as one step in History or saved
/// as a heightmap. The graph is kept beside the map (ground-graph.json).
/// </summary>
public sealed partial class GroundGraphWindow : Window
{
    private const double NodeWidth = 190, HeadHeight = 30, PortRow = 22;

    private readonly GroundNodes.Where _at;
    private readonly string? _file;
    private readonly Func<float[], string>? _apply;
    private readonly FieldCache _cache = new();
    private readonly Canvas _wires = new() { IsHitTestVisible = false }, _nodes = new(), _surface = new() { Background = Brushes.Transparent, ClipToBounds = true };
    private readonly TranslateTransform _pan = new();
    private readonly StackPanel _settings = new() { Spacing = 6 };
    private readonly TerrainPreview _preview = new();
    private readonly TextBlock _status = new() { FontSize = 12, Foreground = Tokens.Dim, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _later = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Path _dragging = new() { Stroke = Tokens.Accent, StrokeThickness = 2, IsHitTestVisible = false };
    private (int From, Point At)? _joining;
    private (int Node, Point Grab)? _moving;
    private Point? _panFrom;
    private bool _busy, _again;

    /// <summary>The graph as it is now.</summary>
    internal GroundGraph Graph { get; }

    /// <summary>The node picked, if any: its numbers shown, its ground previewed.</summary>
    internal int? Picked { get; private set; }

    /// <summary>The window over a graph.</summary>
    /// <param name="graph">The graph to edit.</param>
    /// <param name="at">The ground's size and place (and the open map's ground, for its node).</param>
    /// <param name="file">Where the graph is kept (written as it changes); null: nowhere.</param>
    /// <param name="apply">Applies the Output's field to the map, returning what was done; null: no map to apply it to.</param>
    internal GroundGraphWindow(GroundGraph graph, GroundNodes.Where at, string? file, Func<float[], string>? apply)
    {
        (Graph, _at, _file, _apply) = (graph, at, file, apply);
        Title = "Ground graph";
        Width = 1400;
        Height = 860;
        Background = Tokens.Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _later.Tick += (_, _) =>
        {
            _later.Stop();
            _ = Work();
        };
        _nodes.RenderTransform = _pan;
        _wires.RenderTransform = _pan;
        _surface.Children.Add(_wires);
        _surface.Children.Add(_nodes);
        _surface.Children.Add(_dragging);
        _dragging.RenderTransform = _pan;
        _surface.PointerPressed += (_, e) =>
        {
            if (e.Source == _surface)
            {
                _panFrom = e.GetPosition(_surface);
                e.Pointer.Capture(_surface);
            }
        };
        _surface.PointerMoved += (_, e) => Moved(e.GetPosition(_surface));
        _surface.PointerReleased += (_, e) => Released(e.GetPosition(_surface));
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && Picked is int id && FocusManager?.GetFocusedElement() is not TextBox)
            {
                Remove(id);
                e.Handled = true;
            }
        };

        var add = new Button { Content = "Add a node", FontSize = 12 };
        var menu = new MenuFlyout();
        foreach (var group in GroundNodes.Kinds.GroupBy(k => k.Group))
        {
            var sub = new MenuItem { Header = group.Key };
            foreach (var k in group)
            {
                var item = new MenuItem { Header = k.Name };
                ToolTip.SetTip(item, k.Help);
                item.Click += (_, _) => AddNode(k.Id);
                sub.Items.Add(item);
            }
            menu.Items.Add(sub);
        }
        add.Flyout = menu;
        var applyButton = new Button { Content = "Apply to the map", FontSize = 12, IsEnabled = apply != null }.Classed("primary");
        ToolTip.SetTip(applyButton, apply != null ? "The Output's ground becomes the map's: one step in History (Ctrl+Z takes it back)." : "No map open to apply it to.");
        applyButton.Click += async (_, _) => await ApplyOutput();
        var save = new Button { Content = "Save as a heightmap…", FontSize = 12 };
        save.Click += async (_, _) => await SaveHeightmap();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 8), Children = { add, applyButton, save } };
        var hint = new TextBlock { Text = "Drag a title to move a node · drag from an output's dot to an input's to join them · click a joined input's dot to unjoin it · drag the canvas to look round · Delete takes the picked node out", FontSize = 11, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
        var left = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(hint, Dock.Bottom);
        left.Children.Add(bar);
        left.Children.Add(hint);
        left.Children.Add(new Border { Child = _surface, BorderBrush = Tokens.Line, BorderThickness = new Thickness(0, 1), Background = new SolidColorBrush(Color.Parse("#141417")) });
        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(12) };
        right.Children.Add(new ScrollViewer { Content = _settings, MaxHeight = 420 });
        var view = new Border { Child = _preview, CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Tokens.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 10) };
        Grid.SetRow(view, 1);
        right.Children.Add(view);
        Grid.SetRow(_status, 2);
        right.Children.Add(_status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,380") };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Content = grid;
        Closed += (_, _) => _cache.Clear();
        Picked = Graph.Nodes.FirstOrDefault(n => n.Kind == "output")?.Id;
        Redraw();
        Opened += (_, _) => _ = Work();
    }

    /// <summary>Opens the window for a map (its graph from its folder, or a starter one) or for none.</summary>
    internal static void Open(Window owner, MapProject? map, float[]? ground, int cols, int rows, float sea, Func<float[], string>? apply)
    {
        string? file = map != null ? System.IO.Path.Combine(map.Folder, "ground-graph.json") : null;
        GroundGraph graph;
        try
        {
            graph = file != null && File.Exists(file) ? GroundGraph.FromJson(JsonNode.Parse(File.ReadAllText(file))) : ground != null ? GroundGraph.StarterOverMap() : GroundGraph.Starter();
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            graph = ground != null ? GroundGraph.StarterOverMap() : GroundGraph.Starter();
        }
        if (graph.Nodes.Count == 0)
        {
            graph = ground != null ? GroundGraph.StarterOverMap() : GroundGraph.Starter();
        }
        new GroundGraphWindow(graph, new GroundNodes.Where(cols, rows, map?.X0 ?? 0, map?.Y0 ?? 0, sea, ground), file, apply).Show(owner);
    }

    /// <summary>A node of a kind added in view, picked.</summary>
    internal GroundGraph.Node AddNode(string kind)
    {
        var n = Graph.Add(kind, 60 - _pan.X + (Graph.Nodes.Count % 5 * 30), 60 - _pan.Y + (Graph.Nodes.Count % 5 * 30));
        Picked = n.Id;
        Edited();
        return n;
    }

    /// <summary>Joins an output to an input (as a drag from dot to dot does).</summary>
    internal bool Join(int from, int to, int port)
    {
        bool joined = Graph.Join(from, to, port);
        _status.Text = joined ? _status.Text : "That would make a loop: a node cannot be fed by what it feeds.";
        Edited();
        return joined;
    }

    private void Remove(int id)
    {
        Graph.Remove(id);
        Picked = Graph.Nodes.FirstOrDefault(n => n.Kind == "output")?.Id;
        Edited();
    }

    /// <summary>The graph changed: drawn again, kept, worked out again a moment later.</summary>
    private void Edited()
    {
        Redraw();
        Keep();
        _later.Stop();
        _later.Start();
    }

    private void Keep()
    {
        if (_file != null)
        {
            try
            {
                File.WriteAllText(_file, Graph.ToJson().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _status.Text = $"The graph could not be kept: {e.Message}";
            }
        }
    }

    /// <summary>Works the graph out up to the picked node (one at a time; asked again meanwhile, again after) and shows it.</summary>
    internal async Task Work()
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
            int? upTo = Picked;
            _status.Text = "Working it out…";
            var (field, problem) = await Task.Run(() => GroundNodes.Run(Graph, _at, _cache, upTo));
            if (field != null)
            {
                var mesh = await Task.Run(() => PreviewOf(field));
                _preview.Show(mesh);
                string name = upTo is int id && Graph.Nodes.FirstOrDefault(n => n.Id == id) is { } node ? GroundNodes.Of(node.Kind)!.Name : "Output";
                _status.Text = $"{name}: {field.Min():0.#} to {field.Max():0.#} m. Drag the view to turn round it.";
            }
            else
            {
                _status.Text = problem ?? "";
            }
        }
        while (_again);
        _busy = false;
    }

    /// <summary>A field in 3D, rock on its slopes past 35°.</summary>
    private TerrainPreview.Mesh PreviewOf(float[] field)
    {
        int cols = _at.Cols, rows = _at.Rows;
        byte[] mask = new byte[field.Length * 4];
        Parallel.For(0, rows, r =>
        {
            for (int c = 0; c < cols; c++)
            {
                int p = (r * cols) + c;
                mask[(p * 4) + 2] = ZoneGenerator.Slope(field, cols, rows, c, r) > 35 ? (byte)255 : (byte)0;
                mask[(p * 4) + 3] = 255;
            }
        });
        return TerrainPreview.Of(new ZoneGenerator.Zone(field, mask, [], [], new float[field.Length]), cols, rows, _at.X0, _at.Y0, MapProject.NewLayers, false);
    }

    private async Task ApplyOutput()
    {
        if (_apply == null)
        {
            return;
        }
        _status.Text = "Working out the Output…";
        var (field, problem) = await Task.Run(() => GroundNodes.Run(Graph, _at, _cache));
        _status.Text = field != null ? _apply(field) : problem ?? "";
    }

    private async Task SaveHeightmap()
    {
        var (field, problem) = await Task.Run(() => GroundNodes.Run(Graph, _at, _cache));
        if (field == null)
        {
            _status.Text = problem ?? "";
            return;
        }
        if (await Pickers.SaveFile(StorageProvider, "Save the Output as a heightmap", "ground_height.png", [new("16-bit PNG") { Patterns = ["*.png"] }, new("Raw 16-bit (.r16)") { Patterns = ["*.r16", "*.raw"] }]) is string path)
        {
            HeightmapExport.Write(path, field, _at.Cols, _at.Rows);
            _status.Text = $"Saved {System.IO.Path.GetFileName(path)}: {_at.Cols} x {_at.Rows}, 0 m = 32768, 128 steps a metre.";
        }
    }
}
