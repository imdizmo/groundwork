using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace Groundwork.Desktop;

/// <summary>
/// A small 3D view of generated ground (the generator's window): its heights coloured by its paint, woods and water, lit
/// by a sun; dragged to turn round it, the wheel to come nearer. Nothing stands on it.
/// </summary>
public sealed partial class TerrainPreview : OpenGlControlBase
{
    private GL _gl = null!;
    private GlObjects _own = null!;
    private bool _es;
    private uint _prog, _vao, _vbo, _ebo;
    private int _count;
    /// <summary>The ground to upload on the next frame, and the one shown (uploaded again into a new context).</summary>
    private Mesh? _pending, _shown;
    private float _yaw = 0.7f, _pitch = 0.75f, _zoom = 1;
    private Point? _drag;

    private const string Vs = """
		layout(location = 0) in vec3 aPos;
		layout(location = 1) in vec3 aNor;
		layout(location = 2) in vec3 aCol;
		uniform mat4 uViewProj;
		out vec3 vNor; out vec3 vCol;
		void main() { vNor = aNor; vCol = aCol; gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

    private const string Fs = """
		in vec3 vNor; in vec3 vCol;
		uniform vec3 uSun;
		out vec4 frag;
		void main()
		{
			vec3 c = vCol * (0.3 + 0.8 * max(dot(normalize(vNor), uSun), 0.0));
			frag = vec4(pow(c, vec3(1.0 / 2.2)), 1.0);
		}
		""";

    /// <summary>Shows other ground (from any thread; drawn on the next frame).</summary>
    public void Show(Mesh mesh)
    {
        _pending = mesh;
        Avalonia.Threading.Dispatcher.UIThread.Post(RequestNextFrameRendering);
    }

    /// <summary>Sets up the OpenGL calls and the program.</summary>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        _own = new GlObjects(_gl);
        _es = GlVersion.Type == GlProfileType.OpenGLES;
        string head = _es ? "#version 300 es\nprecision highp float;\n" : "#version 330 core\n";
        _prog = _own.Program(GlProgramLinker.Link(_gl, head, Vs, Fs));
    }

    /// <summary>The context goes: all deleted, the ground shown uploaded again into the next one.</summary>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _own.DeleteAll();
        (_prog, _vao, _vbo, _ebo, _count) = (0, 0, 0, 0, 0);
        _pending ??= _shown;
    }

    /// <summary>Draws the ground (uploading new ground first) from the orbit's eye.</summary>
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_pending is { } m)
        {
            _pending = null;
            Upload(m);
        }
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        int w = Math.Max(1, (int)(Bounds.Width * scaling)), h = Math.Max(1, (int)(Bounds.Height * scaling));
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.ClearColor(0.10f, 0.12f, 0.15f, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        if (_shown is not { } s || _count == 0)
        {
            return;
        }
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        float distance = s.Size * 1.75f / _zoom;
        var target = new Vector3(0, s.Middle, 0);
        var eye = target + (distance * new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw)));
        var viewProj = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(0.8f, w / (float)h, distance * 0.01f, distance * 4);
        _gl.UseProgram(_prog);
        _gl.UniformMatrix4(_gl.GetUniformLocation(_prog, "uViewProj"), 1, false, (float*)&viewProj);
        var sun = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));
        _gl.Uniform3(_gl.GetUniformLocation(_prog, "uSun"), sun.X, sun.Y, sun.Z);
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)_count, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.DepthTest);
    }

    private unsafe void Upload(Mesh m)
    {
        // the last ground's buffers go with it
        if (_vao != 0)
        {
            _own.DeleteVertexArray(_vao);
            _own.DeleteBuffer(_vbo);
            _own.DeleteBuffer(_ebo);
        }
        _vao = _own.VertexArray();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo = _own.Buffer());
        _own.Upload(BufferTargetARB.ArrayBuffer, m.V);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo = _own.Buffer());
        _own.Upload(BufferTargetARB.ElementArrayBuffer, m.I);
        for (uint a = 0; a < 3; a++)
        {
            _gl.EnableVertexAttribArray(a);
            _gl.VertexAttribPointer(a, 3, VertexAttribPointerType.Float, false, 36, (void*)(a * 12));
        }
        _gl.BindVertexArray(0);
        (_count, _shown) = (m.I.Length, m);
    }

    /// <summary>A drag starts: the view turns with it.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _drag = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    /// <summary>Dragging: round (left and right) and up and down.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_drag is { } from)
        {
            var at = e.GetPosition(this);
            _yaw -= (float)(at.X - from.X) * 0.01f;
            _pitch = Math.Clamp(_pitch + ((float)(at.Y - from.Y) * 0.01f), 0.08f, 1.5f);
            _drag = at;
            RequestNextFrameRendering();
        }
    }

    /// <summary>The drag ends.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _drag = null;
        e.Pointer.Capture(null);
    }

    /// <summary>The wheel: nearer or further.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _zoom = Math.Clamp(_zoom * MathF.Pow(1.15f, (float)e.Delta.Y), 0.5f, 12);
        RequestNextFrameRendering();
    }
}
