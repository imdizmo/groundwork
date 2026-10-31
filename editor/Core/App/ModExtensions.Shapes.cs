using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>
/// The shapes running extensions draw for the objects of their assets.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension draws shapes at all.</summary>
    public static bool AnyShapes => _state.Hooks.Any(l => l.Shapes.Count > 0);

    /// <summary>Whether a running extension draws shapes for an asset.</summary>
    public static bool HasShapes(int asset) => _state.Hooks.Any(l => l.Shapes.ContainsKey(asset));

    /// <summary>The shapes running extensions draw for an object of an asset (none when none does).</summary>
    /// <param name="asset">The asset's number (AssetIds).</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="area">The area around it.</param>
    public static List<Shape> ShapesOf(int asset, IObjectView obj, IAreaView area)
    {
        var shapes = new List<Shape>();
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Shapes.GetValueOrDefault(asset) ?? [])
            {
                shapes.AddRange(Guard(l, () => p.ShapesOf(obj, area)?.Where(Valid).ToList() ?? [], []));
            }
        }
        return shapes;
    }
    /// <summary>Whether a shape can be drawn: whole triangles of corners it has, finite corners.</summary>
    private static bool Valid(Shape s) =>
        s.Vertices != null && s.Triangles != null && s.Triangles.Length % 3 == 0
        && s.Triangles.All(i => i >= 0 && i < s.Vertices.Length)
        && s.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));
}
