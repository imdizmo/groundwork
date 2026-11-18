namespace Groundwork.Desktop;

/// <summary>
/// The Mask as a black-and-white picture of the map, to see which ground a rule lets through: white where it passes,
/// black where not, grey where a pixel covers ground of both. North is up; one pixel a ground point (a metre) unless
/// the map is bigger than the picture may be, then each pixel covers a square of points.
/// </summary>
internal static class MaskPicture
{
    /// <summary>The picture as a PNG (8-bit grey) and how much ground passes.</summary>
    /// <param name="test">The mask by ground point index (Mask.For): 1 passes, 0 not.</param>
    /// <param name="w">The ground's width in points.</param>
    /// <param name="h">The ground's height in points.</param>
    /// <param name="max">The picture's longest side at most, in pixels.</param>
    internal static (byte[] Png, int Passed, int Width, int Height) Of(Func<int, float> test, int w, int h, int max = 8192)
    {
        int step = (Math.Max(w, h) + max - 1) / max, pw = (w + step - 1) / step, ph = (h + step - 1) / step;
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(pw, ph, SkiaSharp.SKColorType.Gray8, SkiaSharp.SKAlphaType.Opaque));
        byte[] pixels = new byte[pw * ph];
        int passed = 0;
        for (int py = 0; py < ph; py++)
        {
            for (int px = 0; px < pw; px++)
            {
                float sum = 0;
                int n = 0;
                // the picture's top row is the ground's north edge (its last row)
                for (int gz = h - 1 - (py * step); gz > h - 1 - ((py + 1) * step) && gz >= 0; gz--)
                {
                    for (int gx = px * step; gx < Math.Min(w, (px + 1) * step); gx++, n++)
                    {
                        float v = Math.Clamp(test((gz * w) + gx), 0, 1);
                        sum += v;
                        passed += v >= 0.5f ? 1 : 0;
                    }
                }
                pixels[(py * pw) + px] = (byte)MathF.Round(255 * sum / Math.Max(1, n));
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return (data.ToArray(), passed, pw, ph);
    }
}
