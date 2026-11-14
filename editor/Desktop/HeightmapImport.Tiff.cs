using BitMiracle.LibTiff.Classic;

namespace Groundwork.Desktop;

/// <summary>
/// GeoTIFF elevation models (DEMs: survey and satellite heightmaps, in metres): the first band of a strip or tiled TIFF
/// of any compression, its holes (no-data) filled from the ground round them, its pixel size read from its geo tags (in
/// degrees for a geographic DEM, turned into metres at its latitude) and its pixels made square.
/// </summary>
public static partial class HeightmapImport
{
    /// <summary>GeoTIFF's tags: pixel size, the tie point (a pixel's world position), the geo keys, GDAL's no-data value.</summary>
    private const int PixelScaleTag = 33550, TiePointTag = 33922, GeoKeysTag = 34735, NoDataTag = 42113;

    /// <summary>Reads a GeoTIFF: its heights scaled 0..1 between its lowest and highest, which it carries in metres.</summary>
    /// <exception cref="InvalidDataException">Not a TIFF this reads, or too big.</exception>
    private static Heightmap ReadTiff(string file)
    {
        // a file from anywhere: whatever else goes wrong in reading it, it is not a heightmap this reads
        try
        {
            return ReadTiffUnchecked(file);
        }
        catch (Exception e) when (e is not (InvalidDataException or IOException or UnauthorizedAccessException or OutOfMemoryException))
        {
            throw new InvalidDataException($"{Path.GetFileName(file)}: not a TIFF this reads ({e.GetType().Name})", e);
        }
    }

    private static Heightmap ReadTiffUnchecked(string file)
    {
        string name = Path.GetFileName(file);
        Tiff.SetErrorHandler(Quiet.Handler);
        using var tif = Tiff.Open(file, "r") ?? throw new InvalidDataException($"{name}: not a TIFF");
        int cols = Int(tif, TiffTag.IMAGEWIDTH, 0), rows = Int(tif, TiffTag.IMAGELENGTH, 0);
        int bits = Int(tif, TiffTag.BITSPERSAMPLE, 1), format = Int(tif, TiffTag.SAMPLEFORMAT, 1), samples = Int(tif, TiffTag.SAMPLESPERPIXEL, 1);
        if (cols < 2 || rows < 2 || cols > MaxSide || rows > MaxSide)
        {
            throw new InvalidDataException($"{name}: {cols} x {rows}; a heightmap is 2 to {MaxSide} pixels a side");
        }
        if (Int(tif, TiffTag.PLANARCONFIG, 1) != 1 && samples > 1)
        {
            throw new InvalidDataException($"{name}: bands stored apart; save it with one band");
        }
        if (samples is < 1 or > 16)
        {
            throw new InvalidDataException($"{name}: {samples} samples a pixel");
        }
        int size = bits / 8;
        Func<byte[], int, float> sample = (format, bits) switch
        {
            (3, 32) => BitConverter.ToSingle,
            (3, 64) => (b, i) => (float)BitConverter.ToDouble(b, i),
            (2, 16) => (b, i) => BitConverter.ToInt16(b, i),
            (2, 32) => (b, i) => BitConverter.ToInt32(b, i),
            (1, 16) => (b, i) => BitConverter.ToUInt16(b, i),
            (1, 32) => (b, i) => BitConverter.ToUInt32(b, i),
            (1 or 2, 8) => (b, i) => b[i],
            _ => throw new InvalidDataException($"{name}: {bits}-bit samples of format {format}; a DEM is 8, 16 or 32-bit whole numbers or 32 or 64-bit floats"),
        };
        float[] v = new float[cols * rows];
        int stride = size * samples;   // the first band of each pixel
        if (tif.IsTiled())
        {
            int tw = Int(tif, TiffTag.TILEWIDTH, 0), th = Int(tif, TiffTag.TILELENGTH, 0);
            long tileBytes = tif.TileSize();
            // tiles of a sane size, the buffer holding every sample read from it
            if (tw is < 1 or > MaxSide || th is < 1 or > MaxSide || tileBytes < (long)tw * th * stride || tileBytes > 256L << 20)
            {
                throw new InvalidDataException($"{name}: tiles of {tw} x {th} ({tileBytes} bytes) that do not fit its samples");
            }
            byte[] tile = new byte[tileBytes];
            for (int y = 0; y < rows; y += th)
            {
                for (int x = 0; x < cols; x += tw)
                {
                    if (tif.ReadTile(tile, 0, x, y, 0, 0) < 0)
                    {
                        throw new InvalidDataException($"{name}: a tile at {x}, {y} could not be read");
                    }
                    for (int r = 0; r < th && y + r < rows; r++)
                    {
                        for (int c = 0; c < tw && x + c < cols; c++)
                        {
                            v[((y + r) * cols) + x + c] = sample(tile, ((r * tw) + c) * stride);
                        }
                    }
                }
            }
        }
        else
        {
            int lineBytes = tif.ScanlineSize();
            if (lineBytes < (long)cols * stride)
            {
                throw new InvalidDataException($"{name}: rows of {lineBytes} bytes that do not hold {cols} samples");
            }
            byte[] line = new byte[lineBytes];
            for (int r = 0; r < rows; r++)
            {
                if (!tif.ReadScanline(line, r))
                {
                    throw new InvalidDataException($"{name}: row {r} could not be read");
                }
                for (int c = 0; c < cols; c++)
                {
                    v[(r * cols) + c] = sample(line, c * stride);
                }
            }
        }

        // holes: GDAL's no-data value, and the usual stand-ins for none (NaN, -9999, -32768, -3.4e38)
        float? noData = tif.GetField((TiffTag)NoDataTag) is { Length: > 0 } nd && float.TryParse(Text(nd).Trim('\0', ' '), System.Globalization.CultureInfo.InvariantCulture, out float ndv) ? ndv : null;
        bool[] hole = new bool[v.Length];
        int holes = 0;
        for (int i = 0; i < v.Length; i++)
        {
            if (float.IsNaN(v[i]) || v[i] == noData || v[i] < -9000 || v[i] > 1e5f)
            {
                (hole[i], holes) = (true, holes + 1);
            }
        }
        if (holes == v.Length)
        {
            throw new InvalidDataException($"{name}: holds no heights (all no-data)");
        }
        if (holes > 0)
        {
            FillHoles(v, hole, cols, rows);
        }

        // its pixel size in metres, across and down: degrees in a geographic DEM, at the latitude of its middle
        double[] scale = Doubles(tif, PixelScaleTag), tie = Doubles(tif, TiePointTag);
        float across = 1, down = 1;
        if (scale.Length >= 2 && scale[0] > 0 && scale[1] > 0)
        {
            (across, down) = ((float)scale[0], (float)scale[1]);
            if (Geographic(tif) || scale[0] < 0.01)
            {
                double lat = Math.Clamp(tie.Length >= 6 ? tie[4] - (scale[1] * rows / 2) : 0, -89, 89);
                (across, down) = ((float)(scale[0] * 111320 * Math.Cos(lat * Math.PI / 180)), (float)(scale[1] * 110574));
            }
        }
        if (!float.IsFinite(across) || !float.IsFinite(down) || across <= 0 || down <= 0)
        {
            (across, down) = (1, 1);   // a pixel size that makes no sense: none (the user gives Across)
        }
        // square pixels: the rows stretched (or squeezed) to the pixels' size across
        if (MathF.Abs((down / across) - 1) > 0.01f)
        {
            int rowsNow = Math.Clamp((int)MathF.Round((rows - 1) * down / across) + 1, 2, MaxSide);
            float[] even = new float[cols * rowsNow];
            for (int r = 0; r < rowsNow; r++)
            {
                float at = r * (rows - 1) / (float)(rowsNow - 1);
                int r0 = Math.Min(rows - 2, (int)at);
                float t = at - r0;
                for (int c = 0; c < cols; c++)
                {
                    even[(r * cols) + c] = (v[(r0 * cols) + c] * (1 - t)) + (v[((r0 + 1) * cols) + c] * t);
                }
            }
            (v, rows) = (even, rowsNow);
        }
        float lo = v.Min(), hi = v.Max(), span = MathF.Max(hi - lo, 1e-3f);
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = (v[i] - lo) / span;
        }
        return new(v, cols, rows) { Low = lo, High = lo + span, PerSample = across, Holes = holes };
    }

    /// <summary>
    /// Fills the holes from the ground round them, growing in from their edges (breadth first, each hole once): each
    /// takes the average of its neighbours known or filled before it, so the fill is a smooth surface.
    /// </summary>
    internal static void FillHoles(float[] v, bool[] hole, int cols, int rows)
    {
        var queue = new Queue<int>();
        bool[] queued = new bool[v.Length];
        void Near(int i, Action<int> each)
        {
            int c = i % cols, r = i / cols;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int nc = c + dc, nr = r + dr;
                    if ((dc != 0 || dr != 0) && nc >= 0 && nr >= 0 && nc < cols && nr < rows)
                    {
                        each((nr * cols) + nc);
                    }
                }
            }
        }
        // the holes beside known ground first
        for (int i = 0; i < v.Length; i++)
        {
            if (hole[i])
            {
                Near(i, n =>
                {
                    if (!hole[n] && !queued[i])
                    {
                        queued[i] = true;
                        queue.Enqueue(i);
                    }
                });
            }
        }
        while (queue.TryDequeue(out int i))
        {
            float sum = 0;
            int count = 0;
            Near(i, n =>
            {
                if (!hole[n])
                {
                    (sum, count) = (sum + v[n], count + 1);
                }
                else if (!queued[n])
                {
                    queued[n] = true;
                    queue.Enqueue(n);
                }
            });
            (v[i], hole[i]) = (count > 0 ? sum / count : v[i], false);
        }
    }

    /// <summary>Whether its geo keys say it is in degrees (GTModelTypeGeoKey: 2, geographic).</summary>
    private static bool Geographic(Tiff tif)
    {
        double[] keys = Doubles(tif, GeoKeysTag);
        for (int k = 4; k + 3 < keys.Length; k += 4)
        {
            if (keys[k] == 1024)
            {
                return keys[k + 3] == 2;
            }
        }
        return false;
    }

    private static int Int(Tiff tif, TiffTag tag, int otherwise) => tif.GetField(tag) is { Length: > 0 } f ? f[0].ToInt() : otherwise;

    /// <summary>A tag's numbers (doubles or shorts), or none.</summary>
    private static double[] Doubles(Tiff tif, int tag)
    {
        if (tif.GetField((TiffTag)tag) is not { Length: > 0 } f)
        {
            return [];
        }
        var last = f[^1];
        return last.Value switch
        {
            double[] d => d,
            short[] s => [.. s.Select(x => (double)(ushort)x)],
            ushort[] u => [.. u.Select(x => (double)x)],
            byte[] b when f[0].ToInt() * 8 == b.Length => [.. Enumerable.Range(0, b.Length / 8).Select(i => BitConverter.ToDouble(b, i * 8))],
            byte[] b when f[0].ToInt() * 2 == b.Length => [.. Enumerable.Range(0, b.Length / 2).Select(i => (double)BitConverter.ToUInt16(b, i * 2))],
            _ => [],
        };
    }

    /// <summary>An ASCII tag's text.</summary>
    private static string Text(FieldValue[] f) => f[^1].Value switch
    {
        string s => s,
        byte[] b => System.Text.Encoding.ASCII.GetString(b),
        _ => f[^1].ToString() ?? "",
    };

    /// <summary>LibTiff's warnings and errors kept off the console (an unknown tag is no news); errors still fail the read.</summary>
    private sealed class Quiet : TiffErrorHandler
    {
        public static readonly Quiet Handler = new();

        public override void WarningHandler(Tiff tif, string method, string format, params object[] args)
        {
        }

        public override void WarningHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
        {
        }

        public override void ErrorHandler(Tiff tif, string method, string format, params object[] args)
        {
        }

        public override void ErrorHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
        {
        }
    }
}
