using Groundwork.App;
using Groundwork.Sdk;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// What an extension registers with the editor's registry: shape providers and ground cutters by asset, each showing up
/// in use for the assets it named and for no others. Runs alone (the
/// extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionRegistrationTests : IDisposable
{
    /// <summary>Forgets the extensions in use, so the next test starts without any.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension registering one shape provider for two assets and one cutter for a third.</summary>
    private sealed class Registering : IGroundworkPlugin
    {
        /// <summary>Registers the shapes and cutters.</summary>
        public void Register(IPluginRegistry registry)
        {
            registry.AddShapes(new NoShapes(), "Chest", "Table");
            registry.AddGroundCuts(new NoCuts(), "Pit");
        }
    }

    /// <summary>A shape provider that gives no shapes.</summary>
    private sealed class NoShapes : IShapeProvider
    {
        /// <summary>Gives no shapes.</summary>
        public IEnumerable<Shape> ShapesOf(IObjectView obj, IAreaView area) => [];
    }

    /// <summary>A ground cutter that cuts nothing.</summary>
    private sealed class NoCuts : IGroundCutter
    {
        /// <summary>Gives no cuts.</summary>
        public IEnumerable<GroundCut> CutsOf(IObjectView obj, IAreaView area) => [];
    }

    /// <summary>A shape provider registered for a asset makes that asset have shapes.</summary>
    [Fact]
    public void AShapeProviderMakesItsAssetsHaveShapes()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.True(ModExtensions.HasShapes(AssetIds.Of("Chest")));
        Assert.True(ModExtensions.HasShapes(AssetIds.Of("Table")));
        Assert.True(ModExtensions.AnyShapes);
    }

    /// <summary>A asset no provider was registered for has no shapes from it.</summary>
    [Fact]
    public void AAssetNotRegisteredForShapesHasNone()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.False(ModExtensions.HasShapes(AssetIds.Of("Pit")));
    }

    /// <summary>A ground cutter registered for a asset makes that asset cut the ground.</summary>
    [Fact]
    public void AGroundCutterMakesItsAssetCutTheGround()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.True(ModExtensions.HasCuts(AssetIds.Of("Pit")));
        Assert.False(ModExtensions.HasCuts(AssetIds.Of("Chest")));
        Assert.True(ModExtensions.AnyCuts);
    }
}
