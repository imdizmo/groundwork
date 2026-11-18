using System.Numerics;

namespace Groundwork.Desktop;

// Picking with a camera that is no longer the view's (a picture Claude was given): what is under a point of that
// picture, and where a world point is on it. Points are fractions of the picture (0..1 across, 0..1 down).
public sealed partial class GlView
{
    /// <summary>The last frame's camera: its view × projection and its eye in world metres (x east, height, z north).</summary>
    internal (Matrix4x4 ViewProj, Vector3 Eye)? LastCamera =>
        Scene is { } s ? (_lastViewProj, new Vector3(_lastEye.X + s.Cx, _lastEye.Y, -_lastEye.Z + s.Cz)) : null;

    /// <summary>
    /// What a camera saw at a point of its picture: the shown object there (as a click picks it), and the ground's
    /// world point (null: the ray meets no ground, the sky).
    /// </summary>
    /// <param name="vp">The camera's view × projection (LastCamera).</param>
    /// <param name="u">Across the picture, 0 (left) to 1.</param>
    /// <param name="v">Down the picture, 0 (top) to 1.</param>
    internal (int? Object, Vector3? Ground) ProbeAt(Matrix4x4 vp, float u, float v)
    {
        if (Scene is not { } s)
        {
            return (null, null);
        }
        var (o, d) = Picking.Ray(vp, (u * 2) - 1, 1 - (v * 2));
        var (hit, best) = NearestMeshHit(s, o, d, BoxesHitBy(o, d));
        float? g = Picking.HitGround(s, o, d);
        if (hit != null && Picking.HitGround(s, o, d, best + 1) is float near && near + 0.5f < best)
        {
            hit = null;
        }
        Vector3? ground = g is float t && o + (d * t) is var p ? new Vector3(p.X + s.Cx, p.Y, -p.Z + s.Cz) : null;
        return (hit, ground);
    }

    /// <summary>Where a world point is on a camera's picture, as fractions (outside 0..1: off the picture), or null behind the camera.</summary>
    /// <param name="vp">The camera's view × projection (LastCamera).</param>
    /// <param name="world">The point, x east, height, z north.</param>
    internal Vector2? FractionOf(Matrix4x4 vp, Vector3 world)
    {
        if (Scene is not { } s)
        {
            return null;
        }
        var q = Vector4.Transform(new Vector4(ToView(s, world), 1), vp);
        return q.W <= 0 ? null : new Vector2(((q.X / q.W) + 1) / 2, (1 - (q.Y / q.W)) / 2);
    }
}
