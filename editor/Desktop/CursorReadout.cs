using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The status bar's cursor readout (the web editor's): where the pointer is on the ground in world
/// metres, the ground's height there against the original (and whether it is at the game's ±8 m
/// limit), and the zone. Grid coordinates as the 3D view gives them (one point per metre from the
/// area's south-west corner).
/// </summary>
public static class CursorReadout
{
    /// <summary>The readout for a grid point (rounded and clamped to the area).</summary>
    /// <param name="s">The scene; without a session only the height is given.</param>
    /// <param name="gx">Grid x.</param>
    /// <param name="gz">Grid z.</param>
    public static string Text(WorldScene s, float gx, float gz)
    {
        int x = Math.Clamp((int)MathF.Round(gx), 0, s.W - 1), z = Math.Clamp((int)MathF.Round(gz), 0, s.H - 1);
        int g = z * s.W + x;
        int wx = (int)s.OriginX + x, wz = (int)s.OriginZ + z;
        string height;
        if (s.Session?.Ground is { } ground)
        {
            float h = ground.HeightOf(g), b = ground.Original(g), d = h - b;
            string sign = d >= 0 ? "+" : "";
            height = Lang.T("cursor.groundEdited", h, b, sign, d);
        }
        else
        {
            height = Lang.T("cursor.ground", s.Heights[g]);
        }
        return Lang.T("cursor.at", wx, wz, height);
    }
}
