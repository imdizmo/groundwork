using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The terrain maps read off the ground: water gathering in a valley, slope, facing, wear, and the files.</summary>
public class TerrainMapsTests
{
    private const int N = 200;

    /// <summary>A valley down the middle column, its sides at 15°, its floor falling towards the first row at 3°.</summary>
    private static float[] Valley()
    {
        float[] h = new float[N * N];
        for (int r = 0; r < N; r++)
        {
            for (int c = 0; c < N; c++)
            {
                h[(r * N) + c] = 20 + (MathF.Abs(c - (N / 2)) * MathF.Tan(15 * MathF.PI / 180)) + (r * MathF.Tan(3 * MathF.PI / 180));
            }
        }
        return h;
    }

    private static Dictionary<string, float[]> Maps(float[] h, float[]? worn = null) =>
        TerrainMaps.Of(h, N, N, 0, worn).ToDictionary(m => m.Name, m => m.Values);

    [Fact]
    public void WaterGathersOnTheValleyFloorAndTheFloorIsWet()
    {
        var m = Maps(Valley());
        int floor = (20 * N) + (N / 2), side = (20 * N) + (N / 2) + 60;   // near the valley's foot, on its floor and up its side
        Assert.True(m["flow"][floor] > m["flow"][side] + 0.1f, $"flow {m["flow"][floor]} on the floor, {m["flow"][side]} on the side");
        Assert.True(m["wetness"][floor] > m["wetness"][side] + 0.2f, $"wetness {m["wetness"][floor]} on the floor, {m["wetness"][side]} on the side");
        Assert.All(m.Values, v => Assert.All(v, x => Assert.InRange(x, 0, 1)));
    }

    [Fact]
    public void SlopeFacingAndConvexityReadTheValleySides()
    {
        var m = Maps(Valley());
        int east = (100 * N) + 150, west = (100 * N) + 50;
        // the sides' 15.2° (15° across and 3° along), as a share of 90°
        Assert.InRange(m["slope"][east] * 90, 14.5f, 16);
        // the east side runs down to the west and the west side to the east; both a little towards the first row
        Assert.True(m["facing_east"][east] < 0.05f && m["facing_east"][west] > 0.95f);
        Assert.InRange(m["facing_north"][east], 0.55f, 0.75f);
        // an even slope is neither ridge nor hollow; the valley's line is a hollow
        Assert.InRange(m["convexity"][east], 0.45f, 0.55f);
        Assert.True(m["convexity"][(100 * N) + (N / 2)] < 0.4f);
        Assert.False(m.ContainsKey("wear"));
    }

    [Fact]
    public void WearAndDepositsComeFromTheGeneratorsWear()
    {
        float[] worn = new float[N * N];
        (worn[0], worn[1]) = (-12, 6);
        var m = Maps(Valley(), worn);
        Assert.Equal(0.5f, m["wear"][0], 3);
        Assert.Equal(0f, m["wear"][1]);
        Assert.Equal(0.25f, m["deposits"][1], 3);
        Assert.Equal(0f, m["deposits"][2]);
    }

    [Fact]
    public void An8BitPngIsGreyAtItsSize()
    {
        byte[] png = HeightmapExport.Png8([0, 0.5f, 1, 1, 0, 0], 3, 2);
        Assert.Equal([137, 80, 78, 71], png[..4]);
        Assert.Equal(3, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(2, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Assert.Equal(8, png[24]);   // bits a value
        Assert.Equal(0, png[25]);   // grey
    }

    [Fact]
    public void AGeneratedMapKeepsItsWearOnlyWhenItWasWorn()
    {
        string dir = Directory.CreateTempSubdirectory("gw-worn-").FullName;
        try
        {
            var kinds = new ZoneGenerator.Planting([], [], [], []);
            var spec = new ZoneGenerator.Spec(Low: 5, High: 60, RimHeight: 0, Seed: 3, Erosion: 1);
            var m = MapProject.CreateZone("Worn", 2, spec, kinds, dir);
            var map = MapProject.Open(m.Folder);
            Assert.True(File.Exists(map.WornFile));
            var raster = map.ReadRaster();
            Assert.Contains(map.ReadWorn(raster.Cols, raster.Rows)!, v => v != 0);
            Assert.Null(map.ReadWorn(raster.Cols + 1, raster.Rows));   // grown since: not the map's any more
            // made again with no wear: none kept
            map.Generate(spec with { Erosion = 0 }, kinds);
            Assert.False(File.Exists(map.WornFile));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>A plane at height base, falling degrees towards the map's first row (negative: towards its last).</summary>
    private static float[] Plane(float height, float degrees) =>
        [.. Enumerable.Range(0, N * N).Select(p => height + ((p / N) * MathF.Tan(degrees * MathF.PI / 180)))];

    [Fact]
    public void SnowLiesAboveTheLineDeeperInTheShadeAndSlidesOffSteepGround()
    {
        int mid = (100 * N) + 100;
        // flat ground: 2 m 60 m and more over the line, none under it
        Assert.Equal(2, TerrainMaps.Snow(Plane(500, 0), N, N, 400)[mid], 2);
        Assert.Equal(0, TerrainMaps.Snow(Plane(300, 0), N, N, 400)[mid]);
        // a 20° slope facing north (away from the sun) holds more than one facing south
        float shaded = TerrainMaps.Snow(Plane(500, 20), N, N, 400)[mid], sunny = TerrainMaps.Snow(Plane(500 + (N * MathF.Tan(20 * MathF.PI / 180)), -20), N, N, 400)[mid];
        Assert.True(shaded > 2.5f && sunny < 1.5f, $"{shaded} m in the shade, {sunny} m in the sun");
        // at 60° it slides off, down to the foot
        float[] steep = TerrainMaps.Snow(Plane(2000, -60), N, N, 400);
        Assert.True(steep[mid] < 0.5f, $"{steep[mid]} m on a 60° face");
    }
}
