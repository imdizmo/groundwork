using SharpGLTF.Schema2;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Floating islands: written as a .glb the model loader reads, a domed top over rock hanging to a point.</summary>
public class FloatingIslandTests
{
    [Fact]
    public void AnIslandIsAGlbWithItsTopAndTheRockBelow()
    {
        string file = Path.Combine(Path.GetTempPath(), $"island-{Guid.NewGuid():N}.glb");
        try
        {
            FloatingIsland.Write(file, 0, [(null, new(0.3f, 0.6f, 0.2f), 14), (null, new(0.45f, 0.35f, 0.25f), 8), (null, new(0.4f, 0.4f, 0.45f), 8)]);
            var model = ModelRoot.Load(file);
            var prims = Assert.Single(model.LogicalMeshes).Primitives;
            Assert.Equal(3, prims.Count);
            var top = prims[0].GetVertexAccessor("POSITION").AsVector3Array();
            var below = prims[2].GetVertexAccessor("POSITION").AsVector3Array();
            Assert.True(top.Max(p => p.Y) > 1, "the top is domed");
            Assert.True(below.Min(p => p.Y) < -FloatingIsland.Radius, "the rock hangs deeper than the island is wide");
            Assert.All(prims, p => Assert.NotNull(p.GetVertexAccessor("TEXCOORD_0")));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
