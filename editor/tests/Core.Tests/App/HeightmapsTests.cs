using Groundwork.App;
using Groundwork.Files;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// 16-bit heightmaps: written and read back, with every PNG row filter, and the ground of a real area. The world
/// generator keeps static state: one world at a time.
/// </summary>
[Collection("World files")]
public class HeightmapsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Gray16RoundTripsWithEveryFilter(int filter)
    {
        int w = 37, h = 23;
        ushort[] px = [.. Enumerable.Range(0, w * h).Select(i => (ushort)((i * 7919) % 65536))];
        byte[] png = Png.WriteGray16(w, h, px, new Dictionary<string, string> { ["k"] = "v" }, filter);
        Png.Image img = Png.Read(png);
        Assert.Equal((w, h), (img.Width, img.Height));
        Assert.Equal("v", img.Text["k"]);
        for (int i = 0; i < px.Length; i++)
        {
            Assert.Equal(px[i], (ushort)Math.Round(img.Values[i] * 65535f));
        }
    }

    // Heights written in a picture by hand: not a finite number, or the highest not above the lowest, are left out (an
    // infinite one, or one too large for the Area panel's boxes, made the Area panel throw).
    [Theory]
    [InlineData("NaN", "10", null, 10f)]
    [InlineData("-5", "Infinity", -5f, null)]
    [InlineData("1e39", "10", null, 10f)]
    [InlineData("1e30", "2e30", null, null)]
    [InlineData("10", "-5", null, null)]
    [InlineData("10", "10", null, null)]
    [InlineData("-5", "10", -5f, 10f)]
    public void OnlyUsableHeightsAreReadFromAPicture(string min, string max, float? lowest, float? highest)
    {
        byte[] png = Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { [Heightmaps.MinKey] = min, [Heightmaps.MaxKey] = max });

        var pic = Heightmaps.Read(png, 1, 1);

        Assert.Equal((lowest, highest), (pic.Min, pic.Max));
    }

    // A world named outside Latin-1 (what a PNG note holds): its heightmap is still written, the name's other
    // characters as '?' (it threw).
    [Fact]
    public void AnAreaNamedOutsideLatin1IsWrittenWithQuestionMarks()
    {
        byte[] png = Heightmaps.Encode(1, 1, [0f], "世界 zones 0,0 to 1,1", out _, out _);

        Assert.Equal("?? zones 0,0 to 1,1", Heightmaps.Read(png, 1, 1).Area);
    }

    [Fact]
    public void SomethingElseIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Png.Read([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }
}
