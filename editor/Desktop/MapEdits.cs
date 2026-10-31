using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>A new object placed in the editor (made by a tool, a paste, or read from the map's objects file).</summary>
/// <param name="Id">Its id (negative: the editor's own, given when it is placed).</param>
/// <param name="Prefab">Its asset's number (AssetIds).</param>
/// <param name="Position">Where it is, metres.</param>
/// <param name="Rotation">Its turn, Euler degrees.</param>
/// <param name="Scale">Its size (0: the asset's own).</param>
/// <param name="Data">What extensions keep on it, by key (null: nothing).</param>
public sealed record NewObject(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, IReadOnlyDictionary<string, string>? Data = null);

/// <summary>
/// What a session changed since the map was opened: counters that move with every change (for what is worked out from
/// the ground or the objects), and every object it placed, by id, with its data. Which of them are gone is the scene's
/// things' to say (Thing.Gone). Its members take its lock.
/// </summary>
public sealed class MapEdits
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, NewObject> _added = [];

    /// <summary>Moves on every change (ground or objects).</summary>
    public int Version { get; private set; }

    /// <summary>Moves when objects are added, taken away or brought back (not the ground).</summary>
    public int ObjectsVersion { get; private set; }

    /// <summary>A change was made: <see cref="Version"/> moves.</summary>
    public void Changed()
    {
        lock (_lock)
        {
            Version++;
        }
    }

    /// <summary>Objects were added, taken away or brought back: both counters move.</summary>
    public void ObjectsChanged()
    {
        lock (_lock)
        {
            Version++;
            ObjectsVersion++;
        }
    }

    /// <summary>Keeps objects placed (their ids are given already).</summary>
    public void AddObjects(IEnumerable<NewObject> objects)
    {
        lock (_lock)
        {
            foreach (var o in objects)
            {
                _added[o.Id] = o;
            }
        }
        ObjectsChanged();
    }

    /// <summary>An object placed, by id (gone or not), or null.</summary>
    public NewObject? FindAdded(int id)
    {
        lock (_lock)
        {
            return _added.GetValueOrDefault(id);
        }
    }

    /// <summary>The data an object holds (none: empty).</summary>
    public IReadOnlyDictionary<string, string> DataOf(int id) => FindAdded(id)?.Data ?? new Dictionary<string, string>();
}
