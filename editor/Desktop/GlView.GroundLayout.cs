using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

/// <summary>
/// The vertex layout the ground's program reads, shared by the ground and the mods' layers drawn like it.
/// </summary>
public sealed partial class GlView
{
    /// <summary>
    /// Fills the bound vertex array with a mesh in the ground's layout: position, normal and colour (nine floats a
    /// corner, attributes 0 to 2), and its triangles.
    /// </summary>
    /// <param name="v">The corners' nine floats each.</param>
    /// <param name="idx">The triangles' corners.</param>
    /// <returns>The buffers made: corners, triangles.</returns>
    private unsafe (uint Vbo, uint Ebo) UploadGroundLayout(float[] v, uint[] idx)
    {
        uint vbo = _own.Buffer(), ebo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        _own.Upload(BufferTargetARB.ArrayBuffer, v);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        _own.Upload(BufferTargetARB.ElementArrayBuffer, idx);
        for (uint a = 0; a < 3; a++)
        {
            _gl.EnableVertexAttribArray(a);
            _gl.VertexAttribPointer(a, 3, VertexAttribPointerType.Float, false, 36, (void*)(a * 12));
        }
        return (vbo, ebo);
    }
}
