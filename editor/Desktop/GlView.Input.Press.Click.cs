using Avalonia;
using Avalonia.Input;

namespace Groundwork.Desktop;

// The left press given to the tools that act on the world under the pointer: Shape and Mountain, Dungeon, Measure
// and Select.
public sealed partial class GlView
{
    /// <summary>The Measure tool takes the press: the tape starts at the world point under it.</summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the Measure tool is in use and took it.</returns>
    private bool PressMeasure(Point at)
    {
        if (Mode != ToolMode.Measure)
        {
            return false;
        }
        _dragFrom = null;
        if (WorldAt(at, _surfaceSize) is { } w)
        {
            Tape.Down(w);
        }
        Wake();
        return true;
    }

    /// <summary>
    /// The Select mode with no tool takes the press: Ctrl or Shift adds to the selection, Shift alone takes the row
    /// from the last object clicked, and a double click opens the inspector.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the select drag is started.</returns>
    private bool PressSelect(PointerPressedEventArgs e, Point at)
    {
        if (Tool != null || !SelectMode)
        {
            return false;
        }
        _dragFrom = null;
        _selectDown = true;
        // The keys say what a click or a zone does to the selection (SelectHow).
        SelectTool.Down(at, _surfaceSize, e.KeyModifiers, e.ClickCount);
        Wake();
        return true;
    }
}
