using System.Buffers.Binary;

namespace Groundwork.Desktop;

/// <summary>
/// A heightmap from another tool (a terrain tool): a 16-bit or 8-bit grey PNG, raw little-endian 16-bit (.r16,
/// .raw) or 32-bit float (.r32), of any width and height, first row first; each value from 0 at the tool's lowest
/// ground to 1 at its highest. The tools leave the metres out of the file (and a raw file its width), so they are asked
/// for (<see cref="Metres"/>).
/// </summary>
public static partial class HeightmapImport
{
    /// <summary>The biggest side read (8193 pixels: an 8 km map at a metre a pixel, as the tools export it).</summary>
    public const int MaxSide = 8193;

    /// <summary>The most 63 m squares a map made from a heightmap has a side (about 8 km: Unreal's biggest landscape).</summary>
    public const int MaxSquaresASide = 128;

    /// <summary>
    /// The most 63 m squares it has in all (a 5 km square's worth: 25 million points, about 3.5 GB in the editor)
    /// (ponytail: a memory cap measured on one machine; streaming the ground would lift it).
    /// </summary>
    public const int MaxSquares = 6400;

    /// <summary>A heightmap read: its values (0 to 1, row by row from its first row) and its size in samples.</summary>
    public sealed record Heightmap(float[] Values, int Cols, int Rows)
    {
        /// <summary>The height its 0 stands for, metres, when the file says (a GeoTIFF's); null: give it.</summary>
        public float? Low { get; init; }

        /// <summary>The height its 1 stands for, metres, when the file says.</summary>
        public float? High { get; init; }

        /// <summary>Metres between its samples, when the file says.</summary>
        public float? PerSample { get; init; }

        /// <summary>How many of its samples were holes (no data), filled from the ground round them.</summary>
        public int Holes { get; init; }
    }

    /// <summary>Whether a file's name is one of a heightmap this reads (16-bit PNG, .r16, .raw, .r32, GeoTIFF).</summary>
    public static bool IsHeightmap(string file) => Path.GetExtension(file).ToLowerInvariant() is ".png" or ".r16" or ".raw" or ".r32" or ".tif" or ".tiff";

    /// <summary>
    /// A heightmap read from its file. A raw file holds no width: give it (<paramref name="cols"/>), or it is taken as
    /// square.
    /// </summary>
    /// <exception cref="InvalidDataException">Not a heightmap this reads, not that wide, or too big.</exception>
    public static Heightmap Read(string file, int? cols = null)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is ".tif" or ".tiff")
        {
            return ReadTiff(file);
        }
        long length = new FileInfo(file).Length;
        int size = ext switch { ".r16" or ".raw" => 2, ".r32" => 4, _ => 0 };
        if (size > 0)
        {
            var (w, h) = SizeOf(length / size, length % size, cols, file);
            byte[] b = File.ReadAllBytes(file);
            float[] v = new float[w * h];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = size == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i * 2)) / 65535f : BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(i * 4));
            }
            return new(v, w, h);
        }
        var png = Files.Png.Read(File.ReadAllBytes(file));
        if (png.Width > MaxSide || png.Height > MaxSide || png.Width < 2 || png.Height < 2)
        {
            throw new InvalidDataException($"{Path.GetFileName(file)}: {png.Width} x {png.Height}; a heightmap is 2 to {MaxSide} pixels a side");
        }
        return new(png.Values, png.Width, png.Height);
    }

    /// <summary>The width and height of so many values (rest: bytes left over) so many wide (null: square), or why it is none.</summary>
    private static (int Cols, int Rows) SizeOf(long count, long rest, int? cols, string file)
    {
        int w = cols ?? (int)Math.Round(Math.Sqrt(count));
        long h = w > 0 ? count / w : 0;
        if (rest != 0 || w < 2 || h < 2 || w * h != count || w > MaxSide || h > MaxSide)
        {
            throw new InvalidDataException(cols == null
                ? $"{Path.GetFileName(file)}: {count} values are not a square heightmap; a raw file holds no width, so give it (its samples across)"
                : $"{Path.GetFileName(file)}: {count} values are not rows of {w} (2 to {MaxSide} a side)");
        }
        return (w, (int)h);
    }

    /// <summary>
    /// The map's size in 63 m squares (Unreal's landscape components) for a heightmap at so many metres a sample: each
    /// side the squares it needs to hold it all.
    /// </summary>
    /// <exception cref="InvalidDataException">More than a map holds.</exception>
    public static (int X, int Z) SquaresFor(Heightmap hm, float metresPerSample)
    {
        int Side(int samples) => Math.Max(1, (int)MathF.Ceiling((((samples - 1) * metresPerSample) - 1e-3f) / 63));
        var (x, z) = (Side(hm.Cols), Side(hm.Rows));
        if (x > MaxSquaresASide || z > MaxSquaresASide || x * z > MaxSquares)
        {
            throw new InvalidDataException($"{(hm.Cols - 1) * metresPerSample:0} x {(hm.Rows - 1) * metresPerSample:0} m is more than a map holds: at most {MaxSquaresASide * 63} m a side and {MaxSquares} squares of 63 m in all ({x * z} here)");
        }
        return (x, z);
    }

    /// <summary>
    /// The heightmap on a map <paramref name="cols"/> by <paramref name="rows"/> metres, a point a metre, at
    /// <paramref name="metresPerSample"/> (between its samples in a straight line; past its last ones, as its edge), each
    /// value from <paramref name="lowest"/> to <paramref name="highest"/> metres.
    /// </summary>
    public static float[] Metres(Heightmap hm, float metresPerSample, int cols, int rows, float lowest, float highest)
    {
        float[] h = new float[cols * rows];
        float[] v = hm.Values;
        int w = hm.Cols;
        float step = 1 / MathF.Max(1e-3f, metresPerSample);
        Parallel.For(0, rows, r =>
        {
            float fy = MathF.Min(r * step, hm.Rows - 1);
            int y0 = Math.Min((int)fy, hm.Rows - 2), y1 = y0 + 1;
            float ty = fy - y0;
            for (int c = 0; c < cols; c++)
            {
                float fx = MathF.Min(c * step, w - 1);
                int x0 = Math.Min((int)fx, w - 2), x1 = x0 + 1;
                float tx = fx - x0;
                float top = v[(y0 * w) + x0] + ((v[(y0 * w) + x1] - v[(y0 * w) + x0]) * tx);
                float bottom = v[(y1 * w) + x0] + ((v[(y1 * w) + x1] - v[(y1 * w) + x0]) * tx);
                h[(r * cols) + c] = lowest + ((top + ((bottom - top) * ty)) * (highest - lowest));
            }
        });
        return h;
    }

    /// <summary>
    /// The heightmap with its rows the other way up: for a file whose first row is its south edge (some tools write raw
    /// files so); the map's first row is its north edge, as a picture's is.
    /// </summary>
    public static Heightmap Flipped(Heightmap hm)
    {
        float[] f = new float[hm.Values.Length];
        for (int r = 0; r < hm.Rows; r++)
        {
            Array.Copy(hm.Values, r * hm.Cols, f, (hm.Rows - 1 - r) * hm.Cols, hm.Cols);
        }
        return hm with { Values = f };
    }
}
