using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Groundwork.Desktop;

// New map over a heightmap from another tool (a terrain tool): the file (any width and height), how many metres
// across it is and the heights its values span (the tools leave them out of the file, and a raw file its width); the
// numbers measured from it, and the zone made over it as it is, at its own scale.
public sealed partial class GeneratorWindow
{
    /// <summary>The heightmap read and its file's name, or null.</summary>
    // ponytail: held whole while the window is open (an 8193² file is 256 MB); shrink it on reading if that bites
    private (HeightmapImport.Heightmap Map, string Name)? _heightmap;

    /// <summary>The heightmap's metres across, and the heights its lowest and highest values stand for.</summary>
    private float _across, _lowest, _highest = 200;

    /// <summary>The heightmap's first row is its south edge (some tools write raw files so), not its north.</summary>
    private bool _southFirst;

    /// <summary>The style box (a heightmap needs a zone style under it).</summary>
    private ComboBox? _styles;

    /// <summary>Flat or rolling ground was picked: the heightmap is let go.</summary>
    private Action? _dropped;

    /// <summary>The rows that pick a heightmap and say what its numbers are.</summary>
    private StackPanel HeightmapRows(Slider size)
    {
        var file = new TextBlock { Text = "None: the generator's own hills", FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var pick = new Button { Content = "From a heightmap…", FontSize = 12 };
        ToolTip.SetTip(pick, "A heightmap from another terrain tool (16-bit PNG, .r16, .raw or .r32, any width and height): the map is made over it, its shape kept.");
        var clear = new Button { Content = "None", FontSize = 12, IsVisible = false };
        string? path = null;
        int? rawCols = null;
        var width = NumberBox(v => rawCols = (int)MathF.Round(v), "A raw file holds no width: its samples across, as your tool wrote it (empty: square).");
        var widthRow = Ui.Row("Samples across", width.Box, null, "110,*,64");
        widthRow.IsVisible = false;
        var across = NumberBox(v => _across = v, "How many metres the heightmap covers west to east (your tool's world size); its north to south follows at the same scale, each side rounded up to Unreal's 63 m squares.");
        var lowest = NumberBox(v => _lowest = v, "The height of the heightmap's black (its lowest value), in metres; under 0 is under the sea.");
        var highest = NumberBox(v => _highest = v, "The height of the heightmap's white (its highest value), in metres: your tool's height range.");
        var southFirst = new CheckBox { Content = "Its first row is the south edge", FontSize = 12 };
        ToolTip.SetTip(southFirst, "Pictures start at the north edge; some tools write raw files from the south. Tick it if the map comes out upside down.");
        var numbers = new StackPanel
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                Ui.Row("Across", across.Box, Unit("m"), "110,*,64"),
                Ui.Row("Black is", lowest.Box, Unit("m"), "110,*,64"),
                Ui.Row("White is", highest.Box, Unit("m"), "110,*,64"),
                southFirst,
            },
        };
        void Show()
        {
            bool on = _heightmap != null;
            numbers.IsVisible = clear.IsVisible = on;
            size.IsEnabled = !on;
            file.Text = _heightmap is { } hm
                ? $"{hm.Name}: {hm.Map.Cols} x {hm.Map.Rows} px, made {Metres} x {MetresZ} m"
                : "None: the generator's own hills";
            across.Show(_across);
            lowest.Show(_lowest);
            highest.Show(_highest);
        }
        void Drop()
        {
            (_heightmap, _ground, _chunksZ) = (null, null, null);
            widthRow.IsVisible = false;
            Show();
            Shown();
            Changed();
        }
        _dropped = Drop;
        void Use(bool first)
        {
            if (_heightmap is not { } hm)
            {
                return;
            }
            float perSample = _across / MathF.Max(1, hm.Map.Cols - 1);
            (int X, int Z) squares;
            try
            {
                squares = HeightmapImport.SquaresFor(hm.Map, perSample);
            }
            catch (InvalidDataException e)
            {
                _status.Text = e.Message;
                _ground = null;
                return;
            }
            // the slider (greyed out) as near as it goes, then the size itself: its handler sets the size too
            size.Value = Math.Min(squares.X, size.Maximum);
            (_chunks, _chunksZ) = squares;
            _ground = HeightmapImport.Metres(_southFirst ? HeightmapImport.Flipped(hm.Map) : hm.Map, perSample, Metres, MetresZ, _lowest, _highest);
            if (!Zone && _styles != null)
            {
                _styles.SelectedIndex = 0;   // its own measuring follows (the style's numbers on the ground)
            }
            else
            {
                // the ground's measures, and the first time nothing that would change its shape
                var m = ZoneGenerator.Measured(_spec, _ground, Metres, MetresZ);
                _spec = first ? m : _spec with { Low = m.Low, High = m.High, HillSize = m.HillSize };
            }
            Show();
            Shown();
            Changed();
        }
        async Task ReadIt(bool first)
        {
            if (path == null)
            {
                return;
            }
            _status.Text = "Reading the heightmap…";
            string ext = Path.GetExtension(path).ToLowerInvariant();
            widthRow.IsVisible = ext is ".r16" or ".raw" or ".r32";
            try
            {
                var (p, cols) = (path, rawCols);
                var map = await Task.Run(() => HeightmapImport.Read(p, widthRow.IsVisible ? cols : null));
                _heightmap = (map, Path.GetFileName(path));
                if (first || _across <= 0)
                {
                    _across = map.Cols - 1;   // a metre a sample unless told otherwise
                }
                Use(first);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _status.Text = $"Not a heightmap this reads: {e.Message}";
            }
        }
        foreach (var n in new[] { across, lowest, highest })
        {
            n.Typed = () => Use(first: false);
        }
        width.Typed = () => _ = ReadIt(first: false);
        southFirst.IsCheckedChanged += (_, _) => { _southFirst = southFirst.IsChecked == true; Use(first: false); };
        pick.Click += async (_, _) =>
        {
            if (await Pickers.File(StorageProvider, "A heightmap from another terrain tool", new("Heightmaps") { Patterns = ["*.png", "*.r16", "*.raw", "*.r32"] }) is not string picked)
            {
                return;
            }
            (path, rawCols) = (picked, null);
            width.Box.Text = "";
            await ReadIt(first: true);
        };
        clear.Click += (_, _) => Drop();
        var row = Ui.Row("Ground", file, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { pick, clear } }, "110,*,Auto");
        return new StackPanel { Spacing = 8, Children = { row, widthRow, numbers } };
    }

    private static TextBlock Unit(string unit) => new() { Text = unit, FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(6, 0, 0, 0) };

    /// <summary>A box for a number: set when typed (Enter or leaving it), then <see cref="NumberInput.Typed"/>.</summary>
    private static NumberInput NumberBox(Action<float> set, string tip)
    {
        var n = new NumberInput(new TextBox { FontSize = 12 }, set);
        ToolTip.SetTip(n.Box, tip);
        n.Box.LostFocus += (_, _) => n.Read();
        n.Box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                n.Read();
            }
        };
        return n;
    }

    /// <summary>A number's box, what it sets, and what follows when it is typed.</summary>
    private sealed class NumberInput(TextBox box, Action<float> set)
    {
        public TextBox Box { get; } = box;
        public Action? Typed { get; set; }
        private string? _shown;

        public void Show(float v) => Box.Text = _shown = v.ToString("0.#", CultureInfo.InvariantCulture);

        public void Read()
        {
            if (Box.Text != _shown && float.TryParse(Box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && float.IsFinite(v))
            {
                set(v);
                _shown = Box.Text;
                Typed?.Invoke();
            }
        }
    }
}
