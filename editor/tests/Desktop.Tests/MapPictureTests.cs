using System.Numerics;
using Groundwork.Sdk;
using SkiaSharp;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The map's picture from above (north up, the sea, names) and the block look's sides.</summary>
[Collection("MapAreas")]
public class MapPictureTests
{
    /// <summary>200 x 100 m: the sea under 0 on the west half (the ground falls westward), green land on the east.</summary>
    private sealed class Area : IAreaView
    {
        public float MinX => 0;
        public float MinZ => 0;
        public float MaxX => 200;
        public float MaxZ => 100;
        public IReadOnlyList<IObjectView> ObjectsOf(string asset) => [];
        public float GroundHeight(float x, float z) => (x - 100) / 10;
        public int Layer(float x, float z) => 0;
        public Vector3 GroundColor(int x, int z) => new(0.05f, 0.3f, 0.05f);
        public uint GroundMask(int x, int z) => 0;
    }

    [Fact]
    public void ThePictureIsNorthUpWithTheSeaWhereTheGroundIsUnderIt()
    {
        MapNames.Labels.Clear();
        MapNames.Regions.Clear();
        var (png, w, h) = MapPicture.Of(new Area(), 0, MapPicture.Style.Colour, 400);
        Assert.Equal((400, 200), (w, h));
        using var bmp = SKBitmap.Decode(png);
        var sea = bmp.GetPixel(50, 100);
        var land = bmp.GetPixel(350, 100);
        Assert.True(sea.Blue > sea.Green && sea.Blue > sea.Red, $"sea {sea}");
        Assert.True(land.Green > land.Blue && land.Green > land.Red, $"land {land}");

        // a name over the land changes the pixels there
        MapNames.Labels.Add(new MapNames.Label { Text = "Hollow Ford", X = 150, Z = 50 });
        var (named, _, _) = MapPicture.Of(new Area(), 0, MapPicture.Style.Colour, 400);
        using var withName = SKBitmap.Decode(named);
        int changed = Enumerable.Range(300, 80).Count(x => withName.GetPixel(x, 100) != bmp.GetPixel(x, 100));
        Assert.True(changed > 10, $"{changed} pixels changed");
        MapNames.Labels.Clear();

        var (old, _, _) = MapPicture.Of(new Area(), 0, MapPicture.Style.Parchment, 400);
        using var paper = SKBitmap.Decode(old);
        Assert.True(paper.GetPixel(350, 100).Red > land.Red, "parchment is browner");

        // engraved: flat land is bare paper (this ground's slope is even, so no hatching but its contours), the sea lined in ink
        var (inked, _, _) = MapPicture.Of(new Area(), 0, MapPicture.Style.Engraving, 400);
        using var ink = SKBitmap.Decode(inked);
        var column = Enumerable.Range(20, 160).Select(y => ink.GetPixel(50, y)).ToList();
        Assert.Contains(column, c => c.Red < 150);    // a level line across the sea
        Assert.Contains(column, c => c.Red > 230);    // paper between them
        Assert.True(ink.GetPixel(352, 100).Red > 200, $"land {ink.GetPixel(352, 100)}");
    }

    [Fact]
    public void TheBlocksSidesGoDownFromTheGroundToAFlatBottomWithTheSeaInFront()
    {
        var layer = MapBlock.Sides(new Area(), 0);
        Assert.Equal(4, layer.Meshes.Count);
        var south = layer.Meshes[0];
        float bottom = south.Positions.Min(p => p.Y);
        Assert.True(bottom < -10 - 19, $"bottom {bottom}");   // under the lowest ground (-10) by 20 m or more
        Assert.All(south.Positions, p => Assert.Equal(0, p.Z, 3));
        Assert.All(south.Positions, p => Assert.True(p.Y <= MathF.Max(0, (p.X - 100) / 10) + 1e-3f));
        // the sea's band where the ground is under it
        Assert.Contains(south.Colors!, c => c.Z > c.X * 3);
    }

    [Fact]
    public void WithoutCatalogBiomesEachLayerIsABiomeWithTheKindsThatBelongOnIt()
    {
        static MapProject.Layer L(string name) => new(name, new Vector3(0.3f, 0.5f, 0.2f), 0);
        List<MapTools.Kind> kinds = [new("Oak", "Oak", "Trees", 1, 1, null), new("Fern", "Fern", "Plants", 1, 1, null), new("Boulder", "Boulder", "Rocks", 1, 1, null)];
        var biomes = MapBiomes.FromLayers([L("Pine forest"), L("Meadow grass"), L("Scree"), L("Sand")], kinds);
        Assert.Equal(["Pine forest", "Meadow grass", "Scree", "Sand"], biomes.Select(b => b.Paint));
        Assert.Equal(["Oak"], biomes[0].Kinds.Keys);
        Assert.Equal(["Fern"], biomes[1].Kinds.Keys);
        Assert.Equal(["Boulder"], biomes[2].Kinds.Keys);
        Assert.Empty(biomes[3].Kinds);
    }
}
