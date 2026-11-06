using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Groundwork.Files;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A heightmap from another tool (a terrain tool) read from its 16-bit PNG, .r16 and .r32 at any width and height,
/// put in metres on the map's grid at its own scale; the generator's numbers measured from it, and the zone laid over it
/// without moving the ground; a map made over it, two squares by one, keeps it and is made again over it.
/// </summary>
[Collection("DataDir")]
public sealed class HeightmapImportTests : IDisposable
{
    private const int Side = 65;
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-hm-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    /// <summary>The map put back, the test's files deleted.</summary>
    public void Dispose()
    {
        MapProject.Current = _was;
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>A bowl with a ridge across it, 0 to 1.</summary>
    private static float Value(int c, int r) => Math.Clamp((0.3f * ((((c - 32) * (c - 32)) + ((r - 32) * (r - 32))) / 2048f)) + (0.6f * MathF.Exp(-((c - 20) * (c - 20)) / 40f)), 0, 1);

    /// <summary>The bowl and ridge as a heightmap so many samples wide and high.</summary>
    private static HeightmapImport.Heightmap Map(int cols = Side, int rows = Side) =>
        new([.. Enumerable.Range(0, cols * rows).Select(i => Value(i % cols, i / cols))], cols, rows);

    [Fact]
    public void ItsPngR16AndR32AreReadAlikeAtAnyWidthAndHeight()
    {
        const int W = 65, H = 33;
        ushort[] u = new ushort[W * H];
        byte[] r16 = new byte[u.Length * 2], r32 = new byte[u.Length * 4];
        for (int i = 0; i < u.Length; i++)
        {
            float v = Value(i % W, i / W);
            u[i] = (ushort)MathF.Round(v * 65535);
            BinaryPrimitives.WriteUInt16LittleEndian(r16.AsSpan(i * 2), u[i]);
            BinaryPrimitives.WriteSingleLittleEndian(r32.AsSpan(i * 4), v);
        }
        File.WriteAllBytes(Path.Combine(_dir, "h.png"), Png.WriteGray16(W, H, u));
        File.WriteAllBytes(Path.Combine(_dir, "h.r16"), r16);
        File.WriteAllBytes(Path.Combine(_dir, "h.r32"), r32);
        File.WriteAllBytes(Path.Combine(_dir, "square.r16"), new byte[Side * Side * 2]);

        foreach (string f in new[] { "h.png", "h.r16", "h.r32" })
        {
            var hm = HeightmapImport.Read(Path.Combine(_dir, f), cols: f.EndsWith(".png", StringComparison.Ordinal) ? null : W);
            Assert.Equal((W, H), (hm.Cols, hm.Rows));
            Assert.Equal(Value(20, 30), hm.Values[(30 * W) + 20], 3);
        }
        Assert.Equal((Side, Side), (HeightmapImport.Read(Path.Combine(_dir, "square.r16")).Cols, HeightmapImport.Read(Path.Combine(_dir, "square.r16")).Rows));
        Assert.Throws<InvalidDataException>(() => HeightmapImport.Read(Path.Combine(_dir, "h.r16")));   // a raw file not square: its width is needed
        Assert.Throws<InvalidDataException>(() => HeightmapImport.Read(Path.Combine(_dir, "h.r16"), cols: 64));
    }

    [Fact]
    public void ItsValuesBecomeMetresOnTheMapsGridAtItsOwnScale()
    {
        var hm = Map();

        float[] h = HeightmapImport.Metres(hm, 2, 129, 129, -10, 90);   // two metres a sample: stretched twice over

        Assert.Equal(-10 + (100 * hm.Values[0]), h[0], 3);
        Assert.Equal(-10 + (100 * hm.Values[^1]), h[^1], 3);
        Assert.Equal(-10 + (100 * hm.Values[(40 * Side) + 20]), h[(80 * 129) + 40], 3);
        float[] past = HeightmapImport.Metres(hm, 1, 100, 70, 0, 1);   // the map bigger than the heightmap: its edge carried on
        Assert.Equal(hm.Values[(10 * Side) + Side - 1], past[(10 * 100) + 99], 3);
        Assert.Equal((17, 17), HeightmapImport.SquaresFor(new(new float[1025 * 1025], 1025, 1025), 1));   // each side rounded up
        Assert.Equal((81, 61), HeightmapImport.SquaresFor(new(new float[1337 * 1001], 1337, 1001), 3.81f));
        Assert.Throws<InvalidDataException>(() => HeightmapImport.SquaresFor(new(new float[2049 * 2049], 2049, 2049), 4));
        var flipped = HeightmapImport.Flipped(hm);   // a file written from its south edge
        Assert.Equal((hm.Values[(Side - 1) * Side], hm.Values[3]), (flipped.Values[0], flipped.Values[((Side - 1) * Side) + 3]));
    }

    [Fact]
    public void TheZoneLiesOnTheGroundWithoutMovingIt()
    {
        const int N = 127;
        float[] ground = HeightmapImport.Metres(Map(), (Side - 1f) / (N - 1), N, N, 2, 60);

        var spec = ZoneGenerator.Measured(ZoneGenerator.Styles[0].Spec with { RimHeight = 200, Coast = 300, Terraces = 10, LakeHectares = [2] }, ground, N, N);
        var zone = ZoneGenerator.Generate(spec, N, N, -63, -63, new([], [], [], []), ground);

        Assert.True(spec.Low < 10 && spec.High > 40, $"measured {spec.Low} to {spec.High} m");
        Assert.Equal((0f, 0f, 0f), (spec.RimHeight, spec.Coast, spec.Terraces));
        Assert.Empty(zone.Lakes);
        Assert.All(Enumerable.Range(0, N * N), p => Assert.Equal(ground[p], zone.Heights[p], 2));
        Assert.Contains(Enumerable.Range(0, N * N), p => zone.Mask[(p * 4) + 2] > 128);   // its steep sides painted rock
        float[] lowered = [.. ground.Select(v => (v - 30) * 0.25f)];   // gentle, and half under the sea
        var low = ZoneGenerator.Generate(ZoneGenerator.Measured(ZoneGenerator.Styles[0].Spec, lowered, N, N), N, N, -63, -63, new([], [], [], []), lowered);
        Assert.Contains(low.Heights, v => v < -2);
        Assert.All(Enumerable.Range(0, N * N).Where(p => low.Heights[p] < 0), p => Assert.Equal(0, low.Wooded[p]));   // no woods under the sea
        Assert.Contains(Enumerable.Range(0, N * N), p => low.Heights[p] is > 0 and < 2.5f && low.Mask[p * 4] > 128);   // a beach along it
        var higher = ZoneGenerator.Generate(spec with { High = spec.High + 40 }, N, N, -63, -63, new([], [], [], []), ground);
        Assert.True(higher.Heights.Max() > zone.Heights.Max() + 30, "High stretches it");
    }

    [AvaloniaFact]
    public void AMapMadeOverItKeepsItAndIsMadeAgainOverIt()
    {
        const int Cols = 127, Rows = 64;   // two 63 m squares across, one north to south
        var hm = Map(Side, 33);
        float[] ground = HeightmapImport.Metres(hm, 2, Cols, Rows, 5, 25);
        var spec = ZoneGenerator.Measured(ZoneGenerator.Styles[0].Spec, ground, Cols, Rows);

        var made = MapProject.CreateZone("Over", 2, spec, new([], [], [], []), _dir, null, ground, chunksZ: 1);
        var again = MapProject.Open(made.Folder);
        again.Generate(spec with { Woods = 0 }, new([], [], [], []));

        Assert.Equal("base.npy", JsonNode.Parse(File.ReadAllText(Path.Combine(made.Folder, "map.json")))!["generator"]!["base"]!.GetValue<string>());
        Assert.Equal(ground, again.ReadBase());
        var opened = MapProject.Open(made.Folder);
        var (heights, cols, rows) = opened.HeightsNow(opened.Read());
        Assert.Equal((Cols, Rows), (cols, rows));
        Assert.All(Enumerable.Range(0, Cols * Rows), p => Assert.Equal(ground[p], heights[p], 2));
    }
}
