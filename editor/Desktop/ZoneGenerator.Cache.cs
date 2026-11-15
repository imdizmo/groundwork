using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Groundwork.Desktop;

/// <summary>
/// The ground as it was after each shaping step, kept for the New map window's preview: a change to a later step, or to
/// what is laid on the ground (woods, lakes, rivers, paint, camps), makes the preview again from there instead of from the
/// hills. Held by the window (dropped when it closes), at most <see cref="MostBytes"/> of it, the oldest dropped first.
/// Making the map itself never uses it.
/// </summary>
public sealed class ShapeCache
{
    /// <summary>The most the snapshots hold together.</summary>
    public const long MostBytes = 192L << 20;

    private readonly Dictionary<string, LinkedListNode<(string Key, float[] H, float[]? Worn, float[]? Dunes)>> _byKey = [];
    private readonly LinkedList<(string Key, float[] H, float[]? Worn, float[]? Dunes)> _order = new();
    private long _bytes;

    /// <summary>How many snapshots it holds (for the tests).</summary>
    public int Count => _byKey.Count;

    /// <summary>A snapshot, if kept (it becomes the newest).</summary>
    internal (float[] H, float[]? Worn, float[]? Dunes)? Get(string key)
    {
        if (!_byKey.TryGetValue(key, out var node))
        {
            return null;
        }
        _order.Remove(node);
        _order.AddLast(node);
        return (node.Value.H, node.Value.Worn, node.Value.Dunes);
    }

    /// <summary>Keeps copies of the ground (and what was worn, and the dunes) under a key, dropping the oldest past the cap.</summary>
    internal void Put(string key, float[] h, float[]? worn, float[]? dunes)
    {
        if (_byKey.ContainsKey(key))
        {
            return;
        }
        long bytes = 4L * (h.Length + (worn?.Length ?? 0) + (dunes?.Length ?? 0));
        if (bytes > MostBytes)
        {
            return;
        }
        while (_bytes + bytes > MostBytes && _order.First is { } oldest)
        {
            Drop(oldest);
        }
        var node = _order.AddLast((key, (float[])h.Clone(), (float[]?)worn?.Clone(), (float[]?)dunes?.Clone()));
        _byKey[key] = node;
        _bytes += bytes;
    }

    private void Drop(LinkedListNode<(string Key, float[] H, float[]? Worn, float[]? Dunes)> node)
    {
        _order.Remove(node);
        _byKey.Remove(node.Value.Key);
        _bytes -= 4L * (node.Value.H.Length + (node.Value.Worn?.Length ?? 0) + (node.Value.Dunes?.Length ?? 0));
    }

    /// <summary>
    /// The key of the ground after the first <paramref name="done"/> steps: the numbers that shape it so far (what is laid
    /// on it afterwards, and the sliders of steps not yet run, left out), the steps run, and the ground's size, place and
    /// given heightmap.
    /// </summary>
    internal static string KeyOf(ZoneGenerator.Spec spec, ZoneGenerator.ShapeStep[] steps, int done, int cols, int rows, float x0, float y0, float[]? given)
    {
        var ran = steps[..done];
        bool Ran(string kind) => ran.Any(s => s.Kind == kind && s.On);
        var shaping = spec with
        {
            // laid on the ground after it is shaped: not part of its shape
            LakeHectares = null,
            Woods = 0,
            WoodDensity = 0,
            OpenTrees = 0,
            Small = 0,
            TreeScale = 0,
            Shore = 0,
            Rivers = 0,
            RiverWidth = 0,
            Meander = 0,
            Floaters = 0,
            OpenDirt = 0,
            SnowChannel = 0,
            Camps = 0,
            CampSize = 0,
            Roads = false,
            RoadChannel = 0,
            Giants = 0,
            GiantScale = 0,
            CliffSlope = 0,
            Crests = 0,
            Streams = 0,
            SnowSlip = 0,
            // the sliders of the steps not run yet
            Expand = Ran("expand") ? spec.Expand : 0,
            Valleys = Ran("valleys") ? spec.Valleys : 0,
            Glaciers = Ran("glaciers") ? spec.Glaciers : 0,
            Strata = Ran("strata") ? spec.Strata : 0,
            Erosion = Ran("erosion") ? spec.Erosion : 0,
            Talus = Ran("scree") ? spec.Talus : 0,
            SeaWear = Ran("sea") ? spec.SeaWear : 0,
            Dunes = Ran("dunes") ? spec.Dunes : 0,
            DuneWind = Ran("dunes") ? spec.DuneWind : 0,
            Crags = Ran("crags") ? spec.Crags : 0,
            Steps = ran,
        };
        string given_ = given == null ? "-" : $"{RuntimeHelpers.GetHashCode(given)}:{given.Length}";
        return $"{JsonSerializer.Serialize(shaping, SpecJson.Default.Spec)}|{cols}x{rows}@{x0},{y0}|{given_}";
    }
}
