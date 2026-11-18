using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Mask as a picture (MaskPicture, map_mask_picture): north up, white where the ground passes.</summary>
public class MaskPictureTests
{
    /// <summary>A picture's grey pixels, row by row from the top.</summary>
    private static byte[] Pixels(byte[] png)
    {
        using var b = SkiaSharp.SKBitmap.Decode(png).Copy(SkiaSharp.SKColorType.Gray8);
        return b.Bytes;
    }

    [Fact]
    public void TheNorthRowIsTheTopAndGreyIsTheShareThatPasses()
    {
        // 4 x 4 points: only the north row (gz 3) passes, and on the south row only its west half
        var (png, passed, w, h) = MaskPicture.Of(p => p / 4 == 3 || p is 0 or 1 ? 1 : 0, 4, 4);
        Assert.Equal((4, 4, 6), (w, h, passed));
        byte[] px = Pixels(png);
        Assert.All(px[..4], v => Assert.Equal(255, v));
        Assert.Equal(new byte[] { 255, 255, 0, 0 }, px[12..]);
        // halved: each pixel two by two points
        var (half, _, hw, hh) = MaskPicture.Of(p => p / 4 == 3 || p is 0 or 1 ? 1 : 0, 4, 4, max: 2);
        Assert.Equal((2, 2), (hw, hh));
        Assert.Equal(new byte[] { 128, 128, 128, 0 }, Pixels(half));
    }

    [AvaloniaFact]
    public async Task ClaudeSeesHowMuchOfTheGroundAMaskLetsThrough()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gw-maskpic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = MapProject.Create("Masked", 2, generated: false, height: 10, seed: 1, under: dir).Read().Session!;
            var t = new ClaudeTools(TestWindows.Editing(s));
            var all = await t.MapMaskPicture(new() { HeightMin = 0 }, size: 64);
            Assert.StartsWith("100% of the ground passes", ((ModelContextProtocol.Protocol.TextContentBlock)all.Content[0]).Text);
            Assert.IsType<ModelContextProtocol.Protocol.ImageContentBlock>(all.Content[1]);
            var none = await t.MapMaskPicture(new() { HeightMin = 50 }, size: 64);
            Assert.StartsWith("0% of the ground passes", ((ModelContextProtocol.Protocol.TextContentBlock)none.Content[0]).Text);
            var bad = await t.MapMaskPicture(new() { SlopeMin = 30, SlopeMax = 10 });
            Assert.True(bad.IsError);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
