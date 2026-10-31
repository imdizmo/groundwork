using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The ground's changed points on their way to the graphics card: a rectangle's vertices are those of
/// the whole rows at the same points, so uploading only the rectangle draws the same ground.
/// </summary>
public class GlViewTerrainUploadTests
{
    [Fact]
    public void ARectanglesPointsAreThoseOfTheWholeRows()
    {
        var s = TestScenes.Flat(2).Scene;
        var rnd = new Random(3);
        for (int i = 0; i < s.Heights.Length; i++)
        {
            s.Heights[i] = 30 + (float)rnd.NextDouble() * 5;
            s.Mask[i * 4] = (byte)rnd.Next(256);
        }
        (int x0, int z0, int x1, int z1) = (0, 7, 40, 19);
        foreach (var (lx, hx) in new[] { (x0, x1), (17, 60), (0, s.W - 1) })
        {
            int n = hx - lx + 1, rows = z1 - z0 + 1;
            float[] rowsV = GlView.TerrainRows(s, z0, z1);
            float[] v = new float[n * rows * 9];
            GlView.TerrainPoints(s, lx, z0, hx, z1, v);
            for (int r = 0; r < rows; r++)
            {
                Assert.Equal(rowsV.AsSpan((r * s.W + lx) * 9, n * 9).ToArray(), v.AsSpan(r * n * 9, n * 9).ToArray());
            }
        }
    }
}
