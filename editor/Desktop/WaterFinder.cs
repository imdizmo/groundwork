using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Rivers and lakes found in a map's ground as it is: a lake in every hollow deep and wide enough to hold one (to its lip),
/// a river wherever enough ground drains to the same line, down to a lake, the sea, the map's edge or a larger river. The
/// water is routed by priority flood (ZoneGenerator.Drainage) on the ground sampled every 4 m.
/// </summary>
public static class WaterFinder
{
    private const int S = 4;

    /// <summary>The most rivers found (the longest first): a whole drainage net of small ones is clutter.</summary>
    private const int MostRivers = 40;

    /// <summary>
    /// Finds the rivers and lakes of some ground. Rivers come as stretches whose surfaces fall evenly (as the map's rivers
    /// are), just under the ground; nothing is carved.
    /// </summary>
    /// <param name="h">Heights, rows of cols, a metre apart.</param>
    /// <param name="cols">Points a row.</param>
    /// <param name="rows">Rows.</param>
    /// <param name="x0">Unreal X of the first point.</param>
    /// <param name="y0">Unreal Y of the first point.</param>
    /// <param name="sea">The sea level: ground under it is sea, where rivers end.</param>
    /// <param name="catchment">The ground a river must drain at least, m².</param>
    /// <param name="smallestLake">The smallest lake, m².</param>
    public static (List<MapWater.River> Rivers, List<MapWater.Lake> Lakes) Find(float[] h, int cols, int rows, float x0, float y0, float sea, float catchment, float smallestLake)
    {
        int w = ((cols - 1) / S) + 1, d = ((rows - 1) / S) + 1;
        float[] soft = ZoneGenerator.BoxBlur(h, cols, rows, 2), g = new float[w * d], wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = soft[(Math.Min(rows - 1, z * S) * cols) + Math.Min(cols - 1, x * S)];
                (g[(z * w) + x], wt[(z * w) + x]) = (v, v > sea + 0.2f ? 1 : 0);
            }
        }
        soft = [];
        var (order, to, _, filled) = ZoneGenerator.Drainage(g, wt, w, d, S);
        // the ground each point drains, all its water passed to its one receiver (Drainage's own area spreads it over
        // every lower neighbour, so a wide valley floor never gathers it into one line)
        float[] area = new float[w * d];
        Array.Fill(area, S * S);
        for (int k = order.Length - 1; k >= 0; k--)
        {
            int o = order[k];
            if (to[o] != o)
            {
                area[to[o]] += area[o];
            }
        }
        bool[] pond = new bool[w * d];
        for (int i = 0; i < pond.Length; i++)
        {
            pond[i] = wt[i] > 0 && filled[i] - g[i] > 0.3f;
        }
        Vector2 At(int i) => new(x0 + (i % w * S), y0 + (i / w * S));

        // lakes: each hollow (its cells, four-connected) wide and deep enough, filled to its lip
        var lakes = new List<MapWater.Lake>();
        int[] lakeOf = new int[w * d];
        Array.Fill(lakeOf, -1);
        var levels = new List<float>();
        for (int i = 0; i < pond.Length; i++)
        {
            if (!pond[i] || lakeOf[i] != -1)
            {
                continue;
            }
            var cells = new List<int> { i };
            lakeOf[i] = levels.Count;
            for (int k = 0; k < cells.Count; k++)
            {
                int c = cells[k];
                foreach (int n in (int[])[c % w > 0 ? c - 1 : -1, c % w < w - 1 ? c + 1 : -1, c - w, c + w])
                {
                    if (n >= 0 && n < pond.Length && pond[n] && lakeOf[n] == -1)
                    {
                        lakeOf[n] = levels.Count;
                        cells.Add(n);
                    }
                }
            }
            float level = cells.Max(c => filled[c]), depth = cells.Max(c => filled[c] - g[c]);
            levels.Add(level);
            if (cells.Count * S * S < smallestLake || depth < 1)
            {
                continue;
            }
            var lake = new MapWater.Lake { Name = "Lake", Level = level - 0.1f, Depth = MathF.Round(depth, 1) };
            lake.Outline.AddRange(Outline(cells, w).Select(At));
            if (lake.Outline.Count >= 3)
            {
                lakes.Add(lake);
            }
        }

        // rivers: the lines draining at least catchment, each from its head (none of that above it) down to where it ends
        bool[] river = new bool[w * d], fed = new bool[w * d];
        for (int i = 0; i < river.Length; i++)
        {
            river[i] = wt[i] > 0 && !pond[i] && area[i] >= catchment;
        }
        for (int i = 0; i < river.Length; i++)
        {
            if (river[i] && to[i] != i)
            {
                fed[to[i]] = true;
            }
        }
        var paths = new List<List<int>>();
        for (int i = 0; i < river.Length; i++)
        {
            if (!river[i] || fed[i])
            {
                continue;
            }
            var path = new List<int> { i };
            // down to the sea or a lake (its first cell kept: the river reaches it), or to the edge
            for (int c = i; to[c] != c && river[c];)
            {
                c = to[c];
                path.Add(c);
            }
            paths.Add(path);
        }
        // the longest first; each later one ends where it meets one already made
        bool[] taken = new bool[w * d];
        var rivers = new List<MapWater.River>();
        int made = 0;
        foreach (var path in paths.OrderByDescending(p => p.Count))
        {
            int end = path.FindIndex(c => taken[c]);
            var line = end < 0 ? path : path[..(end + 1)];
            if (line.Count < 12 || made >= MostRivers)
            {
                continue;
            }
            foreach (int c in line)
            {
                taken[c] = true;
            }
            made++;
            int last = line[^1];
            float floor = wt[last] <= 0 ? sea : lakeOf[last] >= 0 ? levels[lakeOf[last]] : float.MinValue;
            rivers.AddRange(Stretched(line, w, x0, y0, h, cols, rows, area, floor));
        }
        return (rivers, lakes);
    }

    /// <summary>
    /// A line of cells as river stretches: eased round its corners, a point every 12 m; its surface 0.3 m under the
    /// ground, never rising, not under floor where it ends; as wide as the ground it drains.
    /// </summary>
    private static IEnumerable<MapWater.River> Stretched(List<int> line, int w, float x0, float y0, float[] h, int cols, int rows, float[] area, float floor)
    {
        var pts = line.Select(c => new Vector2(c % w * S, c / w * S)).ToList();
        for (int pass = 0; pass < 3; pass++)
        {
            pts = [pts[0], .. Enumerable.Range(1, pts.Count - 2).Select(i => (pts[i - 1] + (2 * pts[i]) + pts[i + 1]) / 4), pts[^1]];
        }
        var keep = Enumerable.Range(0, pts.Count).Where(i => i % 3 == 0 || i == pts.Count - 1).ToList();
        float[] surface = new float[keep.Count];
        for (int k = 0; k < keep.Count; k++)
        {
            var p = pts[keep[k]];
            float ground = ZoneGenerator.At(h, cols, rows, p.X, p.Y) - 0.3f;
            surface[k] = k == 0 ? ground : MathF.Min(surface[k - 1] - 0.01f, ground);
        }
        for (int k = 0; k < surface.Length; k++)
        {
            surface[k] = MathF.Max(surface[k], floor);
        }
        foreach (var (a, b) in Stretches(surface))
        {
            var r = new MapWater.River { Name = "River", Top = surface[a], End = surface[b] };
            for (int k = a; k <= b; k++)
            {
                float half = Math.Clamp(1 + (4 * MathF.Sqrt(area[line[keep[k]]] / 1e6f)), 1, 20);
                r.Points.Add(new MapWater.Point(x0 + pts[keep[k]].X, y0 + pts[keep[k]].Y, MathF.Round(half, 1), MathF.Round(MathF.Max(0.6f, half * 0.35f), 1), 0.6f));
            }
            yield return r;
        }
    }

    /// <summary>
    /// A falling surface split into stretches that each fall evenly: each as long (up to a dozen points) as the even fall
    /// keeps the water no more than half a metre over the surface and the bed no more than 1.5 m deeper.
    /// </summary>
    internal static List<(int From, int To)> Stretches(float[] surface)
    {
        var stretches = new List<(int From, int To)>();
        for (int i = 0; i < surface.Length - 1;)
        {
            int end = i + 1;
            while (end + 1 < surface.Length && end + 1 - i <= 12 && Enumerable.Range(i, end + 2 - i).All(j => (surface[i] + ((surface[end + 1] - surface[i]) * (j - i) / (end + 1 - i)) - surface[j]) is > -1.5f and < 0.5f))
            {
                end++;
            }
            stretches.Add((i, end));
            i = end;
        }
        return stretches;
    }

    /// <summary>
    /// The outer edge of some cells (eight-connected, on a grid w wide) as a ring of their indices, clockwise (Moore
    /// neighbour tracing), thinned to at most 64 points.
    /// </summary>
    internal static List<int> Outline(List<int> cells, int w)
    {
        var inside = cells.ToHashSet();
        (int Dx, int Dz)[] dirs = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];
        bool In(int x, int z) => x >= 0 && x < w && z >= 0 && inside.Contains((z * w) + x);
        int start = cells.Min();
        (int X, int Z) cur = (start % w, start / w), back = (cur.X - 1, cur.Z);
        var ring = new List<int> { start };
        for (int steps = 0; steps < (4 * cells.Count) + 8; steps++)
        {
            int bd = Array.IndexOf(dirs, (back.X - cur.X, back.Z - cur.Z));
            bool moved = false;
            for (int k = 1; k <= 8; k++)
            {
                var (dx, dz) = dirs[(bd + k) % 8];
                if (In(cur.X + dx, cur.Z + dz))
                {
                    var (bx, bz) = dirs[(bd + k - 1) % 8];
                    (back, cur, moved) = ((cur.X + bx, cur.Z + bz), (cur.X + dx, cur.Z + dz), true);
                    break;
                }
            }
            if (!moved || (cur.Z * w) + cur.X == start)
            {
                break;
            }
            ring.Add((cur.Z * w) + cur.X);
        }
        int every = (ring.Count + 63) / 64;
        return [.. ring.Where((_, i) => i % every == 0)];
    }
}
