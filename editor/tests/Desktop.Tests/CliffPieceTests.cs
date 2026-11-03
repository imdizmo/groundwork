using SharpGLTF.Schema2;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Cliff pieces: a .glb the model loader reads, a wall of chipped rock columns standing out of its plane.</summary>
public class CliffPieceTests
{
    [Fact]
    public void ACliffPieceIsAWallOfRockInLedges()
    {
        string file = Path.Combine(Path.GetTempPath(), $"cliff-{Guid.NewGuid():N}.glb");
        try
        {
            CliffPiece.Write(file, 0, (null, new(0.4f, 0.4f, 0.45f), 8));
            var prim = Assert.Single(Assert.Single(ModelRoot.Load(file).LogicalMeshes).Primitives);   // all rock: the map paints what grows on it
            var rock = prim.GetVertexAccessor("POSITION").AsVector3Array();
            Assert.InRange(rock.Max(p => p.Y), CliffPiece.Height - 3, CliffPiece.Height + 1);   // its tallest leaning a little
            // the face is not flat: up it, ledges stand out of it by a metre or more (at most places across it)
            var face = CliffPiece.Face(0);
            var relief = Enumerable.Range(0, 9).Select(i => Enumerable.Range(0, 20).Select(j => face(-4 + i, j * 0.5f)).ToList()).Select(f => f.Max() - f.Min()).Order().ToList();
            Assert.True(relief[4] > 0.8f, $"face relief {relief[4]} m");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ItsFaceIsChippedColumnsTwiceAsTallAsWideLeaningBackTheirFeetDrawnInWithNoGapsThrough()
    {
        int backs = 0;
        static float Area(System.Numerics.Vector2[] p) => MathF.Abs(Enumerable.Range(0, p.Length).Sum(i => (p[i].X * p[(i + 1) % p.Length].Y) - (p[(i + 1) % p.Length].X * p[i].Y)) / 2);
        for (int v = 0; v < CliffPiece.Variants; v++)
        {
            var columns = CliffPiece.Columns(v);
            // columns: several of them two to three and a half times as tall as wide, a few-sided prism each
            var tall = columns.Where(c => c.H / c.W is >= 2 and <= 3.5f).ToList();
            Assert.True(tall.Count >= 2, $"variant {v}: {tall.Count} tall columns");
            Assert.All(columns, c => Assert.InRange(c.Corners, 5, 7));
            // their feet drawn in: the section at the foot well under the one halfway up
            foreach (var c in tall)
            {
                var sections = CliffPiece.Sections(c);
                Assert.True(Area(sections[0].P) < 0.5f * Area(sections[sections.Length / 2].P), $"variant {v}: a column's foot not drawn in");
            }
            // most shapes' walls leaning back into the hill: their tall columns' tops behind (-z) their feet
            float lean = tall.Average(c => { var r = CliffPiece.Sections(c); return r[(int)(r.Length * 0.85f)].P.Average(p => p.Y) - r[(int)(r.Length * 0.25f)].P.Average(p => p.Y); });
            backs += lean < -0.2f ? 1 : 0;
            // joints: across the face at mid height, the face steps out or back half a metre or more within 0.3 m
            var face = CliffPiece.Face(v);
            int joints = Enumerable.Range(0, 80).Count(i =>
            {
                float x = -CliffPiece.Width * 0.4f + (i * CliffPiece.Width * 0.8f / 80);
                return MathF.Abs(face(x + 0.3f, CliffPiece.Height / 2) - face(x, CliffPiece.Height / 2)) > 0.5f;
            });
            Assert.True(joints >= 2, $"variant {v}: {joints} joints across it");
            // no gaps through it: rock across the whole face, from just over its foot to a little under its top
            for (float x = -CliffPiece.Width * 0.45f; x < CliffPiece.Width * 0.45f; x += 0.25f)
            {
                for (float y = 1; y < CliffPiece.Height - 2.5f; y += 0.5f)
                {
                    Assert.True(face(x, y) > -1.9f, $"variant {v}: a gap through it at {x}, {y}");
                }
            }
            // chipped: its sides' facets turned many ways, not a few flat sides straight up (normals off upright and
            // off the column's few sides)
            var m = CliffPiece.Mesh(v, 4).V;
            var normals = Enumerable.Range(0, m.Count / 8).Select(k => new System.Numerics.Vector3(m[(k * 8) + 3], m[(k * 8) + 4], m[(k * 8) + 5])).ToList();
            float slanting = normals.Count(n => MathF.Abs(n.Y) is > 0.2f and < 0.85f) / (float)normals.Count;
            Assert.True(slanting > 0.12f, $"variant {v}: {slanting:P0} of it slanting facets");
        }
        Assert.True(backs >= CliffPiece.Variants - 2, $"{backs} shapes leaning back");
    }
}
