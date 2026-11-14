namespace Groundwork.Desktop;

/// <summary>
/// Judges each ground point against a mask's settings. The mask's settings and the ground's heights and paint are
/// copied when the judge is made, so later edits do not change its answers.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>The layers that pass; null for every layer.</summary>
    private readonly HashSet<int>? _layers;
    /// <summary>The layer a point mostly is, by ground point index.</summary>
    private readonly Func<int, int> _layerAt;
    /// <summary>Lowest ground height that passes, in metres; null for no limit.</summary>
    private readonly float? _heightMin;
    /// <summary>Highest ground height that passes, in metres; null for no limit.</summary>
    private readonly float? _heightMax;
    /// <summary>Smallest slope that passes, in degrees; null for no limit.</summary>
    private readonly float? _slopeMin;
    /// <summary>Largest slope that passes, in degrees; null for no limit.</summary>
    private readonly float? _slopeMax;
    /// <summary>The rule on painted ground.</summary>
    private readonly Mask.PaintRule _paint;
    /// <summary>The ground's width in points.</summary>
    private readonly int _w;
    /// <summary>The ground's height in points.</summary>
    private readonly int _h;
    /// <summary>The ground's paint channels a point (Ground.C).</summary>
    private readonly int _c;
    /// <summary>Each ground point's height, copied; null when no height or slope limit is set.</summary>
    private readonly float[]? _heights;
    /// <summary>Each ground point's paint flag, copied; null when paint does not matter.</summary>
    private readonly byte[]? _paintFlags;
    /// <summary>Each ground point's paint (Ground.C floats), copied; null when paint does not matter.</summary>
    private readonly float[]? _colours;
    /// <summary>The terrain map's value at each ground point; null when no terrain map limits the ground.</summary>
    private readonly float[]? _terrain;
    /// <summary>The terrain map's lowest and highest values that pass.</summary>
    private readonly float? _terrainMin, _terrainMax;

    /// <summary>The last terrain map made (one held), with what it was made from: made again only when the ground changed.</summary>
    private static (string Name, float Sea, int W, int H, double Sum, float[] Values)? _lastTerrain;

    /// <summary>
    /// A terrain map of the ground by ground point, worked out on the ground turned to a map's rows (its first row the
    /// north edge, as TerrainMaps reads them: the ground's grid runs north upwards) and turned back.
    /// </summary>
    private static float[] TerrainOf(string name, Ground g, float sea)
    {
        int w = g.W, h = g.H;
        float[] rows = new float[w * h];
        double sum = 0;
        for (int gz = 0; gz < h; gz++)
        {
            for (int gx = 0; gx < w; gx++)
            {
                float v = g.HeightOf((gz * w) + gx);
                rows[((h - 1 - gz) * w) + gx] = v;
                sum += v * (1 + ((gx * 7) + (gz * 13)) % 101);   // where the heights are counts too, not only their total
            }
        }
        if (_lastTerrain is { } last && last.Name == name && last.Sea == sea && last.W == w && last.H == h && last.Sum == sum)
        {
            return last.Values;
        }
        float[] map = TerrainMaps.One(name, rows, w, h, sea) ?? new float[w * h];
        float[] values = new float[w * h];
        for (int gz = 0; gz < h; gz++)
        {
            Array.Copy(map, (h - 1 - gz) * w, values, gz * w, w);
        }
        _lastTerrain = (name, sea, w, h, sum, values);
        return values;
    }

    /// <summary>Copies the mask's settings and the ground's heights and paint, as they are now.</summary>
    /// <param name="mask">The mask whose settings to judge by.</param>
    /// <param name="g">The ground to judge; its heights and paint are copied now.</param>
    /// <param name="layerAt">The layer a point mostly is, by ground point index.</param>
    /// <param name="sea">The sea level, for a terrain map.</param>
    public MaskJudge(Mask mask, Ground g, Func<int, int> layerAt, float sea = 0)
    {
        _layers = mask.Layers.Count > 0 ? [.. mask.Layers] : null;
        _layerAt = layerAt;
        _heightMin = mask.HeightMin;
        _heightMax = mask.HeightMax;
        _slopeMin = mask.SlopeMin;
        _slopeMax = mask.SlopeMax;
        _paint = mask.Paint;
        _w = g.W;
        _h = g.H;
        _c = g.C;
        if (_heightMin != null || _heightMax != null || _slopeMin != null || _slopeMax != null)
        {
            _heights = new float[_w * _h];
            for (int p = 0; p < _heights.Length; p++)
            {
                _heights[p] = g.HeightOf(p);
            }
        }
        _paintFlags = _paint != Mask.PaintRule.Any ? (byte[])g.PMod.Clone() : null;
        if (mask.Terrain != null && (mask.TerrainMin != null || mask.TerrainMax != null))
        {
            (_terrainMin, _terrainMax) = (mask.TerrainMin, mask.TerrainMax);
            _terrain = TerrainOf(mask.Terrain, g, sea);
        }
        _colours = _paint != Mask.PaintRule.Any ? (float[])g.Paint.Clone() : null;
    }
}
