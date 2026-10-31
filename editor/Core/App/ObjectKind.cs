namespace Groundwork.App;

/// <summary>What an object is, for the View panel and picking: its asset's category in the map's catalog.</summary>
public enum ObjectKind
{
    /// <summary>Trees, stumps, logs.</summary>
    Trees,
    /// <summary>Rocks and boulders.</summary>
    Rocks,
    /// <summary>Bushes and shrubs.</summary>
    Bushes,
    /// <summary>Plants, flowers, grass.</summary>
    Plants,
    /// <summary>Props: crates, fences, things people made.</summary>
    Props,
    /// <summary>Buildings and their parts.</summary>
    Buildings,
    /// <summary>Markers: spawn points, triggers, what the game places at run time.</summary>
    Markers,
    /// <summary>Anything else.</summary>
    Other,
}
