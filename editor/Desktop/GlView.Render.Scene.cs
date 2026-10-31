using System.Numerics;
using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

// Drawing the area: the ground, the objects, the rooms, the water, and the tools' marks over them.
public sealed partial class GlView
{
    /// <summary>
    /// Draws the area: the ground, the objects (opaque, then see-through ones, then glass), the water, and the tools'
    /// marks.
    /// </summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="eye">The eye's position.</param>
    /// <param name="rh">The picture's height in pixels.</param>
    private void DrawScene(WorldScene s, Matrix4x4 vp, Vector3 eye, int rh)
    {
        DrawGround(vp);
        var u = SetObjectUniforms(s, vp, eye);
        bool seeThrough = _seeThrough;
        int drawn = DrawObjectBatches(u, seeThrough);
        _drawnInstances = drawn + DrawGlass(u, seeThrough);
        DrawWater(vp);
        _ = DrawGhosts(s, vp);
        DrawToolMarks(s, vp, rh);
    }

    /// <summary>The ground, then the extensions' layers on it.</summary>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    private unsafe void DrawGround(Matrix4x4 vp)
    {
        UsePlainTerrain(vp);
        if (Scene is { } s)
        {
            SetHoles(s, _terrainProg, true, 0);
        }
        _gl.BindVertexArray(_terrainVao);
        _gl.DrawElements(PrimitiveType.Triangles, _terrainIndexCount, DrawElementsType.UnsignedInt, (void*)0);
        if (Scene is { } scene && _layerMeshes.Count > 0)
        {
            DrawLayerMeshes(scene, _terrainProg, _ => true);
        }
    }

    /// <summary>The water, before the previews and the tools' marks so they show over it: a see-through plane.</summary>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    private unsafe void DrawWater(Matrix4x4 vp)
    {
        if (!ShowWater)
        {
            return;
        }
        _gl.Disable(EnableCap.CullFace);
        GlBlend.Over(_gl);
        _gl.DepthMask(false);
        _gl.UseProgram(_waterProg);
        _gl.UniformMatrix4(_u.At(_waterProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.BindVertexArray(_waterVao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>The tools' marks over the area: the overlays, selection, brush, measure, path, area, place and the rest.</summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="rh">The picture's height in pixels.</param>
    private void DrawToolMarks(WorldScene s, Matrix4x4 vp, int rh)
    {
        DrawSelection(vp);
        DrawHoverObject(vp);
        DrawBrush(s, vp);
        DrawLasso(s, vp);
        DrawScreenBox(vp);
        DrawGizmo(s, vp);
        DrawMeasure(s, vp);
        DrawPlayers(s, vp);
        DrawRivers(vp);
        DrawModPreview(vp);
        if (ShowNewMarkers)
        {
            DrawNewMarkers(s, vp, rh);
        }
    }

    /// <summary>The plain ground program (the game's look off), its faces culled, with the view and the sun.</summary>
    private unsafe void UsePlainTerrain(Matrix4x4 vp)
    {
        var sun = Lighting.SunDirView;
        _gl.Enable(EnableCap.CullFace);
        _gl.UseProgram(_terrainProg);
        _gl.UniformMatrix4(_u.At(_terrainProg, "uViewProj"), 1, false, (float*)&vp);
        _gl.Uniform3(_u.At(_terrainProg, "uSun"), sun.X, sun.Y, sun.Z);
    }
}
