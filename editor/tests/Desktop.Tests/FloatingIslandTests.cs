using System.Text.Json.Nodes;
using SharpGLTF.Schema2;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Floating islands: written as a .glb the model loader reads, a domed top over smooth ground hanging below.</summary>
public class FloatingIslandTests
{
    [Fact]
    public void IslandsAreWrittenOnlyIntoARealModelsFolderAndNeverThroughALink()
    {
        string dir = Directory.CreateTempSubdirectory("gw-islands-").FullName;
        try
        {
            var m = MapProject.Create("Isle", 1, generated: false, height: 10, seed: 1, under: dir);
            // its own models folder: made, the islands in it
            Assert.Equal(FloatingIsland.Variants, FloatingIsland.WriteFor(m).Count);
            string own = Path.Combine(m.Folder, "models");
            Assert.Equal(FloatingIsland.Variants, Directory.GetFiles(own, "FloatingIsland_*.glb").Length);
            // a link planted where an island goes: refused, its target untouched
            string target = Path.Combine(dir, "precious.txt");
            File.WriteAllText(target, "keep");
            string first = Path.Combine(own, FloatingIsland.Models("Isle")[0] + ".glb");
            File.Delete(first);
            File.CreateSymbolicLink(first, target);
            Assert.Throws<InvalidDataException>(() => FloatingIsland.WriteFor(m));
            Assert.Equal("keep", File.ReadAllText(target));
            // a models folder elsewhere that is not one (missing, or without models): nothing made there
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(m.Folder, "map.json")))!;
            string elsewhere = Path.Combine(dir, "made-for-me");
            json["models"] = elsewhere;
            File.WriteAllText(Path.Combine(m.Folder, "map.json"), json.ToJsonString());
            Assert.Throws<InvalidDataException>(() => FloatingIsland.WriteFor(MapProject.Open(m.Folder)));
            Assert.False(Directory.Exists(elsewhere));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AnIslandIsAGlbWithItsTopAndSmoothGroundBelow()
    {
        string file = Path.Combine(Path.GetTempPath(), $"island-{Guid.NewGuid():N}.glb");
        try
        {
            FloatingIsland.Write(file, 0, [(null, new(0.3f, 0.6f, 0.2f), 14), (null, new(0.45f, 0.35f, 0.25f), 8)]);
            var model = ModelRoot.Load(file);
            var prims = Assert.Single(model.LogicalMeshes).Primitives;
            Assert.Equal(2, prims.Count);
            var top = prims[0].GetVertexAccessor("POSITION").AsVector3Array();
            var below = prims[1].GetVertexAccessor("POSITION").AsVector3Array();
            Assert.True(top.Max(p => p.Y) > 1, "the top is domed");
            Assert.True(below.Min(p => p.Y) < -FloatingIsland.Radius * 0.6f, "the ground hangs below like a boulder");
            // smooth, like a boulder: its triangles share their corners (flat faces have three each)
            Assert.True(below.Count * 4 < prims[1].GetIndices().Count, "the underside is smooth");
            Assert.All(prims, p => Assert.NotNull(p.GetVertexAccessor("TEXCOORD_0")));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
