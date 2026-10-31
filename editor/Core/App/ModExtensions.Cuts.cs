using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>
/// The ground cuts running extensions make for the objects of their assets.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension cuts the ground at all.</summary>
    public static bool AnyCuts => _state.Hooks.Any(l => l.Cutters.Count > 0);

    /// <summary>Whether a running extension cuts the ground for an asset.</summary>
    public static bool HasCuts(int asset) => _state.Hooks.Any(l => l.Cutters.ContainsKey(asset));

    /// <summary>Where running extensions leave the ground undrawn for an object of an asset (none when none does).</summary>
    /// <param name="asset">The asset's number (AssetIds).</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="area">The area around it.</param>
    public static List<GroundCut> CutsOf(int asset, IObjectView obj, IAreaView area)
    {
        var cuts = new List<GroundCut>();
        foreach (var l in _state.Hooks)
        {
            foreach (var c in l.Cutters.GetValueOrDefault(asset) ?? [])
            {
                cuts.AddRange(Guard(l, () => c.CutsOf(obj, area)?.Where(ValidCut).ToList() ?? [], []));
            }
        }
        return cuts;
    }

    /// <summary>Whether a cut can be made: an outline of at least three finite points.</summary>
    private static bool ValidCut(GroundCut c) =>
        c.Outline is { Length: >= 3 } && c.Outline.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y));
}
