namespace Groundwork.App;

/// <summary>Sorting objects into <see cref="ObjectKind"/>s by the open map's catalog, with their labels for the View panel.</summary>
public static class ObjectKinds
{
    private static volatile Dictionary<string, ObjectKind> _kinds = [];
    private static readonly Lock Gate = new();

    /// <summary>Every kind, in the View panel's order.</summary>
    public static readonly ObjectKind[] All = Enum.GetValues<ObjectKind>();

    /// <summary>
    /// A map's assets' categories (its catalog's kinds), added to those known (a later map's wins for the same asset): a
    /// category named as a kind ("Trees", "rocks") is that kind, any other is Other.
    /// </summary>
    // ponytail: never emptied; a few thousand assets at most
    public static void UseCategories(IEnumerable<(string Asset, string Category)> categories)
    {
        lock (Gate)
        {
            var kinds = new Dictionary<string, ObjectKind>(_kinds, StringComparer.Ordinal);
            foreach (var (asset, category) in categories)
            {
                kinds[asset] = Enum.TryParse(category, true, out ObjectKind k) ? k : ObjectKind.Other;
            }
            _kinds = kinds;
        }
    }

    /// <summary>
    /// The kind of an object of an asset (null, or not in the catalog: Other); a scaled copy ("Grass@1_0.8_1") is its
    /// asset's kind.
    /// </summary>
    public static ObjectKind Of(string? asset) =>
        asset != null && (_kinds.TryGetValue(asset, out var k) || (asset.IndexOf('@') is > 0 and var at && _kinds.TryGetValue(asset[..at], out k))) ? k : ObjectKind.Other;

    /// <summary>The kind of an object: a building part is Buildings, any other its asset's.</summary>
    public static ObjectKind Of(string? asset, bool piece) => piece ? ObjectKind.Buildings : Of(asset);

    /// <summary>A kind's label, in the language shown.</summary>
    public static string Label(ObjectKind k) => Lang.T(LabelKey(k));

    /// <summary>A kind's label, said (shown and in English).</summary>
    public static Said Name(ObjectKind k) => Said.Of(LabelKey(k));

    private static string LabelKey(ObjectKind k) => k switch
    {
        ObjectKind.Trees => "kind.label.trees",
        ObjectKind.Rocks => "kind.label.rocks",
        ObjectKind.Bushes => "kind.label.bushes",
        ObjectKind.Plants => "kind.label.plants",
        ObjectKind.Props => "kind.label.props",
        ObjectKind.Buildings => "kind.label.buildings",
        ObjectKind.Markers => "kind.label.markers",
        _ => "kind.label.other",
    };

    /// <summary>Whether a kind is shown when the editor starts: all but the small plants (thousands of them) and markers.</summary>
    public static bool ShownAtFirst(ObjectKind k) => k is not (ObjectKind.Plants or ObjectKind.Markers);
}
