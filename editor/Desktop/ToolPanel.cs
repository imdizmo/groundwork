using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Which tool is in use: View (look around, click picks), Select, Measure, Paste (Ctrl+V), a brush (Tool says which),
/// or a map tool (Mod).
/// </summary>
public enum ToolMode
{
    /// <summary>Look around; a click picks an object.</summary>
    View,
    /// <summary>The Select tool.</summary>
    Select,
    /// <summary>The Measure tool.</summary>
    Measure,
    /// <summary>Pasting what the Area tool copied (Ctrl+V).</summary>
    Paste,
    /// <summary>A sculpt or paint brush (Tool says which).</summary>
    Brush,
    /// <summary>A mod's own tool (ToolPanel.ModTool).</summary>
    Mod,
}

/// <summary>
/// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
/// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
/// brushes and the paint brushes. Keys as in the web editor: 1-9 and 0 pick the brushes, E selects,
/// Esc goes back to View.
/// </summary>
public sealed partial class ToolPanel
{
    /// <summary>The brush shapes before the stamps, in BrushShape's order, in the language in use.</summary>
    private static string[] BuiltInShapes() =>
        [Lang.T("brush.shape.circle"), Lang.T("brush.shape.square"), Lang.T("brush.shape.ring"), Lang.T("brush.shape.ragged")];

    /// <summary>The brush settings the options change (size, strength, shape, falloff, each tool's own).</summary>
    public Brush Brush { get; } = new();
    /// <summary>The brush in use; null when the mode is not Brush.</summary>
    public BrushTool? Tool { get; private set; }
    /// <summary>The tool in use (Brush when <c>Tool</c> is set).</summary>
    public ToolMode Mode { get; private set; }
    /// <summary>The Select tool is in use.</summary>
    public bool SelectMode => Mode == ToolMode.Select;
    /// <summary>The tool changed (Mode and Tool say which).</summary>
    public event Action<BrushTool?>? ToolChanged;

    /// <summary>The tool rail card, on the window's left edge.</summary>
    public Control Rail { get; }
    /// <summary>The brush options card, shown only while a brush is in use.</summary>
    public Control Options { get; }

    /// <summary>The rail button of each brush.</summary>
    private readonly Dictionary<BrushTool, Button> _buttons = [];

    /// <summary>The paint buttons' names again (a map names the paints its ground colours).</summary>
    internal void RelabelPaints()
    {
        foreach (var (t, b) in _buttons.Where(kv => IsPaint(kv.Key)))
        {
            if (Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(b).OfType<TextBlock>().FirstOrDefault(x => x.Text?.Length > 1) is { } name)
            {
                name.Text = RailLabel(t);
            }
            ToolTip.SetTip(b, Lang.T("rail.brushTip", Brush.Label(t), Keys.FirstOrDefault(k => k.Tool == t).Key ?? "", Brush.Help(t)));
        }
    }

    /// <summary>The rail buttons of the other tools .</summary>
    private readonly Button _viewButton, _measureButton;
    /// <summary>A brush's rail button (tests).</summary>
    internal Button ButtonOf(BrushTool t) => _buttons[t];

    /// <summary>The keys of the web editor's rail.</summary>
    public static readonly (BrushTool Tool, string Key)[] Keys =
    [
        (BrushTool.Raise, "1"), (BrushTool.Lower, "2"), (BrushTool.Flatten, "3"), (BrushTool.Smooth, "4"), (BrushTool.Natural, "0"), (BrushTool.Erode, "O"), (BrushTool.Terrace, "T"), (BrushTool.Hole, ""), (BrushTool.Restore, "5"),
        (BrushTool.PaintLayer2, "6"), (BrushTool.PaintLayer3, "7"), (BrushTool.PaintLayer4, "8"), (BrushTool.PaintLayer1, "9"),
    ];

    /// <summary>A paint brush (the rail's Paint section), not a sculpt one.</summary>
    internal static bool IsPaint(BrushTool t) => t is BrushTool.PaintLayer2 or BrushTool.PaintLayer3 or BrushTool.PaintLayer4 or BrushTool.PaintLayer1;

    /// <summary>The rail's short names (the web editor's).</summary>
    private static string RailLabel(BrushTool t) => t switch
    {
        BrushTool.Natural => Lang.T("rail.label.natural"),
        _ => Brush.Label(t),
    };

    /// <summary>A brush's rail icon (sculpt brushes) or paint colour (paint brushes), the web editor's.</summary>
    private static (string? Icon, IBrush? Swatch) Look(BrushTool t) => t switch
    {
        BrushTool.Raise => ("raise", null),
        BrushTool.Lower => ("lower", null),
        BrushTool.Flatten => ("flatten", null),
        BrushTool.Smooth => ("smooth", null),
        BrushTool.Natural => ("natural", null),
        BrushTool.Erode => ("erode", null),
        BrushTool.Restore => ("restore", null),
        BrushTool.Terrace => ("terrace", null),
        BrushTool.Hole => ("hole", null),
        _ when Brush.MapOpen && Brush.LayerOf(t) is int k and >= 0 && MapProject.Current?.Layers.ElementAtOrDefault(k) is { } layer => (null, new SolidColorBrush(Srgb(layer.Colour))),
        BrushTool.PaintLayer2 => (null, new SolidColorBrush(Color.Parse("#8a6a46"))),
        BrushTool.PaintLayer3 => (null, new SolidColorBrush(Color.Parse("#4e3622"))),
        BrushTool.PaintLayer4 => (null, new SolidColorBrush(Color.Parse("#8d8d88"))),
        _ => (null, new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#5e8a3a"), 0.5), new GradientStop(Color.Parse("#7a6a4a"), 0.5) },
        }),
    };

    private static Color Srgb(System.Numerics.Vector3 linear) =>
        Color.FromRgb(Byte(linear.X), Byte(linear.Y), Byte(linear.Z));

    private static byte Byte(float linear) => (byte)Math.Clamp(MathF.Round(MathF.Pow(Math.Clamp(linear, 0, 1), 1 / 2.2f) * 255), 0, 255);

    /// <summary>Picks the Select tool.</summary>
    public void ChooseSelect() => ChooseMode(ToolMode.Select);

    /// <summary>Picks a tool other than a brush.</summary>
    public void ChooseMode(ToolMode mode) => Choose(null, mode);

    /// <summary>The rail's buttons and captions.</summary>
    private readonly StackPanel _rail;
    /// <summary>[ and ]: the brush one metre smaller or bigger (within the Size slider's range).</summary>
    /// <summary>The brush a tenth bigger or smaller for each step ([ and ], Ctrl+wheel), at least half a metre: fine when small, quick when large.</summary>
    internal void ResizeBrush(int steps)
    {
        double now = SizeSlider.Value, scaled = now * Math.Pow(1.1, steps);
        // at least half a metre a step, so the smallest sizes still move
        double next = steps > 0 ? Math.Max(scaled, now + (0.5 * steps)) : Math.Min(scaled, now + (0.5 * steps));
        SizeSlider.Value = Math.Clamp(Math.Round(next * 2) / 2, SizeSlider.Minimum, SizeSlider.Maximum);
    }

    /// <summary>The flatten height follows the ground where a stroke started (shown rounded to 0.1 m).</summary>
    public void ShowTarget(float h) => TargetBox.Value = (decimal)MathF.Round(h, 1);

    /// <summary>Alt + click with a brush: level to that height (Flatten, not from the stroke's start).</summary>
    public void FlattenTo(float h)
    {
        TargetFromClickBox.IsChecked = false;
        Brush.Target = MathF.Round(h, 1);
        ShowTarget(h);
        Choose(BrushTool.Flatten);
    }
}
