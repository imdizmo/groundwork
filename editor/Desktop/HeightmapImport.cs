using System.Buffers.Binary;

namespace Groundwork.Desktop;

/// <summary>
/// A heightmap from another tool (a terrain tool): a 16-bit or 8-bit grey PNG, raw little-endian 16-bit (.r16,
/// .raw) or 32-bit float (.r32), square, first row first; each value from 0 at the tool's lowest ground to 1 at its
/// highest. The tools leave the metres out of the file, so they are asked for (<see cref="Metres"/>).
/// </summary>
public static class HeightmapImport
{
    /// <summary>The biggest side read (8193 pixels: an 8 km map at a metre a pixel, as the tools export it).</summary>
    public const int MaxSide = 8193;

    /// <summary>A heightmap's values (0 to 1, row by row) and its side in pixels.</summary>
    /// <exception cref="InvalidDataException">Not a heightmap this reads, not square, or too big.</exception>
    public static (float[] Values, int Side) Read(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        long length = new FileInfo(file).Length;
        int size = ext switch { ".r16" or ".raw" => 2, ".r32" => 4, _ => 0 };
        if (size > 0)
        {
            int side = SideOf(length / size, length % size, file);
            byte[] b = File.ReadAllBytes(file);
            float[] v = new float[side * side];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = size == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i * 2)) / 65535f : BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(i * 4));
            }
            return (v, side);
        }
        var png = Files.Png.Read(File.ReadAllBytes(file));
        if (png.Width != png.Height || png.Width > MaxSide)
        {
            throw new InvalidDataException($"{Path.GetFileName(file)}: {png.Width} x {png.Height}; a heightmap must be square, at most {MaxSide} pixels a side");
        }
        return (png.Values, png.Width);
    }

    /// <summary>The side of a square of so many values (rest: bytes left over), or why it is none.</summary>
    private static int SideOf(long count, long rest, string file)
    {
        int side = (int)Math.Round(Math.Sqrt(count));
        if (rest != 0 || (long)side * side != count || side < 2 || side > MaxSide)
        {
            throw new InvalidDataException($"{Path.GetFileName(file)}: {count} values are not a square heightmap of at most {MaxSide} pixels a side (raw files carry no size: the tools write them square)");
        }
        return side;
    }

    /// <summary>
    /// The heightmap on a map <paramref name="n"/> metres a side, a point a metre (stretched or shrunk to fit, between its
    /// pixels in a straight line), each value from <paramref name="lowest"/> to <paramref name="highest"/> metres.
    /// </summary>
    public static float[] Metres(float[] values, int side, int n, float lowest, float highest)
    {
        float[] h = new float[n * n];
        float scale = (side - 1) / (float)Math.Max(1, n - 1);
        Parallel.For(0, n, r =>
        {
            float fy = r * scale;
            int y0 = Math.Min((int)fy, side - 2), y1 = y0 + 1;
            float ty = fy - y0;
            for (int c = 0; c < n; c++)
            {
                float fx = c * scale;
                int x0 = Math.Min((int)fx, side - 2), x1 = x0 + 1;
                float tx = fx - x0;
                float top = values[(y0 * side) + x0] + ((values[(y0 * side) + x1] - values[(y0 * side) + x0]) * tx);
                float bottom = values[(y1 * side) + x0] + ((values[(y1 * side) + x1] - values[(y1 * side) + x0]) * tx);
                h[(r * n) + c] = lowest + ((top + ((bottom - top) * ty)) * (highest - lowest));
            }
        });
        return h;
    }

    /// <summary>
    /// The heightmap with its rows the other way up: for a file whose first row is its south edge (some tools write raw
    /// files so); the map's first row is its north edge, as a picture's is.
    /// </summary>
    public static float[] Flipped(float[] values, int side)
    {
        float[] f = new float[values.Length];
        for (int r = 0; r < side; r++)
        {
            Array.Copy(values, r * side, f, (side - 1 - r) * side, side);
        }
        return f;
    }

    /// <summary>The map's size in 63 m chunks for a heightmap so many metres across (Unreal's landscape components, 1 to 64).</summary>
    public static int ChunksFor(float metres) => Math.Clamp((int)MathF.Round(metres / 63), 1, 64);
}
