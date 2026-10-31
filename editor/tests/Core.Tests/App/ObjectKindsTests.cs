using Groundwork.App;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// Objects' kinds come from the maps' catalogs: a category named as a kind is that kind (in any case), any other
/// category or an asset no catalog lists is Other, a later map's category wins; a building part is Buildings whatever its asset; every kind
/// has a label and shows at first.
/// </summary>
public sealed class ObjectKindsTests
{
    [Fact]
    public void AnAssetsKindIsItsCatalogCategory()
    {
        ObjectKinds.UseCategories([("birch", "Trees"), ("rock_big", "rocks"), ("crate", "Props"), ("sign", "Signage")]);
        ObjectKinds.UseCategories([("crate", "Buildings")]);   // a later map's category wins

        Assert.Equal(ObjectKind.Trees, ObjectKinds.Of("birch"));
        Assert.Equal(ObjectKind.Rocks, ObjectKinds.Of("rock_big"));
        Assert.Equal(ObjectKind.Buildings, ObjectKinds.Of("crate"));
        Assert.Equal(ObjectKind.Trees, ObjectKinds.Of("birch"));
        Assert.Equal(ObjectKind.Other, ObjectKinds.Of("sign"));
        Assert.Equal(ObjectKind.Other, ObjectKinds.Of("not_listed"));
        Assert.Equal(ObjectKind.Trees, ObjectKinds.Of("birch@1.20_0.90_1.20"));   // a scaled copy is its asset's
        Assert.Equal(ObjectKind.Other, ObjectKinds.Of("@birch"));
        Assert.Equal(ObjectKind.Other, ObjectKinds.Of(null));
        Assert.Equal(ObjectKind.Buildings, ObjectKinds.Of("birch", piece: true));
        Assert.Equal(ObjectKind.Trees, ObjectKinds.Of("birch", piece: false));
    }

    [Fact]
    public void EveryKindHasALabelAndAllButPlantsAndMarkersShowAtFirst()
    {
        Assert.All(ObjectKinds.All, k =>
        {
            Assert.False(string.IsNullOrEmpty(ObjectKinds.Label(k)));
            Assert.DoesNotContain("kind.label", ObjectKinds.Label(k));
            Assert.Equal(k is not (ObjectKind.Plants or ObjectKind.Markers), ObjectKinds.ShownAtFirst(k));
        });
    }
}
