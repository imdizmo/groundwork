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
            Assert.True(prims[1].GetVertexAccessor("POSITION").AsVector3Array().Min(p => p.Y) > CliffPiece.Height - 4.5f, "the top on top");
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
}
