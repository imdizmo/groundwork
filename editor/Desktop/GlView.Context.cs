using Avalonia.OpenGL;
using Groundwork.App;
using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

// The OpenGL context's life: set up when the context is made, and everything made with it deleted when it is lost.
public sealed partial class GlView
{
    /// <summary>Sets up the OpenGL calls and the shader programs.</summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _u = new GlUniforms(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        _terrainProg = Program(Shaders.TerrainVs, Shaders.TerrainFs);
        _objectProg = Program(Shaders.ObjectVs, Shaders.ObjectFs);
        _gl.VertexAttrib4(7, 1f, 1f, 1f, 1f);   // the vertex colour of meshes without one: white (an attribute off reads this)
        _gl.VertexAttrib4(9, 0f, 0f, 0f, 0f);   // the fifth to eighth layers' weights of a map without them: none
        _waterProg = Program(Shaders.WaterVs, Shaders.WaterFs);
        Status?.Invoke(Said.Raw($"OpenGL {(_es ? "ES " : "")}{GlVersion.Major}.{GlVersion.Minor}: {_gl.GetStringS(StringName.Renderer)}"));
    }

    /// <summary>
    /// The view left the window (the map page is shown): Avalonia drops its OpenGL context and makes a
    /// new one when it comes back. Its buffers, textures and programs are shared with Avalonia's own
    /// context, so they outlive it: all are deleted here (the context is still current), then every name
    /// is forgotten and the next frame builds them again in the new one. Keeping the names drew with
    /// names that meant nothing, or something else, in the new context.
    /// </summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _own.DeleteAll();
        _terrainEbo = _waterVbo = _waterEbo = 0;
        _terrainProg = _objectProg = _waterProg = _lineProg = 0;
        _terrainVao = _terrainIndexCount = _waterVao = _terrainVbo = _terrainCutVbo = 0;
        _weightsVbo = _weights2Vbo = _holeTex = _frameFbo = 0;
        Array.Clear(_layerTex);
        _ghostVbo = 0;
        // Drawn through DrawLines, which makes them only while they are 0: kept, the rivers' triangles were written
        // into whatever had the name in the new context (the ground: shards over the whole map).
        _waterLinesVao = _waterLinesVbo = _modPreviewVao = _modPreviewVbo = _shapeGhostVao = _shapeGhostVbo = 0;
        _hoverBoxVao = _hoverBoxVbo = 0;
        _markVao = _markVbo = _markProg = 0;
        _markCount = 0;
        _marksDirty = true;
        _measureVao = _measureVbo = _lassoVao = _lassoVbo = _ringVao = _ringVbo = _falloffVao = _falloffVbo = _lineVao = _lineVbo = _screenBoxVao = _screenBoxVbo = 0;
        _outlineFbo = _outlineTex = _outlineProg = _outlineVao = 0;
        _outlineW = _outlineH = 0;
        // A new context may take the mask's picture: tried again.
        _outlineFailed = false;
        _playersVao = _playersVbo = 0;
        _replayPathVao = _replayPathVbo = _replayPlayerVao = _replayPlayerVbo = 0;
        _nameTex.Clear();
        _textProg = _textVao = _textVbo = _regionVao = _regionVbo = 0;
        _regionLines = null;
        Array.Clear(_replayPostVao);
        Array.Clear(_replayPostVbo);
        _lowFbo = _lowColor = _lowDepth = 0;
        _lowW = _lowH = 0;
        _lowFailed = null;
        _batches.Clear();
        // The extensions' shapes went with the context: made again for the next one.
        _shapeBatches.Clear();
        _shapesDirty = true;
        _shapesGeneration = -1;
        _meshGl.Clear();
        _meshColorGl.Clear();
        _pickMeshes.Clear();
        _bothWays.Clear();
        _onGpu.Clear();
        _gpuBytes = 0;
        lock (_textures)
        {
            _textures.Clear();
            // Textures being read for the old context: read again for the new one (else every model
            // came back without its textures: trees as white leaf cards).
            _claimed.Clear();
        }
        _gizmoGl.Clear();
        _selectionDirty = true;
        _sceneDirty = true;
    }
}
