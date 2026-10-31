using System.Numerics;

namespace Groundwork.Desktop;

// The overlays and the tool lines every overlay is drawn with.
public sealed partial class GlView
{
    /// <summary>A box's twelve edges, as pairs of corners (corner k: bit 0 x, bit 1 y, bit 2 z, set: the high side).</summary>
    private static readonly (int A, int B)[] BoxEdges = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)];

    /// <summary>A box's corner k (bit 0 x, bit 1 y, bit 2 z, set: the high side), as <see cref="BoxEdges"/> numbers them.</summary>
    /// <param name="b">The box.</param>
    /// <param name="k">The corner, 0 to 7.</param>
    internal static Vector3 Corner((Vector3 Min, Vector3 Max) b, int k) =>
        new((k & 1) == 0 ? b.Min.X : b.Max.X, (k & 2) == 0 ? b.Min.Y : b.Max.Y, (k & 4) == 0 ? b.Min.Z : b.Max.Z);

    /// <summary>Half a tool line's width per metre from the eye (set each frame from the view's size).</summary>
    private float _linePx = 0.0015f;

    /// <summary>
    /// A small flat square round a point as line pairs, its size a fixed share of the distance to the eye.
    /// </summary>
    /// <param name="into">Where the pairs go.</param>
    /// <param name="c">The point, in view space.</param>
    /// <returns>Its half width.</returns>
    private float Marker(List<float> into, Vector3 c)
    {
        float r = Vector3.Distance(_lastEye, c) * 0.008f;
        Vector3[] k = [c + new Vector3(-r, 0, -r), c + new Vector3(r, 0, -r), c + new Vector3(r, 0, r), c + new Vector3(-r, 0, r)];
        for (int j = 0; j < 4; j++)
        {
            into.AddRange([k[j].X, k[j].Y, k[j].Z, k[(j + 1) % 4].X, k[(j + 1) % 4].Y, k[(j + 1) % 4].Z]);
        }
        return r;
    }

    /// <summary>
    /// Puts the line shader to use (made at first use) with this view × projection.
    /// </summary>
    /// <returns>Its colour uniform's location.</returns>
    private unsafe int UseLineProgram(Matrix4x4 viewProj)
    {
        if (_lineProg == 0)
        {
            _lineProg = Program(Shaders.LineVs, Shaders.LineFs);
        }
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_u.At(_lineProg, "uViewProj"), 1, false, (float*)&viewProj);
        return _u.At(_lineProg, "uColor");
    }
}
