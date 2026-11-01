using Avalonia.Headless.XUnit;
using Groundwork.Tests;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The sample map shipped in samples/Meadow opens: its ground, layers, rivers and lakes, its objects, and a glTF model for
/// every kind its catalog lists.
/// </summary>
public sealed class SampleMapTests
{
    private static readonly string Meadow = Path.GetFullPath(Path.Combine(Fixtures.Editor, "..", "samples", "Meadow"));

    [AvaloniaFact]
    public void TheMeadowOpensWithAModelForEveryKind()
    {
        var map = MapProject.Open(Meadow);
        var scene = map.Read();
        Assert.True(scene.Things.Count > 1000);
        Assert.Equal(4, map.Layers.Count);
        Assert.Single(MapWater.Rivers);
        Assert.Single(MapWater.Lakes);

        var store = ModelStore.Of(Path.Combine(Meadow, "models"))!;
        var kinds = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Meadow, "models", "catalog.json")))!["kinds"]!.AsArray();
        Assert.All(kinds, k => Assert.NotNull(store.LoadModel(k!["model"]!.GetValue<string>())));
    }

    [AvaloniaFact]
    public void ItsObjectsTakeItsOwnCatalogsKindsWhenOpenedFromNoMap()
    {
        MapProject.Current = null;
        var map = MapProject.Open(Meadow);
        _ = map.Read();
        Assert.Equal(Groundwork.App.ObjectKind.Plants, Groundwork.App.ObjectKinds.Of("Grass_Common_Short"));
        Assert.Equal("Plants", MapTools.GroupOf(Groundwork.App.AssetIds.Of("Grass_Common_Short")).Group);
    }

    // A map made like the Meadow (whose models are in its own models folder) shares those models.
    [AvaloniaFact]
    public void AMapMadeLikeTheMeadowSharesItsModels()
    {
        string under = Path.Combine(Path.GetTempPath(), "gw-like-" + Guid.NewGuid().ToString("N"));
        try
        {
            var made = MapProject.Create("Like", 1, generated: false, height: 2, seed: 1, under: under, like: MapProject.Open(Meadow));
            Assert.Equal(Path.Combine(Meadow, "models"), MapProject.Open(made.Folder).Models);
        }
        finally
        {
            Directory.Delete(under, true);
        }
    }
}
