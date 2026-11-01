using Silk.NET.OpenGL;
using SkiaSharp;

namespace Groundwork.Desktop;

// The ground drawn with the map's layer pictures: each point's paint weights beside its vertex, the pictures repeated
// over the ground and blended by them (a layer without a picture keeps its colour).
public sealed partial class GlView
{
    /// <summary>The ground's paint weights (the second, third and fourth layers'), three floats a point.</summary>
    private uint _weightsVbo;

    /// <summary>The layers' pictures on the graphics card (0: none), and their sizes in metres.</summary>
    private readonly uint[] _layerTex = new uint[4];
    private readonly float[] _layerSize = [4, 4, 4, 4];

    /// <summary>The first texture unit the layers' pictures use (0 and 1 are the holes' and the models').</summary>
    private const int LayerUnit = 4;

    /// <summary>The weights of points x0..x1 of rows z0..z1, row after row.</summary>
    private static float[] Weights(WorldScene s, int x0, int z0, int x1, int z1)
    {
        int n = x1 - x0 + 1;
        float[] w = new float[n * (z1 - z0 + 1) * 3];
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int g = (gz * s.W) + gx, o = (((gz - z0) * n) + gx - x0) * 3;
                (w[o], w[o + 1], w[o + 2]) = (s.Mask[g * 4] / 255f, s.Mask[(g * 4) + 1] / 255f, s.Mask[(g * 4) + 2] / 255f);
            }
        }
        return w;
    }

    /// <summary>For a new area (its vertex array bound): the weights' buffer, and the layers' pictures loaded.</summary>
    private unsafe void BuildGroundTextures(WorldScene s)
    {
        float[] w = Weights(s, 0, 0, s.W - 1, s.H - 1);
        _weightsVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _weightsVbo);
        fixed (float* p = w)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(w.Length * 4), p, BufferUsageARB.DynamicDraw);
        }
        _gl.EnableVertexAttribArray(8);
        _gl.VertexAttribPointer(8, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        for (int k = 0; k < 4; k++)
        {
            var (file, size) = s.LayerTextures.ElementAtOrDefault(k);
            _layerSize[k] = size > 0 ? size : 4;
            if (file != null && LayerPicture(file) is { } img)
            {
                _layerTex[k] = _own.Texture();
                UploadMipmapped(_layerTex[k], img);
            }
        }
    }

    /// <summary>A picture file's pixels (null: none, or not a picture).</summary>
    // ponytail: read whole on the window's thread as the area opens; a loader thread when pictures get big or many
    private static ModelStore.ImageData? LayerPicture(string file)
    {
        try
        {
            using var bmp = SKBitmap.Decode(file);
            using var rgba = bmp?.Copy(SKColorType.Rgba8888);
            return rgba == null ? null : new ModelStore.ImageData(rgba.Width, rgba.Height, rgba.Bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Groundwork.App.Log.CaughtBrief(e, "reading a layer's picture " + file);
            return null;
        }
    }

    /// <summary>After an edit: the changed points' weights, row by row.</summary>
    private unsafe void UpdateWeights(WorldScene s, int x0, int z0, int x1, int z1)
    {
        if (_weightsVbo == 0)
        {
            return;
        }
        float[] w = Weights(s, x0, z0, x1, z1);
        int n = x1 - x0 + 1;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _weightsVbo);
        fixed (float* p = w)
        {
            for (int r = 0; r <= z1 - z0; r++)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (((z0 + r) * s.W) + x0) * 12, (nuint)(n * 12), p + (r * n * 3));
            }
        }
    }

    /// <summary>The ground program's layer uniforms, and the pictures bound.</summary>
    private void UseGroundTextures()
    {
        bool on = _layerTex.Any(t => t != 0) && Scene is not null;
        _gl.Uniform1(_u.At(_terrainProg, "uTexOn"), on ? 1f : 0f);
        if (!on || Scene is not { } s)
        {
            return;
        }
        _gl.Uniform4(_u.At(_terrainProg, "uHas"), _layerTex[0] != 0 ? 1f : 0, _layerTex[1] != 0 ? 1f : 0, _layerTex[2] != 0 ? 1f : 0, _layerTex[3] != 0 ? 1f : 0);
        _gl.Uniform4(_u.At(_terrainProg, "uSize"), _layerSize[0], _layerSize[1], _layerSize[2], _layerSize[3]);
        for (int k = 0; k < 4; k++)
        {
            var c = s.LayerColours.ElementAtOrDefault(k);
            _gl.Uniform3(_u.At(_terrainProg, "uCol" + k), c.X, c.Y, c.Z);
            _gl.Uniform1(_u.At(_terrainProg, "uTex" + k), LayerUnit + k);
            _gl.ActiveTexture(TextureUnit.Texture0 + LayerUnit + k);
            _gl.BindTexture(TextureTarget.Texture2D, _layerTex[k]);
        }
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>The weights' buffer and the layers' pictures freed.</summary>
    private void DeleteGroundTextures()
    {
        _own.DeleteBuffer(_weightsVbo);
        _weightsVbo = 0;
        for (int k = 0; k < 4; k++)
        {
            _own.DeleteTexture(_layerTex[k]);
            _layerTex[k] = 0;
        }
    }
}
