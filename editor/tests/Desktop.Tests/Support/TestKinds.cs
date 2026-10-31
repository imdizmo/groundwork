using Groundwork.App;

namespace Groundwork.Desktop.Tests;

/// <summary>The kinds of the assets the tests place, as a map's catalog would give them.</summary>
internal static class TestKinds
{
    /// <summary>Before any test runs.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void UseThem() => ObjectKinds.UseCategories(
    [
        ("Beech1", "Trees"), ("FirTree_log", "Trees"), ("Rock_4", "Rocks"), ("rock4_coast", "Rocks"), ("Bush01", "Bushes"),
        ("Pickable_Mushroom", "Plants"), ("wood_wall_half", "Buildings"), ("woodwall", "Buildings"), ("stone_wall_2x1", "Buildings"),
        ("stone_wall_4x2", "Buildings"), ("wood_pole2", "Buildings"), ("wood_floor", "Buildings"), ("piece_chest_wood", "Buildings"),
        ("piece_workbench", "Buildings"), ("guard_stone", "Props"),
    ]);
}
