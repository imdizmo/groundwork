using Avalonia.Controls;
using Groundwork.App;

namespace Groundwork.Desktop;

// The view's switches: the kinds shown, the water, the game look, see-through buildings, the 3D resolution, and the size drawn at.
public sealed partial class GlView
{
    /// <summary>Which kinds of objects are drawn, by kind.</summary>
    private readonly bool[] _shown = [.. ObjectKinds.All.Select(ObjectKinds.ShownAtFirst)];
    /// <summary>Whether objects of this kind are drawn.</summary>
    public bool IsShown(ObjectKind k) => _shown[(int)k];
    /// <summary>Shows or hides objects of a kind.</summary>
    public void SetShown(ObjectKind k, bool on)
    {
        _shown[(int)k] = on;
        // Which markers are drawn changes, not which things are spawn points or nests: _spawns is kept.
        _marksDirty = true;
        Wake();
    }
    /// <summary>Steep ground: the ground steeper than this many degrees is drawn red (0: off).</summary>
    public float SteepAngle { get => _steepAngle; set { _steepAngle = value; Wake(); } }
    private volatile float _steepAngle;

    /// <summary>Whether the water is drawn.</summary>
    private volatile bool _showWater = true;
    /// <summary>Whether the water is drawn.</summary>
    public bool ShowWater { get => _showWater; set { _showWater = value; Wake(); } }

    /// <summary>
    /// View, Look: objects drawn with their models or as boxes; see-through buildings (buildings drawn faint, to see
    /// what is inside or behind them); and how many pixels the 3D view draws (Sharp: the screen's;
    /// Balanced: one per point; Fast: three quarters of that), fewer pixels for more frames.
    /// </summary>
    public bool ModelsOn
    {
        get => _modelsOn;
        set
        {
            if (_modelsOn != value)
            {
                _modelsOn = value;
                // The models or their boxes: the objects are made again.
                _thingsReset = true;
                Wake();
            }
        }
    }
    /// <summary>Objects drawn with their models (backs <c>ModelsOn</c>).</summary>
    private volatile bool _modelsOn = true;
    /// <summary>Buildings drawn faint, to see what is inside or behind them.</summary>
    public bool SeeThroughBuildings { get => _seeThrough; set { _seeThrough = value; Wake(); } }
    /// <summary>Backs <c>SeeThroughBuildings</c>.</summary>
    private volatile bool _seeThrough;
    /// <summary>How many pixels the 3D view draws: the screen's, one per point, or three quarters of that.</summary>
    public enum Resolution
    {
        /// <summary>One pixel per screen pixel.</summary>
        Sharp,
        /// <summary>One pixel per point (fewer on a scaled screen).</summary>
        Balanced,
        /// <summary>Three quarters of Balanced.</summary>
        Fast,
    }
    /// <summary>How many pixels the 3D view draws (see <c>PixelScale</c>).</summary>
    public Resolution Resolution3D { get => _resolution; set { _resolution = value; Wake(); } }
    /// <summary>Backs <c>Resolution3D</c>.</summary>
    private volatile Resolution _resolution = Resolution.Sharp;

    /// <summary>Pixels drawn per screen point for a resolution, on a screen of this scaling.</summary>
    internal static double PixelScale(Resolution r, double screen) => r switch
    {
        Resolution.Balanced => Math.Min(screen, 1),
        Resolution.Fast => Math.Min(screen, 1) * 0.75,
        _ => screen,
    };

    /// <summary>The size in pixels the 3D view draws at now.</summary>
    internal (int W, int H) RenderSize()
    {
        double scale = PixelScale(_resolution, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        return (Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)));
    }
}
