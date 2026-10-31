using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>An object as extensions see it (IObjectView): its asset, its place, its data.</summary>
/// <param name="asset">Its asset's name ("" when not known).</param>
/// <param name="position">Where it is.</param>
/// <param name="rotation">Its turn, Euler degrees.</param>
/// <param name="data">Its data, by key (null: none).</param>
/// <param name="scale">Its size (0 or 1: the asset's own).</param>
public sealed class ObjectView(string asset, Vector3 position, Vector3 rotation, IReadOnlyDictionary<string, string>? data = null, float scale = 1) : IObjectView
{
    /// <summary>
    /// The object's id in the editor when it was read for a tool, which hands it back to change or remove it; null
    /// otherwise.
    /// </summary>
    public int? Id { get; init; }

    /// <inheritdoc/>
    public string Asset => asset;

    /// <inheritdoc/>
    public Vector3 Position => position;

    /// <inheritdoc/>
    public Vector3 Rotation => rotation;

    /// <inheritdoc/>
    public Quaternion Orientation => Euler.FromEuler(rotation);

    /// <inheritdoc/>
    public float Scale => scale == 0 ? 1 : scale;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Data { get; } = data ?? new Dictionary<string, string>();
}
