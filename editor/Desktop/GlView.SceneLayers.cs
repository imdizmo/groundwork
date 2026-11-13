using System.Numerics;
using Groundwork.Sdk;
using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

// Mods' extensions' layers (SceneLayers) on the graphics card: their meshes, drawn with the ground's program, and the
// cells of ground they leave undrawn.
public sealed partial class GlView
{
    /// <summary>One layer mesh on the graphics card.</summary>
    /// <param name="Vao">Its vertex array (the ground's layout: position, normal, colour).</param>
    /// <param name="Buffers">Its buffers.</param>
    /// <param name="Count">Its index count.</param>
    private sealed record LayerMesh(uint Vao, uint[] Buffers, uint Count);

    /// <summary>What is on the graphics card, and for which result.</summary>
    private SceneLayers.Built? _layersUp;

    /// <summary>The layers' meshes on the graphics card.</summary>
    private readonly List<LayerMesh> _layerMeshes = [];

    /// <summary>The ground cells left undrawn, one byte a cell (0: none uploaded).</summary>
    private uint _holeTex;

    /// <summary>Whether ground cells are left undrawn now.</summary>
    private bool _holesOn;

    /// <summary>The layers worked out last for the area: sent to the graphics card when they are new.</summary>
    private void PrepareLayers(WorldScene s)
    {
        var built = SceneLayers.Shown(s, Wake);
        if (ReferenceEquals(built, _layersUp))
        {
            return;
        }
        DropLayers();
        _layersUp = built;
        foreach (var m in built.Layers.SelectMany(l => l.Meshes).Where(m => m.Triangles.Length > 0))
        {
            _layerMeshes.Add(UploadLayerMesh(s, m));
        }
        UploadHoles(s, built.Holes);
    }

    /// <summary>The layers' meshes and holes off the graphics card.</summary>
    private void DropLayers()
    {
        foreach (var m in _layerMeshes)
        {
            _own.DeleteVertexArray(m.Vao);
            foreach (uint b in m.Buffers)
            {
                _own.DeleteBuffer(b);
            }
        }
        _layerMeshes.Clear();
        _own.DeleteTexture(_holeTex);
        _holeTex = 0;
        _holesOn = false;
        _layersUp = null;
    }

    /// <summary>A mesh in the view's frame with the ground's attributes; its triangles wound for OpenGL (z is mirrored).</summary>
    private LayerMesh UploadLayerMesh(WorldScene s, SceneMesh m)
    {
        int n = m.Positions.Length;
        float[] v = new float[n * 9];
        var c = new Vector3(MathF.Pow(m.Color.X, 2.2f), MathF.Pow(m.Color.Y, 2.2f), MathF.Pow(m.Color.Z, 2.2f));
        for (int i = 0; i < n; i++)
        {
            var p = SceneLayers.ToView(s, m.Positions[i]);
            var nv = m.Normals[i];
            var ci = m.Colors?[i] ?? c;
            (v[i * 9], v[i * 9 + 1], v[i * 9 + 2]) = (p.X, p.Y, p.Z);
            (v[i * 9 + 3], v[i * 9 + 4], v[i * 9 + 5]) = (nv.X, nv.Y, -nv.Z);
            (v[i * 9 + 6], v[i * 9 + 7], v[i * 9 + 8]) = (ci.X, ci.Y, ci.Z);
        }
        uint[] idx = new uint[m.Triangles.Length];
        for (int t = 0; t < idx.Length; t += 3)
        {
            (idx[t], idx[t + 1], idx[t + 2]) = ((uint)m.Triangles[t], (uint)m.Triangles[t + 2], (uint)m.Triangles[t + 1]);
        }
        uint vao = _own.VertexArray();
        _gl.BindVertexArray(vao);
        var (vbo, ebo) = UploadGroundLayout(v, idx);
        _gl.BindVertexArray(0);
        return new LayerMesh(vao, [vbo, ebo], (uint)idx.Length);
    }

    /// <summary>The ground cells left undrawn, one byte a cell, read exactly (no filtering).</summary>
    private unsafe void UploadHoles(WorldScene s, byte[] cells)
    {
        _holesOn = cells.Length == (s.W - 1) * (s.H - 1) && cells.Length > 0;
        if (!_holesOn)
        {
            return;
        }
        _holeTex = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, _holeTex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* p = cells)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)(s.W - 1), (uint)(s.H - 1), 0, PixelFormat.Red, PixelType.UnsignedByte, p);
        }
        _own.Clamped2D(linear: false);
    }

    /// <summary>Tells the bound ground program which cells to leave undrawn (none for the layers' own meshes).</summary>
    /// <param name="s">The area.</param>
    /// <param name="program">The program in use.</param>
    /// <param name="holes">Whether to leave the cells undrawn.</param>
    /// <param name="unit">The texture unit the cells go on.</param>
    private void SetHoles(WorldScene s, uint program, bool holes, int unit)
    {
        bool on = holes && _holesOn;
        _gl.Uniform1(_u.At(program, "uHolesOn"), on ? 1f : 0f);
        // Always on a unit of its own: two samplers of different kinds on one unit fail the draw, used or not.
        _gl.Uniform1(_u.At(program, "uHoles"), unit);
        if (!on)
        {
            return;
        }
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, _holeTex);
        _gl.Uniform4(_u.At(program, "uHoleGrid"), (s.W - 1) / 2f, (s.H - 1) / 2f, s.W - 1, s.H - 1);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>Some of the layers' meshes with a ground program in use.</summary>
    private unsafe void DrawLayerMeshes(WorldScene s, uint program, Func<LayerMesh, bool> which)
    {
        SetHoles(s, program, false, 0);
        // The meshes have no cut attribute (7): it reads the generic one, white for the models' colour, which the
        // ground program takes as cut away. Not cut while they draw.
        _gl.VertexAttrib1(7, 0f);
        foreach (var m in _layerMeshes.Where(which))
        {
            _gl.BindVertexArray(m.Vao);
            _gl.DrawElements(PrimitiveType.Triangles, m.Count, DrawElementsType.UnsignedInt, (void*)0);
        }
        _gl.VertexAttrib4(7, 1f, 1f, 1f, 1f);
    }
}
