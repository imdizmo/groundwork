using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SkiaSharp;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A map's glTF models (ModelStore.Gltf): a file anywhere under the models folder is an asset of its name; its nodes'
/// meshes are its parts where the nodes put them; its triangles face the way their normals do; the base colour and
/// its texture are the material; a name with no file is no model.
/// </summary>
public sealed class ModelStoreGltfTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-gltf-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void AGlbIsAModelWithItsPartsPlacesColourAndTexture()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pack"));
        Make(Path.Combine(_dir, "pack", "Crate.glb"));
        var store = ModelStore.Of(_dir)!;

        var model = store.LoadModel("Crate")!;
        Assert.Equal(2, model.Parts.Count);
        Assert.Contains(model.Parts, p => p.Matrix.Translation == new Vector3(0, 2, 0));

        var mesh = store.LoadMesh(model.Parts[0].Mesh)!;
        float[] v = mesh.Vertices;
        uint[] t = mesh.Submeshes[model.Parts[0].Sub];
        Vector3 P(uint i) => new(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]);
        var face = Vector3.Cross(P(t[1]) - P(t[0]), P(t[2]) - P(t[0]));
        var normal = new Vector3(v[(t[0] * 8) + 3], v[(t[0] * 8) + 4], v[(t[0] * 8) + 5]);
        Assert.True(Vector3.Dot(face, normal) > 0, "the triangle faces away from its normal");

        var mat = store.Material(model.Parts[0].Material);
        Assert.Equal(0.5f, mat.Color.X, 3);
        var tex = store.LoadTexture(mat.Map!)!;
        Assert.Equal((4, 2), (tex.Width, tex.Height));

        Assert.Null(store.LoadModel("Barrel"));
    }

    [Fact]
    public void VertexColoursAreReadAndAMeshWithoutThemHasNone()
    {
        var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1>("Painted");
        var up = Vector3.UnitY;
        mesh.UsePrimitive(new MaterialBuilder("Plain")).AddTriangle(
            (new VertexPositionNormal(0, 0, 0, up.X, up.Y, up.Z), new VertexColor1(new Vector4(1, 0, 0, 1))),
            (new VertexPositionNormal(0, 0, 1, up.X, up.Y, up.Z), new VertexColor1(new Vector4(0, 1, 0, 1))),
            (new VertexPositionNormal(1, 0, 0, up.X, up.Y, up.Z), new VertexColor1(new Vector4(0, 0, 1, 0.5f))));
        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);
        scene.ToGltf2().SaveGLB(Path.Combine(_dir, "Painted.glb"));
        Make(Path.Combine(_dir, "Crate.glb"));
        var store = ModelStore.Of(_dir)!;

        var painted = store.LoadMesh(store.LoadModel("Painted")!.Parts[0].Mesh)!;
        Assert.NotNull(painted.Colors);
        Assert.Equal(painted.Vertices.Length / 8 * 4, painted.Colors.Length);
        var all = Enumerable.Range(0, painted.Colors.Length / 4).Select(i => new Vector4(painted.Colors[i * 4], painted.Colors[(i * 4) + 1], painted.Colors[(i * 4) + 2], painted.Colors[(i * 4) + 3])).ToList();
        Assert.Contains(all, c => Vector4.Distance(c, new Vector4(0, 0, 1, 0.5f)) < 0.01f);   // stored as bytes: 0.5 comes back as 128/255
        Assert.Null(store.LoadMesh(store.LoadModel("Crate")!.Parts[0].Mesh)!.Colors);
    }

    /// <summary>A glb: a one-triangle mesh, red half (linear), with a 4 x 2 texture, at the root and again 2 m up.</summary>
    private static void Make(string file)
    {
        using var bmp = new SKBitmap(4, 2);
        bmp.Erase(SKColors.White);
        using var png = bmp.Encode(SKEncodedImageFormat.Png, 100);
        var material = new MaterialBuilder("Wood").WithBaseColor(new SharpGLTF.Memory.MemoryImage(png.ToArray()), new Vector4(0.5f, 0, 0, 1));
        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1>("Board");
        var up = Vector3.UnitY;
        mesh.UsePrimitive(material).AddTriangle(
            (new VertexPositionNormal(0, 0, 0, up.X, up.Y, up.Z), new VertexTexture1(Vector2.Zero)),
            (new VertexPositionNormal(0, 0, 1, up.X, up.Y, up.Z), new VertexTexture1(Vector2.UnitY)),
            (new VertexPositionNormal(1, 0, 0, up.X, up.Y, up.Z), new VertexTexture1(Vector2.UnitX)));
        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);
        scene.AddRigidMesh(mesh, Matrix4x4.CreateTranslation(0, 2, 0));
        scene.ToGltf2().SaveGLB(file);
    }
}
