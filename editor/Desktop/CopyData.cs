using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// What Copy keeps, like the web editor's clipboard (editor/area.js): the ground's shape relative to its
/// average height (NaN: not copied), each point's weight (the soft edge), its paint (-1: not painted),
/// the objects relative to the middle, and the outline. W × H grid points around the middle. A copy of objects alone
/// (Select's) takes no ground.
/// </summary>
public sealed partial class CopyData
{
    /// <summary>The copy's width in grid points (one per metre).</summary>
    public required int W { get; init; }
    /// <summary>The copy's depth in grid points.</summary>
    public required int H { get; init; }
    /// <summary>Each point's height relative to the reference height, row by row; NaN: not copied.</summary>
    public required float[] Rel { get; init; }
    /// <summary>Each point's weight, 0 to 1 (the soft edge).</summary>
    public required float[] Wt { get; init; }
    /// <summary>Each point's paint, four values a point; -1: not painted.</summary>
    public required float[] Pnt { get; init; }
    /// <summary>The objects copied.</summary>
    public required List<Obj> Objects { get; init; }
    /// <summary>The outline, relative to the middle (metres east, north).</summary>
    public required List<Vector2> Poly { get; init; }
    /// <summary>The name of the file it came from or went to, if any.</summary>
    public string? Name { get; set; }
    /// <summary>
    /// The scene indices of the objects the copy was made from (the same order as <see cref="Objects"/>), for a cut
    /// to remove right after. Only meant in the scene it came from; not saved with the copy.
    /// </summary>
    public IReadOnlyList<int> Taken { get; init; } = [];
    /// <summary>
    /// The copy's middle in the map it came from, in metres (the objects' offsets are from it; its height is their
    /// reference height). Not saved with the copy.
    /// </summary>
    public Vector3 Middle { get; init; }
    /// <summary>
    /// Nothing a paste would put down: no objects, no ground.
    /// </summary>
    public bool Empty => Objects.Count == 0 && Array.TrueForAll(Rel, float.IsNaN);

    /// <summary>An object of a copy.</summary>
    /// <param name="Prefab">Its asset's number (AssetIds).</param>
    /// <param name="Name">Its asset's name.</param>
    /// <param name="Dx">Offset east from the middle, in metres.</param>
    /// <param name="Dz">Offset north from the middle, in metres.</param>
    /// <param name="Dy">Offset from the reference height (or, Follow, from the ground under it).</param>
    /// <param name="Rotation">Its turn, Euler degrees.</param>
    /// <param name="Scale">Its scale.</param>
    /// <param name="Follow">Dy is from the ground under it: trees and the like, not building parts.</param>
    /// <param name="Data">What extensions keep on it, by key (null: nothing).</param>
    public sealed record Obj(int Prefab, string Name, float Dx, float Dz, float Dy, Vector3 Rotation, float Scale, bool Follow = false,
        IReadOnlyDictionary<string, string>? Data = null);
}
