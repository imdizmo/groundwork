using System.Numerics;

namespace Groundwork.Desktop;

// The ground the preview draws: a coarse grid over the generated heights, each corner coloured by the paint, the woods
// and the water over it.
public sealed partial class TerrainPreview
{
    /// <summary>Corners (position, normal, colour), triangles, the ground's width (metres) and the height to look at.</summary>
    public sealed record Mesh(float[] V, uint[] I, float Size, float Middle);

    /// <summary>At most this many corners along a side.</summary>
    private const int Corners = 241;

    private static readonly Vector3 Water = new(0.05f, 0.20f, 0.28f), Wood = new(0.02f, 0.07f, 0.02f), Void = new(0.01f, 0.01f, 0.01f);

    /// <summary>
    /// The preview of a zone of cols by rows metres whose pixel (0, 0) is at (x0, y0) (Unreal metres), painted with
    /// <paramref name="layers"/>; the sea at 0 unless the edge is a drop.
    /// </summary>
    public static Mesh Of(ZoneGenerator.Zone z, int cols, int rows, float x0, float y0, IReadOnlyList<MapProject.Layer> layers, bool dry)
    {
        int step = Math.Max(1, (int)MathF.Ceiling((Math.Max(cols, rows) - 1) / (float)(Corners - 1)));
        int nx = ((cols - 1) / step) + 1, ny = ((rows - 1) / step) + 1;
        float[] y = new float[nx * ny];
        var colour = new Vector3[nx * ny];
        float[] water = new float[nx * ny];
        Array.Fill(water, float.MinValue);
        Parallel.For(0, ny, gr =>
        {
            for (int gc = 0; gc < nx; gc++)
            {
                int i = (gr * nx) + gc, c = gc * step, r = gr * step, p = (r * cols) + c;
                float h = z.Heights[p];
                var col = layers[0].Colour;
                foreach (var l in layers.Skip(1).Where(l => l.Channel >= 0))
                {
                    col = Vector3.Lerp(col, l.Colour, z.Mask[(p * 4) + l.Channel] / 255f);
                }
                colour[i] = Vector3.Lerp(col, Wood, z.Wooded[p] * 0.55f);
                y[i] = h;
                if (dry && h < -300)
                {
                    colour[i] = Void;
                }
                else if (!dry && h < 0)
                {
                    water[i] = 0;
                }
                foreach (var lake in z.Lakes.Where(l => MapWater.InsideBy(l.Outline, new Vector2(c + x0, r + y0)) > -step * 0.5f))
                {
                    water[i] = MathF.Max(water[i], lake.Level);
                }
            }
        });
        // the rivers: the corners nearest along each stretch, at its surface there
        foreach (var river in z.Rivers)
        {
            var pts = river.Points;
            float length = pts.Zip(pts.Skip(1), (a, b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y))).Sum(), gone = 0;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                var (a, b) = (new Vector2(pts[k].X, pts[k].Y), new Vector2(pts[k + 1].X, pts[k + 1].Y));
                float seg = Vector2.Distance(a, b);
                for (float t = 0; t <= seg; t += step * 0.5f)
                {
                    var at = Vector2.Lerp(a, b, seg > 0 ? t / seg : 0) - new Vector2(x0, y0);
                    int gc = Math.Clamp((int)MathF.Round(at.X / step), 0, nx - 1), gr = Math.Clamp((int)MathF.Round(at.Y / step), 0, ny - 1);
                    float surface = river.Top + ((river.End - river.Top) * (length > 0 ? (gone + t) / length : 0));
                    water[(gr * nx) + gc] = MathF.Max(water[(gr * nx) + gc], surface);
                }
                gone += seg;
            }
        }
        for (int i = 0; i < y.Length; i++)
        {
            if (water[i] > y[i])
            {
                (y[i], colour[i]) = (water[i], Water);
            }
        }
        // corners: centred, north away from the camera's start (Unreal's +Y is the editor's -z)
        float[] v = new float[nx * ny * 9];
        float mx = (nx - 1) * step / 2f, my = (ny - 1) * step / 2f;
        for (int gr = 0; gr < ny; gr++)
        {
            for (int gc = 0; gc < nx; gc++)
            {
                int i = (gr * nx) + gc;
                float Y(int c, int r) => y[(Math.Clamp(r, 0, ny - 1) * nx) + Math.Clamp(c, 0, nx - 1)];
                var n = Vector3.Normalize(new Vector3(Y(gc - 1, gr) - Y(gc + 1, gr), 2 * step, Y(gc, gr + 1) - Y(gc, gr - 1)));
                var c = colour[i];
                (v[i * 9], v[(i * 9) + 1], v[(i * 9) + 2]) = ((gc * step) - mx, y[i], my - (gr * step));
                (v[(i * 9) + 3], v[(i * 9) + 4], v[(i * 9) + 5]) = (n.X, n.Y, n.Z);
                (v[(i * 9) + 6], v[(i * 9) + 7], v[(i * 9) + 8]) = (c.X, c.Y, c.Z);
            }
        }
        uint[] idx = new uint[(nx - 1) * (ny - 1) * 6];
        int k2 = 0;
        for (int gr = 0; gr + 1 < ny; gr++)
        {
            for (int gc = 0; gc + 1 < nx; gc++)
            {
                uint a = (uint)((gr * nx) + gc), b = a + 1, c = a + (uint)nx, d = c + 1;
                (idx[k2], idx[k2 + 1], idx[k2 + 2], idx[k2 + 3], idx[k2 + 4], idx[k2 + 5]) = (a, c, b, b, c, d);
                k2 += 6;
            }
        }
        var land = y.Where(h => h > -300).DefaultIfEmpty(0);
        return new Mesh(v, idx, Math.Max(cols, rows), (land.Min() + land.Max()) / 2);
    }
}
