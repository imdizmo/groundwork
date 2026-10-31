using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>The copy of a Select selection (CopyData.Things.cs).</summary>
public sealed partial class CopyData
{
    /// <summary>
    /// The Select tool's copy, objects only: trees and the like each keep their height above the ground where they
    /// land, building parts the selection's shape (their height from the ground at its middle). Null when none can be
    /// copied.
    /// </summary>
    /// <param name="s">The scene the objects are in.</param>
    /// <param name="things">The objects, as indices into the scene's things.</param>
    /// <param name="nameOf">An asset number's name, null when unknown.</param>
    /// <param name="groundAt">The ground's height at a point (x, z).</param>
    public static CopyData? FromThings(WorldScene s, IReadOnlyList<int> things, Func<int, string?> nameOf, Func<float, float, float> groundAt)
    {
        var shown = things.Distinct().Where(i => !s.Things[i].Gone).ToList();
        if (shown.Count == 0)
        {
            return null;
        }
        var list = shown.Select(i => s.Things[i]).ToList();
        float cx = list.Average(t => t.Position.X), cz = list.Average(t => t.Position.Z);
        float x0 = list.Min(t => t.Position.X) - cx - 1, x1 = list.Max(t => t.Position.X) - cx + 1, z0 = list.Min(t => t.Position.Z) - cz - 1, z1 = list.Max(t => t.Position.Z) - cz + 1;
        float mid = groundAt(cx, cz);
        var objs = list.Select(t => new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(), t.Position.X - cx, t.Position.Z - cz,
            t.Position.Y - (t.Piece ? mid : groundAt(t.Position.X, t.Position.Z)), t.Rotation, t.Scale, Follow: !t.Piece, Data: s.Session?.Edits.FindAdded(t.Id)?.Data)).ToList();
        return new CopyData
        {
            W = 1,
            H = 1,
            Rel = [float.NaN],
            Wt = [0f],
            Pnt = [-1f, -1f, -1f, -1f],
            Objects = objs,
            Poly = [new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)],
            Taken = shown,
            Middle = new Vector3(cx, mid, cz),
        };
    }
}
