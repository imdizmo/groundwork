using System.Numerics;
using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

/// <summary>
/// The block look (View panel): the map drawn as a block cut out of the earth, its four sides showing layers of soil
/// and rock that follow the ground above them, and the sea as a band of water where the ground is under it. A look
/// only: nothing of it is saved or goes to Unreal.
/// </summary>
public static class MapBlock
{
    /// <summary>Whether the sides are drawn (the view's sea is cut at the map's edge then too).</summary>
    public static bool On
    {
        get; set
        {
            field = value;
            ModExtensions.Redraw();
        }
    }

    /// <summary>The layers under the ground, from the top: how thick (metres) and their colour (linear).</summary>
    internal static readonly (float Thick, Vector3 Colour)[] Strata =
    [
        (1.5f, new Vector3(0.10f, 0.06f, 0.03f)),
        (5, new Vector3(0.32f, 0.20f, 0.11f)),
        (3, new Vector3(0.45f, 0.33f, 0.20f)),
        (7, new Vector3(0.26f, 0.16f, 0.09f)),
        (4, new Vector3(0.50f, 0.39f, 0.26f)),
        (9, new Vector3(0.30f, 0.19f, 0.11f)),
        (5, new Vector3(0.42f, 0.30f, 0.18f)),
    ];

    private static readonly Vector3 SeaSide = new(0.03f, 0.13f, 0.24f);

    /// <summary>The sides as the editor's own scene layer (rebuilt when the ground changes).</summary>
    public sealed class Scene : ISceneProvider
    {
        /// <inheritdoc/>
        public SceneLayer? Build(IAreaView area) => On ? Sides(area, MapProject.Current?.SeaLevel ?? 0) : null;
    }

    /// <summary>The four sides: a column of layers every step along each edge, down to a flat bottom under the lowest ground.</summary>
    internal static SceneLayer Sides(IAreaView area, float sea)
    {
        float w = area.MaxX - area.MinX, d = area.MaxZ - area.MinZ;
        // ponytail: about a thousand columns a side at most; finer ones would not show at the distance a block is seen from
        float step = MathF.Max(1, MathF.Max(w, d) / 1000);
        var edges = new (Vector2 From, Vector2 To, Vector3 Out)[]
        {
            (new(area.MinX, area.MinZ), new(area.MaxX, area.MinZ), -Vector3.UnitZ),
            (new(area.MaxX, area.MinZ), new(area.MaxX, area.MaxZ), Vector3.UnitX),
            (new(area.MaxX, area.MaxZ), new(area.MinX, area.MaxZ), Vector3.UnitZ),
            (new(area.MinX, area.MaxZ), new(area.MinX, area.MinZ), -Vector3.UnitX),
        };
        var columns = edges.Select(e =>
        {
            int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(e.From, e.To) / step));
            return Enumerable.Range(0, n + 1).Select(i => Vector2.Lerp(e.From, e.To, i / (float)n)).Select(p => (P: p, H: area.GroundHeight(p.X, p.Y))).ToArray();
        }).ToArray();
        float low = columns.SelectMany(c => c).Min(c => c.H);
        float bottom = MathF.Min(low, sea) - MathF.Max(20, MathF.Max(w, d) * 0.06f);
        var layer = new SceneLayer();
        for (int e = 0; e < 4; e++)
        {
            var pos = new List<Vector3>();
            var col = new List<Vector3>();
            var tri = new List<int>();
            void Quad(Vector2 a, Vector2 b, float aTop, float bTop, float aLow, float bLow, Vector3 c)
            {
                if (aTop - aLow < 0.01f && bTop - bLow < 0.01f)
                {
                    return;
                }
                int i = pos.Count;
                pos.AddRange([new(a.X, aTop, a.Y), new(b.X, bTop, b.Y), new(b.X, bLow, b.Y), new(a.X, aLow, a.Y)]);
                col.AddRange([c, c, c, c]);
                // ponytail: both windings, so the side shows whichever way the ground program culls
                tri.AddRange([i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2]);
            }
            var c0 = columns[e];
            for (int i = 0; i + 1 < c0.Length; i++)
            {
                var (a, ha) = c0[i];
                var (b, hb) = c0[i + 1];
                float da = 0, db = 0;
                foreach (var (thick, colour) in Strata)
                {
                    float ta = MathF.Max(bottom, ha - da), tb = MathF.Max(bottom, hb - db);
                    Quad(a, b, ta, tb, MathF.Max(bottom, ha - da - thick), MathF.Max(bottom, hb - db - thick), colour);
                    (da, db) = (da + thick, db + thick);
                }
                Quad(a, b, MathF.Max(bottom, ha - da), MathF.Max(bottom, hb - db), bottom, bottom, Strata[^1].Colour * 0.8f);
                if (ha < sea || hb < sea)
                {
                    Quad(a, b, sea, sea, MathF.Min(sea, ha), MathF.Min(sea, hb), SeaSide);
                }
            }
            var normal = edges[e].Out;
            layer.Meshes.Add(new SceneMesh([.. pos], [.. pos.Select(_ => normal)], [.. tri]) { Colors = [.. col], Pickable = false });
        }
        return layer;
    }
}
