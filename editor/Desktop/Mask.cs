using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The Mask, like the web editor's (editor/masks.js; WorldEdit's masks, WorldPainter's filters): limits brushes, paths,
/// shapes, area actions and pastes to some of the map's layers, a height range, a slope range or painted / unpainted ground. Off,
/// everything passes.
/// </summary>
public sealed class Mask
{
    /// <summary>
    /// Which ground passes by its paint. Layer2 to Layer8 need painted ground with that layer at least half.
    /// </summary>
    public enum PaintRule
    {
        /// <summary>Paint does not matter.</summary>
        Any,
        /// <summary>Ground never painted, or painted almost black.</summary>
        Unpainted,
        /// <summary>Painted ground.</summary>
        Painted,
        /// <summary>Painted with the map's second layer at least half.</summary>
        Layer2,
        /// <summary>Painted with the map's third layer at least half.</summary>
        Layer3,
        /// <summary>Painted with the map's fourth layer at least half.</summary>
        Layer4,
        /// <summary>Painted with the map's fifth layer at least half.</summary>
        Layer5,
        /// <summary>Painted with the map's sixth layer at least half.</summary>
        Layer6,
        /// <summary>Painted with the map's seventh layer at least half.</summary>
        Layer7,
        /// <summary>Painted with the map's eighth layer at least half.</summary>
        Layer8,
    }

    /// <summary>
    /// Whether the mask applies at all; off, <c>For</c> returns null and everything passes.
    /// </summary>
    public bool On { get; set; }
    /// <summary>
    /// The layers that pass (0: the first), by the layer a point mostly is. None chosen: every layer.
    /// </summary>
    public HashSet<int> Layers { get; } = [];
    /// <summary>
    /// Lowest ground height that passes, in metres; null for no limit.
    /// </summary>
    public float? HeightMin { get; set; }
    /// <summary>
    /// Highest ground height that passes, in metres; null for no limit.
    /// </summary>
    public float? HeightMax { get; set; }
    /// <summary>
    /// Smallest slope that passes, in degrees; null for no limit.
    /// </summary>
    public float? SlopeMin { get; set; }
    /// <summary>
    /// Largest slope that passes, in degrees; null for no limit.
    /// </summary>
    public float? SlopeMax { get; set; }
    /// <summary>
    /// The rule on painted ground.
    /// </summary>
    public PaintRule Paint { get; set; }
    /// <summary>
    /// A terrain map that limits the ground too (TerrainMaps.MaskMaps: flow, wetness, convexity, facing north or east,
    /// snow); null: none.
    /// </summary>
    public string? Terrain { get; set; }
    /// <summary>The terrain map's lowest value that passes, 0..1; null for no limit.</summary>
    public float? TerrainMin { get; set; }
    /// <summary>The terrain map's highest value that passes, 0..1; null for no limit.</summary>
    public float? TerrainMax { get; set; }
    /// <summary>
    /// Raised by <c>Notify</c> after the settings change.
    /// </summary>
    public event Action? Changed;
    /// <summary>
    /// Tells listeners the settings changed; call after setting the properties.
    /// </summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>
    /// Settings that let (almost) nothing through.
    /// </summary>
    /// <param name="english">In English (what Claude is told) rather than the language in use (the Mask panel).</param>
    /// <returns>The warnings joined in one line, or empty when the settings are fine.</returns>
    public string Warning(bool english = false)
    {
        var w = new List<Said>();
        if (SlopeMax is <= 0)
        {
            w.Add(Said.Of("mask.error.flatOnly", SlopeMax));
        }
        if (SlopeMin is float a && SlopeMax is float b && a > b)
        {
            w.Add(Said.Of("mask.error.slopeRange"));
        }
        if (HeightMin is float c && HeightMax is float d && c > d)
        {
            w.Add(Said.Of("mask.error.heightRange"));
        }
        if (Terrain != null && TerrainMin is float e && TerrainMax is float f && e > f)
        {
            w.Add(Said.Of("mask.error.terrainRange"));
        }
        var said = Said.Join(" ", w);
        return english ? said.English : said.Shown;
    }

    /// <summary>
    /// The mask over a ground (1 where it passes, 0 elsewhere), or null when off. The ground is judged as it is now,
    /// when the mask is made: an edit that changes it while asking (a mountain, a shape, a paste, a brush stroke) is
    /// not judged against its own changes (a slope then mixed changed and unchanged neighbours).
    /// </summary>
    /// <param name="g">The ground to judge; its heights and paint are copied now.</param>
    /// <param name="layerAt">The layer a point mostly is, by ground point index.</param>
    /// <param name="frozen">Each point is also judged only once (the answer is remembered).</param>
    /// <param name="sea">The sea level, for a terrain map (water runs off into the sea).</param>
    /// <returns>A test by ground point index, or null when the mask is off.</returns>
    public Func<int, float>? For(Ground g, Func<int, int> layerAt, bool frozen = false, float sea = 0)
    {
        if (!On)
        {
            return null;
        }
        var judge = new MaskJudge(this, g, layerAt, sea);
        if (!frozen)
        {
            return judge.Test;
        }
        var seen = new Dictionary<int, float>();
        return p => seen.TryGetValue(p, out float v) ? v : seen[p] = judge.Test(p);
    }

    /// <summary>
    /// Alt + Shift + click: the height range around the clicked ground (±2 m). Turns the mask on.
    /// </summary>
    /// <param name="h">The clicked ground's height, in metres.</param>
    public void PickHeight(float h)
    {
        On = true;
        HeightMin = MathF.Round(h - 2, 1);
        HeightMax = MathF.Round(h + 2, 1);
        Notify();
    }
}
