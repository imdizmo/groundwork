using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Path = Avalonia.Controls.Shapes.Path;

namespace Groundwork.Desktop;

// The Ground graph's canvas: a card a node, its inputs' and output's dots, the wires between them, and the picked node's
// numbers on the right.
public sealed partial class GroundGraphWindow
{
    /// <summary>An input's dot, on the canvas (before the pan).</summary>
    private static Point InputAt(GroundGraph.Node n, int port) => new(n.X, n.Y + HeadHeight + 8 + (port * PortRow) + (PortRow / 2));

    /// <summary>The output's dot.</summary>
    private static Point OutputAt(GroundGraph.Node n) => new(n.X + NodeWidth, n.Y + (HeadHeight / 2));

    /// <summary>The cards, dots and wires drawn again, and the picked node's numbers (unless they are being changed).</summary>
    private void Redraw(bool settings = true)
    {
        _nodes.Children.Clear();
        _wires.Children.Clear();
        foreach (var w in Graph.Wires)
        {
            if (Graph.Nodes.FirstOrDefault(n => n.Id == w.From) is { } a && Graph.Nodes.FirstOrDefault(n => n.Id == w.To) is { } b)
            {
                _wires.Children.Add(new Path { Data = Curve(OutputAt(a), InputAt(b, w.Port)), Stroke = Tokens.LineStrong, StrokeThickness = 2 });
            }
        }
        foreach (var n in Graph.Nodes)
        {
            Card(n);
        }
        if (settings)
        {
            Settings();
        }
    }

    private static Geometry Curve(Point a, Point b)
    {
        double bend = Math.Max(40, Math.Abs(b.X - a.X) / 2);
        return Geometry.Parse(string.Create(CultureInfo.InvariantCulture, $"M {a.X},{a.Y} C {a.X + bend},{a.Y} {b.X - bend},{b.Y} {b.X},{b.Y}"));
    }

    /// <summary>A node's card: its title (drag it to move the node), its inputs' names, and the dots.</summary>
    private void Card(GroundGraph.Node n)
    {
        var kind = GroundNodes.Of(n.Kind)!;
        bool picked = n.Id == Picked;
        var title = new Border
        {
            Height = HeadHeight,
            Background = kind.Group switch { "Make" => new SolidColorBrush(Color.Parse("#2c3a2c")), "Shape" => new SolidColorBrush(Color.Parse("#2c3340")), "Combine" => new SolidColorBrush(Color.Parse("#3a2f40")), "Mask" => new SolidColorBrush(Color.Parse("#40382a")), _ => new SolidColorBrush(Color.Parse("#4a3016")) },
            CornerRadius = new CornerRadius(7, 7, 0, 0),
            Padding = new Thickness(10, 0),
            Cursor = new Cursor(StandardCursorType.SizeAll),
            Child = new TextBlock { Text = kind.Name, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
        };
        ToolTip.SetTip(title, kind.Help);
        title.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(title).Properties.IsLeftButtonPressed)
            {
                Pick(n.Id);
                _moving = (n.Id, e.GetPosition(_surface) - new Point(n.X + _pan.X, n.Y + _pan.Y));
                e.Pointer.Capture(_surface);
                e.Handled = true;
            }
        };
        var body = new StackPanel { Margin = new Thickness(14, 8, 10, 8), Spacing = 0 };
        foreach (string input in kind.Inputs)
        {
            body.Children.Add(new TextBlock { Text = input == "in" ? "ground" : input, Height = PortRow, FontSize = 11, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center });
        }
        if (kind.Inputs.Length == 0)
        {
            body.Children.Add(new TextBlock { Text = Summary(n, kind), Height = PortRow, FontSize = 11, Foreground = Tokens.Dim, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        var card = new Border
        {
            Width = NodeWidth,
            Background = Tokens.Raised,
            BorderBrush = picked ? Tokens.Accent : Tokens.LineStrong,
            BorderThickness = new Thickness(picked ? 2 : 1),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Children = { title, body } },
        };
        card.PointerPressed += (_, e) =>
        {
            Pick(n.Id);
            e.Handled = true;
        };
        Canvas.SetLeft(card, n.X);
        Canvas.SetTop(card, n.Y);
        _nodes.Children.Add(card);
        for (int i = 0; i < kind.Inputs.Length; i++)
        {
            int port = i;
            bool joined = Graph.Into(n.Id, port) != null;
            var dot = Dot(InputAt(n, port), joined);
            ToolTip.SetTip(dot, joined ? "Click to unjoin" : "Drag an output's dot here to join it");
            dot.PointerPressed += (_, e) =>
            {
                if (Graph.Wires.RemoveAll(w => w.To == n.Id && w.Port == port) > 0)
                {
                    Edited();
                }
                e.Handled = true;
            };
            _nodes.Children.Add(dot);
        }
        if (kind.Id != "output")
        {
            var output = Dot(OutputAt(n), true);
            ToolTip.SetTip(output, "Drag to an input's dot to join them");
            output.Cursor = new Cursor(StandardCursorType.Cross);
            output.PointerPressed += (_, e) =>
            {
                _joining = (n.Id, e.GetPosition(_surface));
                e.Pointer.Capture(_surface);
                e.Handled = true;
            };
            _nodes.Children.Add(output);
        }
    }

    private static string Summary(GroundGraph.Node n, GroundNodes.Kind kind) => kind.Id switch
    {
        "hills" => $"{GroundNodes.Value(n, "low"):0} to {GroundNodes.Value(n, "high"):0} m",
        "flat" => $"{GroundNodes.Value(n, "height"):0.#} m",
        "mountain" => $"{Mountain.Presets[Math.Clamp((int)GroundNodes.Value(n, "kind"), 0, Mountain.Presets.Length - 1)].Name}, {GroundNodes.Value(n, "height"):0} m",
        "heightmap" => n.Text.Length > 0 ? System.IO.Path.GetFileName(n.Text) : "no file",
        "formula" => n.Text.Length > 0 ? n.Text : "no formula",
        _ => "",
    };

    private static Ellipse Dot(Point at, bool full)
    {
        var e = new Ellipse { Width = 13, Height = 13, Fill = full ? Tokens.Accent : Tokens.Raised, Stroke = Tokens.Accent, StrokeThickness = 2 };
        Canvas.SetLeft(e, at.X - 6.5);
        Canvas.SetTop(e, at.Y - 6.5);
        return e;
    }

    private void Pick(int id)
    {
        if (Picked != id)
        {
            Picked = id;
            Redraw();
            _later.Stop();
            _later.Start();
        }
    }

    private void Moved(Point at)
    {
        if (_moving is var (id, grab) && Graph.Nodes.FirstOrDefault(n => n.Id == id) is { } n)
        {
            (n.X, n.Y) = (at.X - grab.X - _pan.X, at.Y - grab.Y - _pan.Y);
            Redraw();
        }
        else if (_joining is var (from, _) && Graph.Nodes.FirstOrDefault(n => n.Id == from) is { } f)
        {
            _dragging.Data = Curve(OutputAt(f), new Point(at.X - _pan.X, at.Y - _pan.Y));
        }
        else if (_panFrom is Point p)
        {
            _pan.X += at.X - p.X;
            _pan.Y += at.Y - p.Y;
            _panFrom = at;
        }
    }

    private void Released(Point at)
    {
        if (_joining is var (from, _))
        {
            // the input whose dot is nearest the pointer, within a dot's reach
            var on = new Point(at.X - _pan.X, at.Y - _pan.Y);
            var (node, port, _) = Graph.Nodes.SelectMany(n => Enumerable.Range(0, GroundNodes.Of(n.Kind)!.Inputs.Length).Select(i => (Node: n, Port: i, D: Point.Distance(InputAt(n, i), on))))
                .Where(x => x.D < 14).OrderBy(x => x.D).FirstOrDefault();
            if (node != null)
            {
                _ = Join(from, node.Id, port);
            }
            _dragging.Data = null;
        }
        else if (_moving != null)
        {
            Keep();
        }
        (_joining, _moving, _panFrom) = (null, null, null);
    }

    /// <summary>The picked node's numbers, choices and text, each change worked out again a moment later.</summary>
    private void Settings()
    {
        _settings.Children.Clear();
        if (Picked is not int id || Graph.Nodes.FirstOrDefault(n => n.Id == id) is not { } n || GroundNodes.Of(n.Kind) is not { } kind)
        {
            _settings.Children.Add(new TextBlock { Text = "Click a node to see its numbers and its ground.", FontSize = 12, Foreground = Tokens.Dim });
            return;
        }
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(new TextBlock { Text = kind.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var remove = new Button { Content = "Take it out", FontSize = 11 };
        remove.Click += (_, _) => Remove(n.Id);
        head.Children.Add(Ui.Col(remove, 1));
        _settings.Children.Add(head);
        _settings.Children.Add(new TextBlock { Text = kind.Help, FontSize = 11.5, Foreground = Tokens.Dim, TextWrapping = TextWrapping.Wrap });
        if (kind.TextLabel is string label)
        {
            var text = new TextBox { Text = n.Text, FontSize = 12, PlaceholderText = kind.Id == "formula" ? "h * smooth(1 - sqrt(x*x + z*z) / 300)" : "a heightmap file" };
            text.TextChanged += (_, _) =>
            {
                n.Text = text.Text ?? "";
                Keep();
                _later.Stop();
                _later.Start();
            };
            Control after = new TextBlock();
            if (kind.Id == "heightmap")
            {
                var choose = new Button { Content = "Choose…", FontSize = 11 };
                choose.Click += async (_, _) =>
                {
                    if (await Pickers.File(StorageProvider, "A heightmap", new("Heightmaps") { Patterns = ["*.png", "*.r16", "*.raw", "*.r32", "*.tif", "*.tiff"] }) is string f)
                    {
                        text.Text = f;
                    }
                };
                after = choose;
            }
            _settings.Children.Add(Ui.Row(label, text, after, "70,*,76"));
        }
        foreach (var p in kind.Params)
        {
            _settings.Children.Add(p.Options != null ? Choice(n, p) : Number(n, p));
        }
    }

    private Grid Choice(GroundGraph.Node n, GroundNodes.Param p)
    {
        var box = new ComboBox { ItemsSource = p.Options, SelectedIndex = Math.Clamp((int)GroundNodes.Value(n, p.Id), 0, p.Options!.Length - 1), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) =>
        {
            n.Values[p.Id] = Math.Max(0, box.SelectedIndex);
            Changed(n);
        };
        return Ui.Row(p.Label, box, null, "130,*,0");
    }

    private Grid Number(GroundGraph.Node n, GroundNodes.Param p)
    {
        float v = GroundNodes.Value(n, p.Id);
        string format = p.Step < 1 ? "0.##" : "0";
        var slider = new Slider { Minimum = p.Min, Maximum = p.Max, Value = Math.Clamp(v, p.Min, p.Max), SmallChange = p.Step, TickFrequency = p.Step, IsSnapToTickEnabled = true };
        var box = new TextBox { Text = v.ToString(format, CultureInfo.InvariantCulture), FontSize = 12, MinWidth = 0, MinHeight = 0, Height = 26, Padding = new Thickness(4, 2), HorizontalContentAlignment = HorizontalAlignment.Right };
        bool setting = false;
        slider.ValueChanged += (_, e) =>
        {
            if (setting)
            {
                return;
            }
            n.Values[p.Id] = (float)e.NewValue;
            setting = true;
            box.Text = e.NewValue.ToString(format, CultureInfo.InvariantCulture);
            setting = false;
            Changed(n);
        };
        void Typed()
        {
            if (float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float t) && MathF.Abs(t - GroundNodes.Value(n, p.Id)) > 1e-6f)
            {
                n.Values[p.Id] = t;   // past the slider's ends too
                setting = true;
                slider.Value = Math.Clamp(t, p.Min, p.Max);
                setting = false;
                Changed(n);
            }
        }
        box.LostFocus += (_, _) => Typed();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Typed();
            }
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*,62,30"), ColumnSpacing = 6 };
        row.Children.Add(new TextBlock { Text = p.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        row.Children.Add(Ui.Col(slider, 1));
        row.Children.Add(Ui.Col(box, 2));
        row.Children.Add(Ui.Col(new TextBlock { Text = p.Unit, FontSize = 11, Foreground = Tokens.Dim, VerticalAlignment = VerticalAlignment.Center }, 3));
        return row;
    }

    /// <summary>A node's number changed: kept, its card's line redrawn (a source's), worked out again a moment later.</summary>
    private void Changed(GroundGraph.Node n)
    {
        Keep();
        if (GroundNodes.Of(n.Kind)!.Inputs.Length == 0)
        {
            Redraw(settings: false);   // a source's card shows its numbers; the slider being dragged is left alone
        }
        _later.Stop();
        _later.Start();
    }
}
