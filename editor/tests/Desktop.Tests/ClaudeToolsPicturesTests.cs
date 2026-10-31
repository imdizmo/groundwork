using SkiaSharp;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Claude's pictures (ClaudeTools): the area's map drawn from its data, and screenshots of the 3D view and the world's
/// map, made no wider than 1280 pixels; four views in one. Headless there are no OpenGL frames: the view's and the
/// map's pictures here are plain PNGs written in their place.
/// </summary>
[Collection("World files")]
public class ClaudeToolsPicturesTests
{

    // 40 x 30 points, 12 pixels each, with a 40 pixel margin: water blue under its level, painted ground grey,
    // and a JPEG to show.
    [Fact]
    public void TheAreasMapIsDrawnFromItsData()
    {
        const int W = 40, H = 30;
        float[] heights = new float[W * H];
        float[] paint = new float[W * H * 3];
        for (int j = 0; j < H; j++)
        {
            for (int i = 0; i < W; i++)
            {
                // The west half under water, the east half dry; the north-east corner paved.
                heights[j * W + i] = i < W / 2 ? 20 : 40 + j;
                if (i >= 30 && j >= 20)
                {
                    paint[(j * W + i) * 3 + 2] = 1;
                }
            }
        }
        List<(float X, float Z, string Kind)> things = [(5, 5, "Trees"), (6, 6, "Rocks"), (7, 7, "Ore"), (8, 8, "Buildings"), (9, 9, "Pickables")];
        byte[] jpeg = ClaudeTools.DrawMap(W, H, 0, 0, 30, heights, paint, things, 10);
        using var map = SKBitmap.Decode(jpeg);
        Assert.Equal((W * 12 + 80, H * 12 + 80), (map.Width, map.Height));
        // North up: row j is drawn at 40 + (H - 1 - j) * 12. Water (west) is blue; paved ground (north-east) grey.
        var water = map.GetPixel(40 + 5 * 12 + 6, 40 + (H - 1 - 15) * 12 + 6);
        Assert.True(water.Blue > water.Red + 60, $"water {water}");
        var paved = map.GetPixel(40 + 35 * 12 + 6, 40 + (H - 1 - 25) * 12 + 6);
        Assert.True(Math.Abs(paved.Red - paved.Blue) < 40 && paved.Red > 100, $"paved {paved}");
        // Without paint and objects too.
        using var bare = SKBitmap.Decode(ClaudeTools.DrawMap(W, H, 100, -50, 30, heights, null, [], 4));
        Assert.Equal(map.Width, bare.Width);
    }

    // Wider than 1280 pixels: made 1280 wide, the same shape; smaller: kept as it is.
    [Fact]
    public void PicturesAreShrunkTo1280Wide()
    {
        static byte[] Make(int w, int h)
        {
            using var bitmap = new SKBitmap(w, h);
            bitmap.Erase(SKColors.Green);
            using var image = SKImage.FromBitmap(bitmap);
            return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }
        using var big = SKBitmap.Decode(ClaudeTools.Shrink(Make(2560, 640)));
        Assert.Equal((1280, 320), (big.Width, big.Height));
        using var small = SKBitmap.Decode(ClaudeTools.Shrink(Make(400, 300)));
        Assert.Equal((400, 300), (small.Width, small.Height));
    }
}
