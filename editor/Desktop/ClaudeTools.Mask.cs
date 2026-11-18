using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>Which ground a change may touch, as the Mask panel's settings; what is left out does not limit.</summary>
    public sealed class MaskSpec
    {
        /// <summary>Lowest ground height that passes, metres.</summary>
        [Description("Lowest ground height that changes, metres (optional).")]
        public float? HeightMin { get; set; }

        /// <summary>Highest ground height that passes, metres.</summary>
        [Description("Highest ground height that changes, metres (optional).")]
        public float? HeightMax { get; set; }

        /// <summary>Smallest slope that passes, degrees.</summary>
        [Description("Smallest slope that changes, degrees (optional; e.g. 30: only steep ground).")]
        public float? SlopeMin { get; set; }

        /// <summary>Largest slope that passes, degrees.</summary>
        [Description("Largest slope that changes, degrees (optional; e.g. 15: only gentle ground).")]
        public float? SlopeMax { get; set; }

        /// <summary>The rule on painted ground.</summary>
        [Description("By paint (optional): any, unpainted, painted, or one of the map's painted layers by name (map_catalog's layers; the first layer is unpainted).")]
        public string? Paint { get; set; }

        /// <summary>The terrain map that limits the ground (TerrainMaps.MaskMaps), or null.</summary>
        [Description("By a terrain map (optional): flow, wetness, convexity (0.5 even ground, more on ridges, less in hollows), facing_north, facing_east or snow, each 0..1; with TerrainMin and/or TerrainMax.")]
        public string? Terrain { get; set; }

        /// <summary>The terrain map's lowest value that passes, 0..1.</summary>
        [Description("The terrain map's lowest value that changes, 0..1 (optional; e.g. wetness 0.6: wet ground).")]
        public float? TerrainMin { get; set; }

        /// <summary>The terrain map's highest value that passes, 0..1.</summary>
        [Description("The terrain map's highest value that changes, 0..1 (optional).")]
        public float? TerrainMax { get; set; }
    }

    /// <summary>The Mask over the session's ground for a spec (null: no mask), or why the spec is wrong.</summary>
    internal static (Func<int, float>? Mask, string? Problem) MaskOf(MaskSpec? spec, EditSession s)
    {
        if (spec == null)
        {
            return (null, null);
        }
        if (spec.Terrain != null && !TerrainMaps.MaskMaps.Contains(spec.Terrain))
        {
            return (null, $"No terrain map {spec.Terrain}: one of {string.Join(", ", TerrainMaps.MaskMaps)}.");
        }
        var mask = new Mask { On = true, HeightMin = spec.HeightMin, HeightMax = spec.HeightMax, SlopeMin = spec.SlopeMin, SlopeMax = spec.SlopeMax, Terrain = spec.Terrain, TerrainMin = spec.TerrainMin, TerrainMax = spec.TerrainMax };
        if (PaintOf(mask, spec.Paint) is string badPaint)
        {
            return (null, badPaint);
        }
        if (mask.Warning(english: true) is { Length: > 0 } warning)
        {
            return (null, "The mask lets nothing through: " + warning);
        }
        return (mask.For(s.Ground, s.Scene.LayerAt, sea: s.Scene.Water), null);
    }

    /// <summary>Sets the mask's paint rule from its name (none given: any paint); or says which name is none.</summary>
    /// <param name="mask">The mask whose paint rule is set.</param>
    /// <param name="paint">The rule's name, or null.</param>
    /// <returns>Why the name is wrong, or null when it is not.</returns>
    internal static string? PaintOf(Mask mask, string? paint)
    {
        if (paint == null)
        {
            return null;
        }
        var layers = MapProject.Current?.Layers.Skip(1).Select(l => l.Name).ToList() ?? [];
        int k = layers.FindIndex(n => string.Equals(n, paint, StringComparison.OrdinalIgnoreCase));
        if (k >= 0)
        {
            mask.Paint = Mask.PaintRule.Layer2 + k;
            return null;
        }
        if (paint.ToLowerInvariant() is not ("any" or "unpainted" or "painted") || !Enum.TryParse<Mask.PaintRule>(paint, true, out var rule))
        {
            return $"The mask's paint is any, unpainted, painted{string.Concat(layers.Select(n => ", " + n))}.";
        }
        mask.Paint = rule;
        return null;
    }

    /// <summary>map_mask_picture: which ground a mask lets through, as a black-and-white picture of the map.</summary>
    [McpServerTool(Name = "map_mask_picture", ReadOnly = true, Title = "See where a mask passes")]
    [Description("A black-and-white picture of the whole map, north up: white where the mask lets the ground through, black where not (grey: some of the ground a pixel covers). Check a rule (a slope or height band, a terrain map) before painting or placing with it, e.g. map_paint_whole's limits. Says how much of the ground passes. Changes nothing.")]
    public Task<ModelContextProtocol.Protocol.CallToolResult> MapMaskPicture(
        [Description("The mask to see.")] MaskSpec mask,
        [Description("The picture's longest side in pixels (64..2048; default 1024).")] int size = 1024) => OnUi(() =>
    {
        static ModelContextProtocol.Protocol.CallToolResult Error(string t) => new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = t } } };
        if (NoMap() is string why)
        {
            return Error(why);
        }
        var s = _w.Session!;
        var (test, problem) = MaskOf(mask, s);
        if (problem != null)
        {
            return Error(problem);
        }
        int w = s.Ground.W, h = s.Ground.H;
        var (png, passed, pw, ph) = MaskPicture.Of(test ?? (_ => 1), w, h, Math.Clamp(size, 64, 2048));
        float x0 = s.Scene.OriginX, z0 = s.Scene.OriginZ, x1 = x0 + w - 1, z1 = z0 + h - 1;
        return new()
        {
            Content =
            {
                new ModelContextProtocol.Protocol.TextContentBlock { Text = $"{100.0 * passed / (w * h):0.#}% of the ground passes ({passed} of {w * h} points). The picture is {pw} x {ph}: its left edge x {x0:0}, right x {x1:0}, top z {z1:0} (north), bottom z {z0:0}." },
                ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(png, "image/png"),
            },
        };
    });

    /// <summary>map_picture: the whole map from above, drawn as a map.</summary>
    [McpServerTool(Name = "map_picture", ReadOnly = true, Title = "See the whole map as a map")]
    [Description("A picture of the whole map from straight above, north up, drawn as a map: the ground's colours shaded by its hills, the sea, lakes and rivers, the regions and names, a scale bar. Style colour, parchment or engraving (black ink: hatched slopes, contour lines every 5 m). Changes nothing.")]
    public Task<ModelContextProtocol.Protocol.CallToolResult> MapPictureTool(
        [Description("colour, parchment or engraving")] string style = "colour",
        [Description("The picture's longest side in pixels (256..2048; default 1024).")] int size = 1024) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return new ModelContextProtocol.Protocol.CallToolResult { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = why } } };
        }
        var s = _w.Session!;
        var area = new SceneLayers.AreaView(s.Scene);
        var (png, pw, ph) = MapPicture.Of(area, s.Scene.Water, style.StartsWith("parch", StringComparison.OrdinalIgnoreCase) ? MapPicture.Style.Parchment : style.StartsWith("engrav", StringComparison.OrdinalIgnoreCase) ? MapPicture.Style.Engraving : MapPicture.Style.Colour, Math.Clamp(size, 256, 2048));
        return new ModelContextProtocol.Protocol.CallToolResult
        {
            Content =
            {
                new ModelContextProtocol.Protocol.TextContentBlock { Text = $"The picture is {pw} x {ph}: its left edge x {area.MinX:0}, right x {area.MaxX:0}, top z {area.MaxZ:0} (north), bottom z {area.MinZ:0}." },
                ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(png, "image/png"),
            },
        };
    });
}
