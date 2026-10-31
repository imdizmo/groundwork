namespace Groundwork.App;

/// <summary>
/// The map's assets by number, for the editor's own lists: each name gets the next number the first time it is asked
/// for and keeps it while the editor runs (0: none). Files and extensions always name assets; the numbers never leave
/// the editor.
/// </summary>
public static class AssetIds
{
    // ponytail: never emptied; a map names a few thousand assets at most
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Ids = new();
    private static readonly List<string> Names = [];
    private static readonly Lock Gate = new();

    /// <summary>An asset's number (a new one the first time).</summary>
    public static int Of(string name)
    {
        if (Ids.TryGetValue(name, out int id))
        {
            return id;
        }
        lock (Gate)
        {
            if (!Ids.TryGetValue(name, out id))
            {
                Names.Add(name);
                Ids[name] = id = Names.Count;
            }
            return id;
        }
    }

    /// <summary>The asset a number stands for, or null.</summary>
    public static string? NameOf(int id)
    {
        lock (Gate)
        {
            return id >= 1 && id <= Names.Count ? Names[id - 1] : null;
        }
    }
}
