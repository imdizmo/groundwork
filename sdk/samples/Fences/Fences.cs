using System.Globalization;
using System.Numerics;
using Groundwork.Sdk;

[assembly: GroundworkPlugin(typeof(Fences.Plugin))]

namespace Fences;

/// <summary>
/// The plugin's entry: a Fence tool on the rail, and the rails drawn between a fence's posts. A post is an object of the
/// asset <see cref="Post"/> with three values in its data: the fence it belongs to, its place along it, and the rails'
/// height. The map saves them with the post, so the rails come back when the map is opened again.
/// </summary>
public sealed class Plugin : IGroundworkPlugin
{
    /// <summary>The posts' asset (a map without a model of that name draws them as the shapes below only).</summary>
    public const string Post = "FencePost";

    /// <inheritdoc/>
    public void Register(IPluginRegistry registry)
    {
        registry.AddTool(new FenceTool());
        registry.AddShapes(new FenceShapes(), Post);
    }
}

/// <summary>
/// Click on the ground to set a fence's posts one after another, each one a step of the history; Enter or Esc ends the
/// fence, and the next click starts another. A line shows where the next rail goes.
/// </summary>
public sealed class FenceTool : IEditorTool
{
    private string? _fence;
    private int _next;
    private Vector3 _last;

    /// <inheritdoc/>
    public ToolInfo Info { get; } = new("fence", "Fence", "Click to set posts, one after another; Enter or Esc ends the fence.")
    {
        Icon = "M4 20V6l2-2 2 2v14M16 20V6l2-2 2 2v14M8 9h8M8 15h8",
    };

    /// <inheritdoc/>
    public IReadOnlyList<ToolControl> Controls { get; } =
    [
        new SliderControl("rail", "Rail height", 0.3f, 2f, 1f) { Step = 0.1f, Unit = "m" },
        new TextControl("said", "Click on the ground to set the first post."),
    ];

    /// <inheritdoc/>
    public void OnChosen(IToolContext ctx) => End(ctx);

    /// <inheritdoc/>
    public void OnLeft(IToolContext ctx) => End(ctx);

    /// <inheritdoc/>
    public void OnPointer(IToolContext ctx, PointerInput input)
    {
        if (input.Target != PointerTarget.Ground)
        {
            return;
        }
        if (input.Action == PointerAction.Move && _fence != null)
        {
            ctx.Preview([FenceShapes.Rail(_last, input.Point, ctx.Number("rail"), new Vector4(1, 0.8f, 0.2f, 0.6f))]);
        }
        else if (input.Action == PointerAction.Press)
        {
            _fence ??= Guid.NewGuid().ToString("N")[..8];
            var data = new Dictionary<string, string>
            {
                ["fence"] = _fence,
                ["order"] = _next.ToString(CultureInfo.InvariantCulture),
                ["rail"] = ctx.Number("rail").ToString("0.##", CultureInfo.InvariantCulture),
            };
            ctx.Commit("Fence post", new ToolEdit().Add(Plugin.Post, input.Point, Vector3.Zero, data));
            (_last, _next) = (input.Point, _next + 1);
            ctx.SetText("said", $"{_next} post(s). Enter or Esc ends the fence.");
        }
    }

    /// <inheritdoc/>
    public bool OnKey(IToolContext ctx, string key, KeyModifiers mods)
    {
        if (_fence == null || key is not ("Enter" or "Escape"))
        {
            return false;
        }
        End(ctx);
        return true;
    }

    /// <inheritdoc/>
    public void OnControl(IToolContext ctx, string id)
    {
    }

    private void End(IToolContext ctx)
    {
        (_fence, _next) = (null, 0);
        ctx.Preview([]);
        ctx.SetText("said", "Click on the ground to set the first post.");
    }
}

/// <summary>A post's shapes: the post itself, and the rail to the next post of its fence.</summary>
public sealed class FenceShapes : IShapeProvider
{
    private static readonly Vector4 Wood = new(0.45f, 0.32f, 0.2f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IAreaView area)
    {
        float rail = Value(obj, "rail", 1);
        yield return Box(new Vector3(-0.08f, 0, -0.08f), new Vector3(0.08f, rail + 0.2f, 0.08f), Wood);
        if (!obj.Data.TryGetValue("fence", out string? fence))
        {
            yield break;
        }
        int order = (int)Value(obj, "order", -1);
        var next = area.ObjectsOf(Plugin.Post).FirstOrDefault(o => o.Data.GetValueOrDefault("fence") == fence && (int)Value(o, "order", -1) == order + 1);
        if (next != null)
        {
            yield return Rail(obj.Position, next.Position, rail, Wood);
        }
    }

    /// <summary>A rail from one post to the next, in the world's frame: a thin box at the given height.</summary>
    public static Shape Rail(Vector3 from, Vector3 to, float height, Vector4 color)
    {
        var along = to - from;
        var side = Vector3.Normalize(Vector3.Cross(along.LengthSquared() > 1e-6f ? along : Vector3.UnitX, Vector3.UnitY)) * 0.04f;
        var up = new Vector3(0, height, 0);
        Vector3[] v =
        [
            from + up - side, from + up + side, to + up + side, to + up - side,
            from + up - side + new Vector3(0, 0.12f, 0), from + up + side + new Vector3(0, 0.12f, 0),
            to + up + side + new Vector3(0, 0.12f, 0), to + up - side + new Vector3(0, 0.12f, 0),
        ];
        return new Shape(v, BoxTriangles, color) { WorldSpace = true, BothSides = true };
    }

    private static Shape Box(Vector3 min, Vector3 max, Vector4 color)
    {
        Vector3[] v =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];
        return new Shape(v, BoxTriangles, color) { BothSides = true };
    }

    // ponytail: drawn from both sides, so the winding need not be right
    private static readonly int[] BoxTriangles =
    [
        0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0,
    ];

    private static float Value(IObjectView obj, string key, float otherwise) =>
        obj.Data.TryGetValue(key, out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : otherwise;
}
