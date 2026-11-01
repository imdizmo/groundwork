using System.IO.Compression;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Terraces, erosion over the whole map, and heightmaps written out for other tools.</summary>
public class TerrainExtrasTests
{
    [Theory]
    [InlineData(10.2f, 10f)]    // on a shelf: down to it
    [InlineData(10.0f, 10f)]    // a shelf stays put
    [InlineData(11.95f, 12f)]   // near the top of a rise: up to the next shelf
    [InlineData(-3.1f, -4f)]    // below zero too
    public void ATerraceFlattensShelvesAStepApart(float h, float shelf)
    {
        Assert.Equal(shelf, Sculpt.TerraceTarget(h, 2), 0.1f);
    }

    [Fact]
    public void ATerraceIsSteepBetweenItsShelves()
    {
        float a = Sculpt.TerraceTarget(11.4f, 2), b = Sculpt.TerraceTarget(11.8f, 2);
        Assert.True(b - a > 0.4f, $"{a} -> {b}");
    }

    [Fact]
    public void ErodingTheWholeMapWearsASpikeDown()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        int p = (32 * g.W) + 32;
        g.Base[p] = 50;
        var (touched, _) = Sculpt.ErodeWhole(g, false, 33, 1, null, new Random(1));
        Assert.Contains(p, touched);
        Assert.True(g.HeightOf(p) < 45, $"{g.HeightOf(p)}");
    }

    [Fact]
    public void ErodingTheWholeMapLeavesWhatTheMaskCloses()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        int p = (32 * g.W) + 32;
        g.Base[p] = 50;
        var (touched, _) = Sculpt.ErodeWhole(g, false, 33, 1, _ => 0, new Random(1));
        Assert.Empty(touched);
        Assert.Equal(50, g.HeightOf(p));
    }

    [Fact]
    public void AHeightIsUnrealsSixteenBits()
    {
        Assert.Equal(32768, HeightmapExport.Value(0));
        Assert.Equal(32768 + 1280, HeightmapExport.Value(10));
        Assert.Equal(0, HeightmapExport.Value(-1000));
        Assert.Equal(65535, HeightmapExport.Value(1000));
    }

    [Fact]
    public void R16IsLittleEndianRowAfterRow()
    {
        byte[] b = HeightmapExport.R16([0, 10, -1, 2], 2, 2);
        Assert.Equal(8, b.Length);
        Assert.Equal(32768 + 1280, BitConverter.ToUInt16(b, 2));
        Assert.Equal(32768 + 256, BitConverter.ToUInt16(b, 6));
    }

    [Fact]
    public void APng16HasItsSizeAndRows()
    {
        byte[] png = HeightmapExport.Png16([0, 10, -1, 2, 3, 4], 3, 2);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(3, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);   // IHDR width
        Assert.Equal(2, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);   // height
        Assert.Equal(16, png[24]);   // bits
        Assert.Equal(0, png[25]);    // grey
        int idat = 33;   // after the signature and IHDR's 25 bytes
        int len = (png[idat] << 24) | (png[idat + 1] << 16) | (png[idat + 2] << 8) | png[idat + 3];
        using var z = new ZLibStream(new MemoryStream(png, idat + 8, len), CompressionMode.Decompress);
        using var rows = new MemoryStream();
        z.CopyTo(rows);
        byte[] r = rows.ToArray();
        Assert.Equal(2 * (1 + (3 * 2)), r.Length);
        Assert.Equal(0, r[0]);   // no filter
        Assert.Equal(32768 + 1280, (r[3] << 8) | r[4]);   // big-endian: the second height, 10 m
        Assert.Equal(32768 + 256, (r[8] << 8) | r[9]);    // the second row's first: 2 m
    }
}
