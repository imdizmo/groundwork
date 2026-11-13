using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>
/// What running extensions draw of an open area as a whole (ISceneProvider): meshes, and holes in the ground.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>The editor's own scene providers (its tunnels), drawn like an extension's.</summary>
    private static readonly List<ISceneProvider> BuiltInScenes = [];

    /// <summary>Adds one of the editor's own scene providers (once, at start).</summary>
    /// <param name="provider">The provider; it may give null for nothing to draw.</param>
    public static void AddBuiltInScene(ISceneProvider provider)
    {
        lock (BuiltInScenes)
        {
            BuiltInScenes.Add(provider);
        }
    }

    /// <summary>Whether a running extension (or the editor itself) draws areas at all.</summary>
    public static bool AnyScenes => BuiltInScenes.Count > 0 || _state.Hooks.Any(l => l.Scenes.Count > 0);

    /// <summary>
    /// What the running extensions draw in an area, each layer checked: meshes whose triangles name corners they lack,
    /// or whose corners, normals or mask coordinates are not as many as the corners or not numbers, are left out, and
    /// so are empty holes. A provider that throws turns its extension off (the others go on).
    /// </summary>
    /// <param name="area">The area.</param>
    public static List<SceneLayer> ScenesOf(IAreaView area)
    {
        var layers = new List<SceneLayer>();
        ISceneProvider[] own;
        lock (BuiltInScenes)
        {
            own = [.. BuiltInScenes];
        }
        foreach (var p in own)
        {
            // The editor's own: a fault is logged, not turned off.
            try
            {
                if (p.Build(area) is { } layer)
                {
                    layers.Add(Checked(layer));
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                Log.Caught(e, "drawing the editor's own scene layer");
            }
        }
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Scenes)
            {
                if (Guard(l, () => p.Build(area), null) is { } layer)
                {
                    layers.Add(Checked(layer));
                }
            }
        }
        return layers;
    }

    /// <summary>A layer with only what can be drawn.</summary>
    private static SceneLayer Checked(SceneLayer layer)
    {
        var ok = new SceneLayer();
        foreach (var m in (layer.Meshes ?? []).Where(m => m != null && ValidMesh(m)))
        {
            ok.Meshes.Add(m);
        }
        foreach (var h in (layer.Holes ?? []).Where(h => h != null && h.X1 > h.X0 && h.Z1 > h.Z0))
        {
            ok.Holes.Add(h);
        }
        return ok;
    }

    /// <summary>
    /// Whether a mesh can be drawn: whole triangles of corners it has, as many finite normals as finite corners, and
    /// as many finite colours as corners when given.
    /// </summary>
    private static bool ValidMesh(SceneMesh m) =>
        m.Positions != null && m.Normals != null && m.Triangles != null && m.Normals.Length == m.Positions.Length
        && m.Triangles.Length % 3 == 0 && m.Triangles.All(i => i >= 0 && i < m.Positions.Length)
        && m.Positions.All(Finite) && m.Normals.All(Finite)
        && (m.Colors == null || (m.Colors.Length == m.Positions.Length && m.Colors.All(Finite)));

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
