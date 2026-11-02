using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>A generated map keeps what it was made from, and may be made again from new numbers until it is worked on by hand.</summary>
public class MapGeneratorTests
{
    private static readonly ZoneGenerator.Planting Kinds = new([new("Tree", "Tree", "Trees", 1, 1, null)], [], [], []);

    [Fact]
    public void AGeneratedMapKeepsItsNumbersAndIsMadeAgainOnlyUntilItIsWorkedOn()
    {
        string dir = Directory.CreateTempSubdirectory("gw-gen-").FullName;
        try
        {
            var spec = new ZoneGenerator.Spec(Low: 5, High: 40, RimHeight: 0, LakeHectares: [1.5f], Woods: 0.3f, Seed: 7, CoastSide: "north");
            var m = MapProject.CreateZone("Gen", 4, spec, Kinds, dir);
            // its numbers, read back from map.json, and it counts as untouched
            var again = MapProject.Open(m.Folder);
            Assert.Equal(spec with { LakeHectares = null }, again.Generated!.Value.Spec with { LakeHectares = null });
            Assert.Equal([1.5f], again.Generated!.Value.Spec.LakeHectares!);
            Assert.True(again.Untouched);
            // made again from other numbers: other ground, still untouched, the new numbers kept
            byte[] before = File.ReadAllBytes(again.HeightsFile);
            again.Generate(spec with { Seed = 8, High = 60 }, Kinds);
            var third = MapProject.Open(m.Folder);
            Assert.Equal(8, third.Generated!.Value.Spec.Seed);
            Assert.True(third.Untouched);
            Assert.NotEqual(before, File.ReadAllBytes(third.HeightsFile));
            // worked on by hand (a file saved): no longer
            File.WriteAllText(third.ObjectsFile, "[]");
            Assert.False(MapProject.Open(m.Folder).Untouched);
            // a map not made by the generator has no numbers
            Assert.Null(MapProject.Create("Plain", 2, false, 1, 1, dir).Generated);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
