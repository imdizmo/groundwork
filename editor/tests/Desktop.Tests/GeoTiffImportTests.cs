using System.Buffers.Binary;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>GeoTIFF elevation models: their heights and pixel size read, holes filled, degrees turned into metres.</summary>
public class GeoTiffImportTests
{
    /// <summary>
    /// A little-endian float32 GeoTIFF, uncompressed in one strip, with its pixel size, a tie point (the first pixel's
    /// world position), its model type (1: projected, metres; 2: geographic, degrees) and a no-data value.
    /// </summary>
    private static string Write(float[] v, int cols, int rows, double scaleX, double scaleY, double tieY, int modelType, string? noData)
    {
        var tags = new List<(ushort Tag, ushort Type, int Count, byte[] Data)>
        {
            (256, 4, 1, U32(cols)), (257, 4, 1, U32(rows)), (258, 3, 1, U16(32)), (259, 3, 1, U16(1)), (262, 3, 1, U16(1)),
            (273, 4, 1, []), (277, 3, 1, U16(1)), (278, 4, 1, U32(rows)), (279, 4, 1, U32(v.Length * 4)), (339, 3, 1, U16(3)),
            (33550, 12, 3, [.. F64(scaleX), .. F64(scaleY), .. F64(0)]),
            (33922, 12, 6, [.. F64(0), .. F64(0), .. F64(0), .. F64(500000), .. F64(tieY), .. F64(0)]),
            (34735, 3, 8, [.. U16(1), .. U16(1), .. U16(0), .. U16(1), .. U16(1024), .. U16(0), .. U16(1), .. U16((ushort)modelType)]),
        };
        if (noData != null)
        {
            tags.Add((42113, 2, noData.Length + 1, [.. System.Text.Encoding.ASCII.GetBytes(noData), 0]));
        }
        // header, the IFD, then each long value, then the pixels
        int ifd = 8, ifdSize = 2 + (tags.Count * 12) + 4, extra = ifd + ifdSize;
        var outside = new List<byte>();
        int pixelsAt = extra + tags.Where(t => t.Data.Length > 4).Sum(t => t.Data.Length + (t.Data.Length % 2));
        List<byte> file = [(byte)'I', (byte)'I', 42, 0, .. U32(ifd), .. U16((ushort)tags.Count)];
        foreach (var (tag, type, count, data) in tags)
        {
            byte[] value = tag == 273 ? U32(pixelsAt) : data;
            file.AddRange([.. U16(tag), .. U16(type), .. U32(count)]);
            if (value.Length <= 4)
            {
                file.AddRange([.. value, .. new byte[4 - value.Length]]);
            }
            else
            {
                file.AddRange(U32(extra + outside.Count));
                outside.AddRange(value);
                if (value.Length % 2 == 1)
                {
                    outside.Add(0);
                }
            }
        }
        file.AddRange(U32(0));
        file.AddRange(outside);
        foreach (float f in v)
        {
            file.AddRange(BitConverter.GetBytes(f));
        }
        string path = Path.Combine(Directory.CreateTempSubdirectory("gw-tif-").FullName, "dem.tif");
        File.WriteAllBytes(path, [.. file]);
        return path;
    }

    private static byte[] U16(ushort v) => [(byte)v, (byte)(v >> 8)];

    private static byte[] U32(int v)
    {
        byte[] b = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, v);
        return b;
    }

    private static byte[] F64(double v) => BitConverter.GetBytes(v);

    [Fact]
    public void AProjectedDemKeepsItsMetresAndFillsItsHoles()
    {
        // 4 by 3 pixels 2 m apart, heights 100 to 111 m, one of them no-data (-500, which would be a height otherwise)
        float[] v = [.. Enumerable.Range(0, 12).Select(i => 100f + i)];
        v[5] = -500;
        string file = Write(v, 4, 3, 2, 2, 0, 1, "-500");
        try
        {
            var hm = HeightmapImport.Read(file);
            Assert.True(HeightmapImport.IsHeightmap(file));
            Assert.Equal((4, 3), (hm.Cols, hm.Rows));
            Assert.Equal(2, hm.PerSample);
            Assert.Equal(1, hm.Holes);
            Assert.Equal(100, hm.Low!.Value, 3);
            Assert.Equal(111, hm.High!.Value, 3);
            // the hole: the average of its eight neighbours (100, 101, 102, 104, 106, 108, 109, 110 → 105)
            Assert.Equal(105, hm.Low.Value + (hm.Values[5] * (hm.High.Value - hm.Low.Value)), 2);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }
    }

    [Fact]
    public void AGeographicDemIsTurnedIntoMetresAndItsPixelsMadeSquare()
    {
        // 0.0003° pixels at 60° north: about 16.7 m across and 33.2 m down, so the rows are doubled
        string file = Write([.. Enumerable.Repeat(50f, 20 * 10)], 20, 10, 0.0003, 0.0003, 60 + (0.0003 * 5), 2, null);
        try
        {
            var hm = HeightmapImport.Read(file);
            Assert.InRange(hm.PerSample!.Value, 16.5f, 16.9f);
            Assert.Equal(20, hm.Cols);
            Assert.Equal(19, hm.Rows);   // 9 gaps of 33.2 m are 18 of 16.7
            Assert.Equal(0, hm.Holes);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }
    }

    [Fact]
    public void AWideHoleFillsSmoothlyFromItsEdges()
    {
        // a slope rising 1 m a column, its middle 10 by 10 gone: filled, it still rises, between its edges' heights
        const int N = 30;
        float[] v = [.. Enumerable.Range(0, N * N).Select(i => (float)(i % N))];
        bool[] hole = [.. Enumerable.Range(0, N * N).Select(i => i % N is >= 10 and < 20 && i / N is >= 10 and < 20)];
        HeightmapImport.FillHoles(v, hole, N, N);
        Assert.DoesNotContain(true, hole);
        for (int c = 10; c < 19; c++)
        {
            Assert.True(v[(15 * N) + c + 1] >= v[(15 * N) + c] - 0.01f, $"column {c}: {v[(15 * N) + c]} then {v[(15 * N) + c + 1]}");
        }
        Assert.InRange(v[(15 * N) + 15], 9, 20);
    }

    [Fact]
    public void ABrokenOrNonsenseFileIsNotAHeightmapAndANonsensePixelSizeIsIgnored()
    {
        string dir = Directory.CreateTempSubdirectory("gw-tif-").FullName;
        try
        {
            // a TIFF's first bytes, then rubbish
            string junk = Path.Combine(dir, "junk.tif");
            File.WriteAllBytes(junk, [(byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 255, 255, 1, 2, 3]);
            _ = Assert.Throws<InvalidDataException>(() => HeightmapImport.Read(junk));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        string file = Write([.. Enumerable.Repeat(5f, 16)], 4, 4, double.NaN, double.PositiveInfinity, 0, 1, null);
        try
        {
            Assert.Equal(1, HeightmapImport.Read(file).PerSample);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, true);
        }
        _ = Assert.Throws<InvalidDataException>(() => HeightmapImport.SquaresFor(new([0, 1, 0, 1], 2, 2), float.NaN));
    }
}
