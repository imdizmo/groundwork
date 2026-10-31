using System.Numerics;

namespace Groundwork.Desktop;

// The brush's ring on the ground, and the Mountain tool's preview (GlView.Overlays.Brush.Ring.cs, .Mountain.cs).
public sealed partial class GlView
{
    /// <summary>The brush ring's lines and its falloff ring's, on the graphics card.</summary>
    private uint _ringVao, _ringVbo, _falloffVao, _falloffVbo;

    /// <summary>
    /// The brush's outline on the ground (and the Ring shape's inner edge), seen through what stands on it.
    /// Short segments, so a wide ring follows hills instead of cutting through them; none outside the open
    /// area (no ground there). For the Mountain tool: the mountain itself as a mesh, and the ring in red
    /// where it does not fit.
    /// </summary>
    /// <summary>How far out (a fraction of the radius) a falloff still works at half strength.</summary>
    private static float HalfStrength(Falloff f)
    {
        for (float d = 0; d < 1; d += 0.01f)
        {
            if (Brush.Fade(f, 1 - d) < 0.5f)
            {
                return d;
            }
        }
        return 1;
    }

    private unsafe void DrawBrush(WorldScene s, Matrix4x4 vp)
    {
        if (_hover is not { } h || s.Session is not { } session || Tool == null)
        {
            return;
        }
        int n = Math.Clamp((int)MathF.Ceiling(MathF.Tau * session.Brush.Radius / 1.5f), 96, 2048);
        DrawLines(ref _ringVao, ref _ringVbo, BrushRing(s, h, session, n), vp, new Vector4(1, 1, 1, 1), width: 1.2f);
        // Where the brush works at half strength (its falloff), amber, brighter the stronger it is.
        if (Tool != null && HalfStrength(session.Brush.Falloff) is float half and > 0.03f and < 0.97f)
        {
            float a = 0.3f + (0.7f * Math.Clamp(session.Brush.Strength, 0, 1));
            DrawLines(ref _falloffVao, ref _falloffVbo, BrushRing(s, h, session, n, half), vp, new Vector4(0.94f, 0.66f, 0.23f, a), width: 1f);
        }
    }
}
