using Avalonia;

namespace Groundwork.Desktop;

// The left press given to the tools that draw or place: Place, Paste, Area and Path.
public sealed partial class GlView
{
    /// <summary>The Paste tool takes the press on the cell under it: the paste is placed there.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Paste tool is in use and took it.</returns>
    private bool PressPaste(Point at)
    {
        if (Mode != ToolMode.Paste)
        {
            return false;
        }
        _dragFrom = null;
        if (GridAt(at, _surfaceSize) is { } cell)
        {
            PasteClicked?.Invoke(cell);
        }
        Wake();
        return true;
    }
}
