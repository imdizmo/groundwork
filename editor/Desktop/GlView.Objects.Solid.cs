using System.Numerics;

namespace Groundwork.Desktop;

// Where an object is drawn: its placement in view space.
public sealed partial class GlView
{
    /// <summary>
    /// Where an object goes, in view space: Unity's rotation (Euler degrees, applied z, x, then y)
    /// mirrored in z, like the web editor's objects.js.
    /// </summary>
    private static Matrix4x4 Placement(WorldScene s, WorldScene.Thing t, Vector3 rootScale)
    {
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y * D, t.Rotation.X * D, t.Rotation.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var scale = t.Scale > 0 ? new Vector3(t.Scale) : rootScale;
        var pos = ToView(s, t.Position);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(pos);
    }
}
