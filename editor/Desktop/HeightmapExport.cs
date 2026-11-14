using System.Buffers.Binary;
using System.IO.Compression;

namespace Groundwork.Desktop;

/// <summary>
/// A map's heights written out for other tools: a 16-bit grey PNG, or raw little-endian 16-bit (.r16, .raw), the map's
/// first row first. Unreal's convention: 32768 is 0 m, 128 steps a metre (a landscape at Z scale 100), so ±256 m.
/// </summary>
public static class HeightmapExport
{
    /// <summary>A height in metres as a 16-bit value.</summary>
    internal static ushort Value(float metres) => (ushort)Math.Clamp(MathF.Round((metres * 128) + 32768), 0, 65535);

    /// <summary>Writes heights (rows of cols) to a file: .r16 and .raw raw, anything else PNG.</summary>
    public static void Write(string file, float[] heights, int cols, int rows) =>
        File.WriteAllBytes(file, Path.GetExtension(file).ToLowerInvariant() is ".r16" or ".raw" ? R16(heights, cols, rows) : Png16(heights, cols, rows));

    /// <summary>Raw little-endian 16-bit values, row after row.</summary>
    internal static byte[] R16(float[] heights, int cols, int rows)
    {
        byte[] b = new byte[cols * rows * 2];
        for (int i = 0; i < cols * rows; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i * 2), Value(heights[i]));
        }
        return b;
    }

    /// <summary>A 16-bit grey PNG: each row unfiltered, big-endian values, one zlib stream.</summary>
    internal static byte[] Png16(float[] heights, int cols, int rows)
    {
        byte[] raw = new byte[rows * (1 + (cols * 2))];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan((r * (1 + (cols * 2))) + 1 + (c * 2)), Value(heights[(r * cols) + c]));
            }
        }
        return Png(raw, cols, rows, 16);
    }

    /// <summary>An 8-bit grey PNG of values 0..1 (0 black, 1 white), the first row first.</summary>
    internal static byte[] Png8(float[] values, int cols, int rows)
    {
        byte[] raw = new byte[rows * (1 + cols)];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                raw[(r * (1 + cols)) + 1 + c] = (byte)Math.Clamp((int)MathF.Round(values[(r * cols) + c] * 255), 0, 255);
            }
        }
        return Png(raw, cols, rows, 8);
    }

    /// <summary>A grey PNG of rows already laid out (each a 0 filter byte and its values), bits a value.</summary>
    private static byte[] Png(byte[] raw, int cols, int rows, byte bits)
    {
        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }
        byte[] head = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(head, cols);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(4), rows);
        head[8] = bits;   // bits a value; colour type 0 (grey), compression, filter and interlace 0
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk(png, "IHDR", head);
        Chunk(png, "IDAT", packed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        s.Write(four);
        byte[] typed = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];
        s.Write(typed);
        BinaryPrimitives.WriteUInt32BigEndian(four, Crc(typed));
        s.Write(four);
    }

    private static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c;
    })];

    /// <summary>PNG's CRC-32 of a chunk's type and data.</summary>
    private static uint Crc(byte[] b)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in b)
        {
            c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        }
        return c ^ 0xFFFFFFFF;
    }
}
