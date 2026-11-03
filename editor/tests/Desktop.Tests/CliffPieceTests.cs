using SharpGLTF.Schema2;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Cliff pieces: a .glb the model loader reads, a wall of rock columns standing out of its plane, grass on their tops.</summary>
public class CliffPieceTests
{
    [Fact]
    public void ACliffPieceIsAWallOfRockInLedges()
    {
        string file = Path.Combine(Path.GetTempPath(), $"cliff-{Guid.NewGuid():N}.glb");
        try
        {
            CliffPiece.Write(file, 0, (null, new(0.4f, 0.4f, 0.45f), 8), (null, new(0.3f, 0.6f, 0.2f), 14));
            var prims = Assert.Single(ModelRoot.Load(file).LogicalMeshes).Primitives;
            Assert.Equal(2, prims.Count);
            var rock = prims[0].GetVertexAccessor("POSITION").AsVector3Array();
            var grass = prims[1].GetVertexAccessor("POSITION").AsVector3Array();
            Assert.True(grass.Max(p => p.Y) > CliffPiece.Height - 3, "the top on top");
            Assert.InRange(rock.Max(p => p.Y), CliffPiece.Height - 3, CliffPiece.Height + 1);   // its tallest leaning a little
            Assert.True(rock.Min(p => p.Z) <= -CliffPiece.Depth + 0.01f, "it reaches back into the hill");
            // the face is not flat: ledges stand out of it by a metre or more
            var face = Enumerable.Range(0, 20).Select(i => CliffPiece.Face(0)(0, i * 0.5f)).ToList();
            Assert.True(face.Max() - face.Min() > 0.8f, $"face relief {face.Max() - face.Min()} m");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ItsFaceIsColumnsTwiceAsTallAsWideLeaningBackTheirFeetDrawnInGrassOnTheirLowerTops()
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
                var rings = CliffPiece.Rings(c);
                Assert.True(Area(rings[0].P) < 0.5f * Area(rings[rings.Length / 2].P), $"variant {v}: a column's foot not drawn in");
            }
            // most shapes' walls leaning back into the hill: their tall columns' tops behind (-z) their feet
            float lean = tall.Average(c => { var r = CliffPiece.Rings(c); return r[^1].P.Average(p => p.Y) - r[2].P.Average(p => p.Y); });
            backs += lean < -0.2f ? 1 : 0;
            // joints: across the face at mid height, the face steps out or back half a metre or more within 0.3 m
            var face = CliffPiece.Face(v);
            int joints = Enumerable.Range(0, 80).Count(i =>
            {
                float x = -CliffPiece.Width * 0.4f + (i * CliffPiece.Width * 0.8f / 80);
                return MathF.Abs(face(x + 0.3f, CliffPiece.Height / 2) - face(x, CliffPiece.Height / 2)) > 0.5f;
            });
            Assert.True(joints >= 2, $"variant {v}: {joints} joints across it");
            // grass on the tops of the columns stepping down under the tallest (seen from the front, not under the ground)
            var g = CliffPiece.Mesh(v, 4, 4)[1].V;
            var at = Enumerable.Range(0, g.Count / 8).Select(k => new System.Numerics.Vector3(g[k * 8], g[(k * 8) + 1], g[(k * 8) + 2])).ToList();
            Assert.True(at.Any(p => p.Y is < (CliffPiece.Height - 2) and > 1), $"variant {v}: no grass on a lower top");
        }
        Assert.True(backs >= CliffPiece.Variants - 2, $"{backs} shapes leaning back");
    }
}
