using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SkiaSharp;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>A model's own picture (ModelThumb), for drawer kinds with no icon: the model in its colours on clear ground.</summary>
public sealed class ModelThumbTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-thumb-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void ARedBoxIsDrawnRedInTheMiddleOnClearGround()
    {
        Box(Path.Combine(_dir, "Box.glb"), new Vector4(0.8f, 0.05f, 0.05f, 1));
        var store = ModelStore.Of(_dir)!;

        using var bmp = SKBitmap.Decode(ModelThumb.Png(store, "Box", 64));
        Assert.Equal((64, 64), (bmp.Width, bmp.Height));
        Assert.Equal(0, bmp.GetPixel(0, 0).Alpha);
        Assert.Equal(0, bmp.GetPixel(63, 63).Alpha);
        var mid = bmp.GetPixel(32, 32);
        Assert.Equal(255, mid.Alpha);
        Assert.True(mid.Red > 2 * mid.Green && mid.Red > 60, $"the middle is {mid}");
        Assert.Null(ModelThumb.Png(store, "Barrel", 64));
    }

    /// <summary>A glb: a 1 m box of one colour, every face wound to face out.</summary>
    private static void Box(string file, Vector4 colour)
    {
        var mesh = new MeshBuilder<VertexPositionNormal>("Box");
        var prim = mesh.UsePrimitive(new MaterialBuilder("Paint").WithBaseColor(colour));
        foreach (var n in (Vector3[])[Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ])
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(n, u);
            VertexPositionNormal P(float a, float b) => new((n + (a * u) + (b * v)) / 2, n);
            prim.AddQuadrangle(P(-1, -1), P(1, -1), P(1, 1), P(-1, 1));
        }
        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);
        scene.ToGltf2().SaveGLB(file);
    }
}
