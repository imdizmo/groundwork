namespace Groundwork.Desktop;

/// <summary>Ready-made kinds of zone for New map's zone form: each a spec whose numbers the form then shows and lets change.</summary>
public static partial class ZoneGenerator
{
    /// <summary>The styles, the first the plain one: rolling woods with a rim and lakes.</summary>
    public static IReadOnlyList<(string Name, string About, Spec Spec)> Styles { get; } =
    [
        ("Rolling woods", "Rolling hills, a mountain rim, lakes in the low ground, woods.", new Spec()),
        ("Grassland with plateaus", "Open grass between flat-topped plateaus, a chasm, big trees in groves, rivers into the lakes.",
            new Spec(Low: -10, High: 60, HillSize: 400, RimHeight: 320, RimWidth: 350, Woods: 0.13f, WoodDensity: 38, OpenTrees: 1.2f, Small: 70, Shore: 4,
                Terraces: 55, TerraceShare: 0.4f, Canyons: 35, CanyonSpacing: 1400, CanyonWidth: 70, Rivers: 2, OpenDirt: 0.3f, RockSlope: 36, Rough: 1.5f, Camps: 6, Roads: true, Giants: 0.3f, GiantScale: 3)),
        ("Sky islands", "Grassland with plateaus, and great rocks hanging in the air over it.",
            new Spec(Low: -10, High: 60, HillSize: 400, RimHeight: 320, RimWidth: 350, Woods: 0.13f, WoodDensity: 38, OpenTrees: 1.2f, Small: 70, Shore: 4,
                Terraces: 55, TerraceShare: 0.4f, Canyons: 35, CanyonSpacing: 1400, CanyonWidth: 70, Rivers: 2, Floaters: 40, OpenDirt: 0.3f, RockSlope: 36, Rough: 1.5f, Camps: 4, Roads: true, Giants: 0.3f, GiantScale: 3)),
        ("Broken badland at the world's edge", "Rugged dry ground, shelves and canyons, dead trees, the land breaking off into nothing on one side.",
            new Spec(Low: 0, High: 120, HillSize: 140, RimHeight: 280, RimWidth: 300, LakeHectares: [], Woods: 0.03f, WoodDensity: 15, OpenTrees: 0.6f, Small: 18, Shore: 0,
                Terraces: 26, TerraceShare: 0.4f, Canyons: 45, CanyonSpacing: 600, CanyonWidth: 45, Coast: 3700, CoastSide: "east", CoastDry: true, Floaters: 25,
                OpenDirt: 0.35f, RockSlope: 36, Rough: 2, Camps: 5, Roads: true)),
        ("Mesas and canyons", "High dry ground stepped into mesas and cut by canyons, scarcely a tree.",
            new Spec(Low: 225, High: 355, HillSize: 300, RimHeight: 150, RimWidth: 200, LakeHectares: [], Woods: 0.02f, WoodDensity: 10, OpenTrees: 0.3f, Small: 18, Shore: 0,
                Terraces: 36, TerraceShare: 0.85f, Canyons: 50, CanyonSpacing: 450, CanyonWidth: 50, OpenDirt: 0.07f, RockSlope: 38, Camps: 2, Roads: true)),
        ("Savanna", "Wide dry plains with low shelves and ridges, scattered trees, small waterholes, a coast on one side.",
            new Spec(Low: 30, High: 140, HillSize: 450, RimHeight: 220, RimWidth: 260, Woods: 0.08f, WoodDensity: 18, OpenTrees: 0.6f, Small: 33, Shore: 5,
                Terraces: 45, TerraceShare: 0.45f, Coast: 350, CoastSide: "east", CoastDepth: 20, OpenDirt: 0.45f, RockSlope: 24, Rough: 4, Camps: 10, Roads: true, Giants: 0.25f, GiantScale: 3)),
        ("Tundra coast", "Cold rolling ground down to a wide bay, snow on the heights, few trees.",
            new Spec(Low: 0, High: 80, HillSize: 500, RimHeight: 280, RimWidth: 280, Woods: 0.03f, WoodDensity: 22, OpenTrees: 0.6f, Small: 80, Shore: 12,
                Coast: 1300, CoastSide: "west", CoastDepth: 40, SnowLine: 75, SnowChannel: 1, OpenDirt: 0, RockSlope: 20, Rough: 2.5f, Camps: 6, Roads: true, RoadChannel: 0)),
        ("Marsh coast", "Low wet flats and pools along the sea, rivers down from high mountains.",
            new Spec(Low: 0, High: 55, HillSize: 250, RimHeight: 520, RimWidth: 650, Woods: 0.05f, WoodDensity: 24, OpenTrees: 0.4f, Small: 22, Shore: 0,
                Coast: 1300, CoastSide: "west", CoastDepth: 3, Rivers: 2, RiverWidth: 10, SnowLine: 330, OpenDirt: 0.12f, Marsh: 14, MarshShare: 0.6f, Rough: 4, Camps: 4, CampSize: 4, Roads: true)),
        ("Highlands", "Stepped grassy highland with crags, lakes and a river, falling to the sea on one side.",
            new Spec(Low: 25, High: 130, HillSize: 250, RimHeight: 230, RimWidth: 250, Woods: 0.05f, WoodDensity: 18, OpenTrees: 0.5f, Small: 60, Shore: 0,
                Terraces: 40, TerraceShare: 0.45f, Coast: 150, CoastSide: "east", CoastDepth: 30, Rivers: 1, OpenDirt: 0.5f, RockSlope: 36, Rough: 5, Camps: 8, CampSize: 4, Roads: true, Giants: 0.25f, GiantScale: 3)),
    ];
}
