using System.Collections.Generic;
using System.Numerics;

namespace Groundwork.Sdk;

/// <summary>
/// An object of the map as the editor has it: its asset, its place, and the data plugins keep on it (text by key,
/// saved with the map's objects). Positions are in metres, x east, y up, z north; turns are Euler angles in degrees
/// (applied z, then x, then y).
/// </summary>
public interface IObjectView
{
    /// <summary>Its asset's name ("" when the editor does not know it).</summary>
    string Asset { get; }

    /// <summary>Where it is.</summary>
    Vector3 Position { get; }

    /// <summary>Its turn, Euler degrees.</summary>
    Vector3 Rotation { get; }

    /// <summary>Its turn as a quaternion (the Euler angles applied z, then x, then y).</summary>
    Quaternion Orientation { get; }

    /// <summary>Its size, the same along every axis (1: the asset's own).</summary>
    float Scale { get; }

    /// <summary>The data plugins keep on it, by key (empty: none).</summary>
    IReadOnlyDictionary<string, string> Data { get; }
}
