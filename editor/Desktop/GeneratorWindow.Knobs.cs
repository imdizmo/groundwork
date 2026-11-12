using Spec = Groundwork.Desktop.ZoneGenerator.Spec;

namespace Groundwork.Desktop;

// The generator's numbers as sliders, in groups: each its label, unit, range and step, read from a spec and set in one.
public sealed partial class GeneratorWindow
{
    /// <summary>One number of the spec as a slider.</summary>
    private sealed record Knob(string Group, string Label, string Unit, double Min, double Max, double Step, Func<Spec, double> Get, Func<Spec, double, Spec> Set, string Tip = "");

    private static float F(double v) => (float)v;

    private static int I(double v) => (int)Math.Round(v);

    /// <summary>The lakes asked: so many, the biggest this big and each next one about half the last.</summary>
    private static float[] Lakes(int count, double biggest) => [.. Enumerable.Range(0, count).Select(i => MathF.Round(F(biggest * Math.Pow(0.5, i)) * 10) / 10).Select(h => MathF.Max(0.5f, h))];

    private static readonly Knob[] Knobs =
    [
        new("Ground", "Lowest ground", "m", -100, 500, 5, s => s.Low, (s, v) => s with { Low = F(v) }),
        new("Ground", "Highest ground", "m", -50, 1500, 5, s => s.High, (s, v) => s with { High = F(v) }),
        new("Ground", "Hills apart", "m", 50, 2000, 25, s => s.HillSize, (s, v) => s with { HillSize = F(v) }, "How far from one hill to the next: small for bumpy ground, big for long slopes."),
        new("Ground", "Roughness", "m", 0, 10, 0.5, s => s.Rough, (s, v) => s with { Rough = F(v) }, "Small bumps over the hills, so no ground is quite flat."),
        new("Ground", "Terrace steps", "m", 0, 100, 5, s => s.Terraces, (s, v) => s with { Terraces = F(v) }, "The height of each step of plateaus and terraces (0: none)."),
        new("Ground", "Terraced share", "%", 0, 100, 5, s => s.TerraceShare * 100, (s, v) => s with { TerraceShare = F(v / 100) }),
        new("Ground", "Canyon depth", "m", 0, 150, 5, s => s.Canyons, (s, v) => s with { Canyons = F(v) }, "Chasms cut through the ground (0: none)."),
        new("Ground", "Canyon width", "m", 10, 200, 5, s => s.CanyonWidth, (s, v) => s with { CanyonWidth = F(v) }),
        new("Ground", "Canyons apart", "m", 200, 3000, 50, s => s.CanyonSpacing, (s, v) => s with { CanyonSpacing = F(v) }),
        new("Ground", "Fjord depth", "m", 0, 800, 10, s => s.Fjords, (s, v) => s with { Fjords = F(v) }, "Inlets cut from the coast into the high ground, sheer-walled, this deep under the sea at their mouths (0: none)."),
        new("Ground", "Fjord width", "m", 50, 1000, 10, s => s.FjordWidth, (s, v) => s with { FjordWidth = F(v) }),
        new("Ground", "Fjords apart", "m", 300, 4000, 50, s => s.FjordSpacing, (s, v) => s with { FjordSpacing = F(v) }),
        new("Ground", "Cliffs from", "°", 0, 70, 1, s => s.CliffSlope, (s, v) => s with { CliffSlope = F(v) }, "Ground steeper than this faced with walls of rock, pieces overlapping along it (0: none; not in the preview)."),
        new("Ground", "Crags", "m", 0, 15, 0.5, s => s.Crags, (s, v) => s with { Crags = F(v) }, "The rock faces' grain, so deep: crags, buttresses and gullies, ribs and chutes running down them (0: smooth)."),
        new("Mountain rim", "Rim height", "m", 0, 800, 10, s => s.RimHeight, (s, v) => s with { RimHeight = F(v) }, "A wall of ridged mountains round the edge (0: none)."),
        new("Mountain rim", "Rim width", "m", 0, 1000, 10, s => s.RimWidth, (s, v) => s with { RimWidth = F(v) }),
        new("Coast", "Coast width", "m", 0, 4000, 50, s => s.Coast, (s, v) => s with { Coast = F(v) }, "How far in from one side the ground goes under the sea, or drops away (0: no coast)."),
        new("Coast", "Sea depth", "m", 0, 800, 5, s => s.CoastDepth, (s, v) => s with { CoastDepth = F(v) }),
        new("Water", "Lakes", "", 0, 12, 1, s => s.LakeHectares?.Length ?? 0, (s, v) => s with { LakeHectares = Lakes(I(v), s.LakeHectares is { Length: > 0 } l ? l.Max() : 5) }),
        new("Water", "Biggest lake", "ha", 0.5, 300, 0.5, s => s.LakeHectares is { Length: > 0 } l ? l.Max() : 5, (s, v) => s with { LakeHectares = Lakes(s.LakeHectares?.Length ?? 0, v) }),
        new("Water", "Rivers", "", 0, 8, 1, s => s.Rivers, (s, v) => s with { Rivers = I(v) }, "Rivers running downhill from high ground into a lake, the sea or off the edge."),
        new("Water", "River width", "m", 2, 30, 1, s => s.RiverWidth, (s, v) => s with { RiverWidth = F(v) }),
        new("Water", "Shore", "m", 0, 20, 1, s => s.Shore, (s, v) => s with { Shore = F(v) }, "The sand along shores (0: none)."),
        new("Water", "Marsh below", "m", 0, 60, 1, s => s.Marsh, (s, v) => s with { Marsh = F(v) }, "Ground lower than this flattened into wet flats (0: no marsh)."),
        new("Water", "Marsh pools", "%", 0, 100, 5, s => s.MarshShare * 100, (s, v) => s with { MarshShare = F(v / 100) }),
        new("Woods & plants", "Wooded", "%", 0, 100, 1, s => s.Woods * 100, (s, v) => s with { Woods = F(v / 100) }),
        new("Woods & plants", "Trees in the woods", "per ha", 0, 80, 1, s => s.WoodDensity, (s, v) => s with { WoodDensity = F(v) }),
        new("Woods & plants", "Trees in the open", "per ha", 0, 5, 0.1, s => s.OpenTrees, (s, v) => s with { OpenTrees = F(v) }),
        new("Woods & plants", "Tree size", "×", 0.3, 4, 0.1, s => s.TreeScale, (s, v) => s with { TreeScale = F(v) }),
        new("Woods & plants", "Giant trees", "%", 0, 100, 1, s => s.Giants * 100, (s, v) => s with { Giants = F(v / 100) }),
        new("Woods & plants", "Giants' size", "×", 1, 6, 0.1, s => s.GiantScale, (s, v) => s with { GiantScale = F(v) }),
        new("Woods & plants", "Bushes and plants", "per ha", 0, 150, 1, s => s.Small, (s, v) => s with { Small = F(v) }),
        new("Paint", "Rock from", "°", 10, 70, 1, s => s.RockSlope, (s, v) => s with { RockSlope = F(v) }, "Ground steeper than this is painted rock."),
        new("Paint", "Open dirt", "%", 0, 100, 1, s => s.OpenDirt * 100, (s, v) => s with { OpenDirt = F(v / 100) }, "The share of the open ground patched with dirt."),
        new("Paint", "Snow from", "m", 0, 1500, 10, s => s.SnowLine, (s, v) => s with { SnowLine = F(v) }, "Snow above this height (0: none)."),
        new("Camps & roads", "Camps", "", 0, 40, 1, s => s.Camps, (s, v) => s with { Camps = I(v) }),
        new("Camps & roads", "Buildings each", "", 1, 12, 1, s => s.CampSize, (s, v) => s with { CampSize = I(v) }),
        new("Extras", "Floating islands", "", 0, 80, 1, s => s.Floaters, (s, v) => s with { Floaters = I(v) }, "Lumps of the map's ground hanging in the air, planted on top (not in the preview)."),
    ];
}
