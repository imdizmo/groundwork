using Groundwork.App;

namespace Groundwork.Desktop;

// Groups: a thing's group (by prefab, built or not, tamed or not), the models started for an area, and marking a
// group to be built again.
public sealed partial class GlView
{
    /// <summary>A new area's objects: every thing in its group, and every group's model read on worker threads.</summary>
    private void StartModels(WorldScene s)
    {
        lock (_groups)
        {
            _groups.Clear();
            lock (_dirtyGroups)
            {
                _dirtyGroups.Clear();
            }
            int n;
            lock (s.Things)
            {
                n = s.Things.Count;
                for (int i = 0; i < n; i++)
                {
                    GroupOf(s, i, start: false);
                }
            }
            _pending = _groups.Count;
            foreach (var g in _groups.Values.ToList())
            {
                Load(g);
            }
        }
        SendKindCounts(s);
    }

    /// <summary>
    /// A thing's group: by asset, building part or not (the third part of the key is always false since tamed creatures went).
    /// </summary>
    private static (int Prefab, bool Piece, bool Tamed) KeyOf(WorldScene.Thing t) => (t.Prefab, t.Piece, false);

    /// <summary>The group a thing belongs to (made, and its model read, if it is the first of its kind).</summary>
#pragma warning disable S3241 // OnThingsChanged marks the group it returns dirty (MarkDirty(GroupOf(s, i))).
    private Group GroupOf(WorldScene s, int i, bool start = true)
    {
        var t = s.Things[i];
        var key = KeyOf(t);
        EnsureSize(i + 1);
        if (!_groups.TryGetValue(key, out var g))
        {
            g = _groups[key] = new Group { Key = key, Kind = ObjectKinds.Of(NameOf(t.Prefab), t.Piece) };
            if (start)
            {
                Interlocked.Increment(ref _pending);
                Load(g);
            }
        }
        if (g.Members.Add(i))
        {
            g.Things.Add(i);
        }
        lock (_objLock)
        {
            _kinds[i] = g.Kind;
        }
        return g;
    }
#pragma warning restore S3241

    /// <summary>A kind's group without things yet (the Place tool's ghosts), its model read if new.</summary>
    private Group KindGroup(int prefab, bool piece)
    {
        lock (_groups)
        {
            if (!_groups.TryGetValue((prefab, piece, false), out var g))
            {
                g = _groups[(prefab, piece, false)] = new Group { Key = (prefab, piece, false), Kind = ObjectKinds.Of(NameOf(prefab), piece) };
                Interlocked.Increment(ref _pending);
                Load(g);
            }
            return g;
        }
    }

    /// <summary>Has a group's instances built again at the next frame.</summary>
    private void MarkDirty(Group g)
    {
        lock (_dirtyGroups)
        {
            _dirtyGroups.Add(g);
        }
    }

    /// <summary>The group is still the view's (not one of an area left since).</summary>
    private bool Current(Group g)
    {
        lock (_groups)
        {
            return _groups.TryGetValue(g.Key, out var now) && now == g;
        }
    }
}
