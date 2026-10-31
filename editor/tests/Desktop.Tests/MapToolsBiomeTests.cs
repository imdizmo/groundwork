using System.Text.Json.Nodes;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Save as biome (MapTools.SaveBiome): the kinds go into the catalog map.json names, as a biome of that name; a map
/// without a catalog of its own is told so and nothing is written.
/// </summary>
public sealed class MapToolsBiomeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-biome-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    public void Dispose()
    {
        MapProject.Current = _was;
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void ABiomeIsKeptInTheMapsOwnCatalogOnly()
    {
        var made = MapProject.Create("Biomes", 1, generated: false, height: 2, seed: 1, under: _dir);
        MapProject.Current = MapProject.Open(made.Folder);
        Assert.NotNull(MapTools.SaveBiome("Wood", "Grass", ["Pine_1"]));

        string catalog = Path.Combine(made.Folder, "catalog.json");
        File.WriteAllText(catalog, """{"kinds": []}""");
        var j = JsonNode.Parse(File.ReadAllText(Path.Combine(made.Folder, "map.json")))!;
        j["catalog"] = "catalog.json";
        File.WriteAllText(Path.Combine(made.Folder, "map.json"), j.ToJsonString());
        MapProject.Current = MapProject.Open(made.Folder);

        Assert.Null(MapTools.SaveBiome("Wood", "Grass", ["Pine_1", "Pine_1", "Birch_2"]));
        var wood = JsonNode.Parse(File.ReadAllText(catalog))!["biomes"]!["Wood"]!;
        Assert.Equal("Grass", wood["paint"]!.GetValue<string>());
        Assert.Equal(["Pine_1", "Birch_2"], wood["kinds"]!.AsObject().Select(kv => kv.Key));
    }
}
