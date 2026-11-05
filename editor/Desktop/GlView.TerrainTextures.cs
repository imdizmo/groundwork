using Silk.NET.OpenGL;
using SkiaSharp;

namespace Groundwork.Desktop;

// The ground drawn with the map's layer pictures: each point's paint weights beside its vertex, the pictures repeated
// over the ground and blended by them (a layer without a picture keeps its colour).
public sealed partial class GlView
{
    /// <summary>
    /// The ground's paint weights: the second, third and fourth layers', three floats a point (attribute 8), and for a
    /// map of more layers the fifth to eighth layers', four a point (attribute 9; 0 when none: it reads 0 then).
    /// </summary>
    private uint _weightsVbo, _weights2Vbo;

    /// <summary>The layers' pictures on the graphics card (0: none), and their sizes in metres.</summary>
    private readonly uint[] _layerTex = new uint[MapProject.MaxLayers];
    private readonly float[] _layerSize = [.. Enumerable.Repeat(4f, MapProject.MaxLayers)];

    /// <summary>The first texture unit the layers' pictures use (0 and 1 are the holes' and the models').</summary>
    private const int LayerUnit = 4;

    /// <summary>
    /// The weights of points x0..x1 of rows z0..z1, row after row: of the paint channels from c0 on, per floats a point
    /// (those past the scene's channels 0).
    /// </summary>
    private static float[] Weights(WorldScene s, int x0, int z0, int x1, int z1, int c0, int per)
    {
        int n = x1 - x0 + 1;
        float[] w = new float[n * (z1 - z0 + 1) * per];
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int g = (gz * s.W) + gx, o = (((gz - z0) * n) + gx - x0) * per;
                for (int c = 0; c < per && c0 + c < s.C; c++)
                {
                    w[o + c] = s.Mask[(g * s.C) + c0 + c] / 255f;
                }
            }
        }
        return w;
    }

    /// <summary>A weights buffer for the whole area, read by a vertex attribute (its vertex array bound).</summary>
    private unsafe uint WeightsBuffer(WorldScene s, uint attribute, int c0, int per)
    {
        float[] w = Weights(s, 0, 0, s.W - 1, s.H - 1, c0, per);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = w)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(w.Length * 4), p, BufferUsageARB.DynamicDraw);
        }
        _gl.EnableVertexAttribArray(attribute);
        _gl.VertexAttribPointer(attribute, per, VertexAttribPointerType.Float, false, (uint)(per * 4), (void*)0);
        return vbo;
    }

    /// <summary>For a new area (its vertex array bound): the weights' buffers, and the layers' pictures loaded.</summary>
    private void BuildGroundTextures(WorldScene s)
    {
        _weightsVbo = WeightsBuffer(s, 8, 0, 3);
        // the fifth to eighth layers' channels follow the hole's (Ground.ChannelOf)
        _weights2Vbo = s.C > Ground.HoleChannel + 1 ? WeightsBuffer(s, 9, Ground.HoleChannel + 1, 4) : 0;
        for (int k = 0; k < MapProject.MaxLayers; k++)
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
    private void UpdateWeights(WorldScene s, int x0, int z0, int x1, int z1)
    {
        UpdateWeights(s, _weightsVbo, x0, z0, x1, z1, 0, 3);
        UpdateWeights(s, _weights2Vbo, x0, z0, x1, z1, Ground.HoleChannel + 1, 4);
    }

    private unsafe void UpdateWeights(WorldScene s, uint vbo, int x0, int z0, int x1, int z1, int c0, int per)
    {
        if (vbo == 0)
        {
            return;
        }
        float[] w = Weights(s, x0, z0, x1, z1, c0, per);
        int n = x1 - x0 + 1;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = w)
        {
            for (int r = 0; r <= z1 - z0; r++)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (((z0 + r) * s.W) + x0) * per * 4, (nuint)(n * per * 4), p + (r * n * per));
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
        float Has(int k) => _layerTex[k] != 0 ? 1f : 0;
        _gl.Uniform4(_u.At(_terrainProg, "uHas"), Has(0), Has(1), Has(2), Has(3));
        _gl.Uniform4(_u.At(_terrainProg, "uHas2"), Has(4), Has(5), Has(6), Has(7));
        _gl.Uniform4(_u.At(_terrainProg, "uSize"), _layerSize[0], _layerSize[1], _layerSize[2], _layerSize[3]);
        _gl.Uniform4(_u.At(_terrainProg, "uSize2"), _layerSize[4], _layerSize[5], _layerSize[6], _layerSize[7]);
        for (int k = 0; k < MapProject.MaxLayers; k++)
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
        _own.DeleteBuffer(_weights2Vbo);
        (_weightsVbo, _weights2Vbo) = (0, 0);
        for (int k = 0; k < MapProject.MaxLayers; k++)
        {
            _own.DeleteTexture(_layerTex[k]);
            _layerTex[k] = 0;
        }
    }
}
