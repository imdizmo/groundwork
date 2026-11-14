using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Rivers and lakes found in the ground: a lake in a pit, a river down a valley to the sea.</summary>
public class WaterFinderTests
{
    private const int N = 600;

    /// <summary>
    /// Ground rising from the sea (the first 40 rows under it) at 3°, a valley down the middle at 10° each side, and a pit
    /// 60 m across and 15 m deep up the slope, off the valley.
    /// </summary>
    private static float[] Ground()
    {
        float[] h = new float[N * N];
        for (int r = 0; r < N; r++)
        {
            for (int c = 0; c < N; c++)
            {
                float ground = ((r - 40) * MathF.Tan(3 * MathF.PI / 180)) + (MathF.Abs(c - 300) * MathF.Tan(10 * MathF.PI / 180));
                float pit = Vector2.Distance(new Vector2(c, r), new Vector2(120, 450));
                h[(r * N) + c] = ground - (pit < 30 ? 15 * (1 - (pit / 30 * pit / 30)) : 0);
            }
        }
        return h;
    }

    [Fact]
    public void ALakeFillsThePitToItsLip()
    {
        var (_, lakes) = WaterFinder.Find(Ground(), N, N, 1000, 2000, 0, 0.1e6f, 1000);
        var lake = Assert.Single(lakes);
        Assert.True(MapWater.InsideBy(lake.Outline, new Vector2(1000 + 120, 2000 + 450)) > 10, "the pit's middle is well inside the lake");
        Assert.True(MapWater.InsideBy(lake.Outline, new Vector2(1000 + 120, 2000 + 520)) < 0, "ground 70 m off is not");
        Assert.InRange(lake.Depth, 3, 15);
    }

    [Fact]
    public void ARiverRunsDownTheValleyToTheSeaFallingAllTheWay()
    {
        var (rivers, _) = WaterFinder.Find(Ground(), N, N, 0, 0, 0, 0.1e6f, 1000);
        Assert.NotEmpty(rivers);
        var points = rivers.SelectMany(r => r.Points).ToList();
        // down the valley's line, to the sea's edge
        Assert.Contains(points, p => MathF.Abs(p.X - 300) < 8 && p.Y < 60);
        Assert.Contains(points, p => MathF.Abs(p.X - 300) < 8 && p.Y > 300);
        Assert.All(rivers, r =>
        {
            Assert.True(r.Top >= r.End);
            Assert.True(r.Points.Count >= 2);
        });
        // wider downstream: it drains more there
        var main = points.Where(p => MathF.Abs(p.X - 300) < 8).OrderBy(p => p.Y).ToList();
        Assert.True(main[0].HalfWidth > main[^1].HalfWidth);
    }

    [Fact]
    public void AnOutlineGoesRoundTheCellsEdge()
    {
        // a 3 by 3 block of cells on a 10-wide grid: its eight edge cells, the middle not on it
        int[] block = [.. from z in Enumerable.Range(2, 3) from x in Enumerable.Range(4, 3) select (z * 10) + x];
        var ring = WaterFinder.Outline([.. block], 10);
        Assert.Equal(8, ring.Count);
        Assert.DoesNotContain(35, ring);
        Assert.Equal(ring.Count, ring.Distinct().Count());
    }
}
