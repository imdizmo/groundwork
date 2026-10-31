using Groundwork.App;
using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>Objects of a paste (PasteTool.Objects.cs): added where each copy has them.</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Adds the objects of one copy as new objects (with the ground's origin), each placed where the copy has it: those
    /// that follow the ground (trees and the like) keep their height above the ground where they land.
    /// </summary>
    /// <param name="g">The ground the objects are placed on.</param>
    /// <param name="c">The copy.</param>
    /// <param name="ca">The copy's anchor point on the grid.</param>
    /// <param name="lift">The copy's lift (see CopyAt).</param>
    /// <param name="anchor">The height the copy's anchored objects stand at.</param>
    /// <param name="add">The objects to add, with whether each is a building part; added to.</param>
    internal void AddObjects(Ground g, CopyData c, Vector2 ca, float lift, float anchor, List<(NewObject, bool)> add)
    {
        float ox = g.OriginX, oz = g.OriginZ;
        foreach (var o in c.Objects)
        {
            var p = ca + Xf(new Vector2(o.Dx, o.Dz));
            // Trees and the like keep their height above the ground where they land (read after this copy shaped it).
            float y = o.Follow ? g.HeightOf(Index(g, p)) + o.Dy + lift : anchor + o.Dy;
            add.Add((new NewObject(0, o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), Rotated(o.Rotation), o.Scale, o.Data), ObjectKinds.Of(o.Name) == ObjectKind.Buildings));
        }
    }
}
