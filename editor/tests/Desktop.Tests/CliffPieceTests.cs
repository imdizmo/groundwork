using SharpGLTF.Schema2;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Cliff pieces: a .glb the model loader reads, a face of rock standing out of its plane in ledges.</summary>
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
            Assert.InRange(rock.Max(p => p.Y), CliffPiece.Height - 3, CliffPiece.Height + 0.01f);
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
    public void ItsFaceIsPillarsInBedsWithGrassOnItsLedgesAndOverItsEdge()
    {
        for (int v = 0; v < CliffPiece.Variants; v++)
        {
            var face = CliffPiece.Face(v);
            // pillars: across the face at mid height, the face steps out or back half a metre or more within 0.3 m
            int joints = Enumerable.Range(0, 80).Count(i =>
            {
                float x = -CliffPiece.Width * 0.4f + (i * CliffPiece.Width * 0.8f / 80);
                return MathF.Abs(face(x + 0.3f, CliffPiece.Height / 2) - face(x, CliffPiece.Height / 2)) > 0.5f;
            });
            Assert.True(joints >= 2, $"variant {v}: {joints} joints across it");
            // beds: up the face, thin grooves every metre or two
            var up = Enumerable.Range(0, 100).Select(j => face(1, j * CliffPiece.Height * 0.8f / 100)).ToList();
            int grooves = Enumerable.Range(1, 98).Count(j => up[j] < up[j - 1] - 0.04f && up[j] < up[j + 1] - 0.04f);
            Assert.True(grooves >= 3, $"variant {v}: {grooves} bedding grooves up it");
            // grass: on ledges down the face, and its top rolling forward over the edge
            var (_, grassI) = CliffPiece.Mesh(v, 4, 4)[1];
            var g = CliffPiece.Mesh(v, 4, 4)[1].V;
            var at = Enumerable.Range(0, g.Count / 8).Select(k => new System.Numerics.Vector3(g[k * 8], g[(k * 8) + 1], g[(k * 8) + 2])).ToList();
            Assert.True(at.Any(p => p.Y < CliffPiece.Height - 4), $"variant {v}: no grass on a ledge");
            float edge = Enumerable.Range(0, 9).Max(i => face((i - 4) * 1f, CliffPiece.Top(v)((i - 4) * 1f) - 0.1f));
            Assert.True(at.Max(p => p.Z) > edge, $"variant {v}: the grass stops short of the edge");
            Assert.NotEmpty(grassI);
        }
    }
}
