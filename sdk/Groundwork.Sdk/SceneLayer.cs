using System.Collections.Generic;
using System.Numerics;

namespace Groundwork.Sdk;

/// <summary>What a scene provider draws in an area: meshes, and holes in the editor's ground.</summary>
public sealed class SceneLayer
{
    /// <summary>The meshes.</summary>
    public IList<SceneMesh> Meshes { get; } = [];

    /// <summary>Where the editor's ground is not drawn (and clicks go through it): what the meshes stand in for.</summary>
    public IList<GroundHole> Holes { get; } = [];
}

/// <summary>
/// A mesh drawn in the area, in the world's frame (metres; x east, y up, z north), triangles wound clockwise seen from
/// the front as in Unity. Drawn lit like the ground, in its corners' <see cref="Colors"/> or in <see cref="Color"/>.
/// </summary>
public sealed class SceneMesh
{
    /// <summary>A mesh.</summary>
    /// <param name="positions">The corners, in world metres.</param>
    /// <param name="normals">Each corner's normal (unit length, towards the front).</param>
    /// <param name="triangles">Three corner indices a triangle.</param>
    public SceneMesh(Vector3[] positions, Vector3[] normals, int[] triangles)
    {
        Positions = positions;
        Normals = normals;
        Triangles = triangles;
    }

    /// <summary>The corners, in world metres.</summary>
    public Vector3[] Positions { get; }

    /// <summary>Each corner's normal (unit length, towards the front).</summary>
    public Vector3[] Normals { get; }

    /// <summary>Three corner indices a triangle, wound clockwise seen from the front.</summary>
    public int[] Triangles { get; }

    /// <summary>The colour drawn without <see cref="Colors"/> (red, green, blue, alpha from 0 to 1, sRGB; alpha is not used).</summary>
    public Vector4 Color { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>
    /// Each corner's colour (linear red, green, blue from 0 to 1; <see cref="IAreaView.GroundColor"/> gives the ground's),
    /// lit as the ground is. Null: drawn in <see cref="Color"/>.
    /// </summary>
    public Vector3[]? Colors { get; set; }

    /// <summary>Whether clicks land on it (true), or go through it.</summary>
    public bool Pickable { get; set; } = true;
}

/// <summary>
/// A rectangle of the editor's ground left undrawn, in whole metres of the world: x from <see cref="X0"/> to
/// <see cref="X1"/>, z from <see cref="Z0"/> to <see cref="Z1"/> (the far edges not included).
/// </summary>
public sealed class GroundHole
{
    /// <summary>A hole.</summary>
    /// <param name="x0">West edge, world x in whole metres.</param>
    /// <param name="z0">South edge, world z in whole metres.</param>
    /// <param name="x1">East edge, past the hole.</param>
    /// <param name="z1">North edge, past the hole.</param>
    public GroundHole(int x0, int z0, int x1, int z1)
    {
        X0 = x0;
        Z0 = z0;
        X1 = x1;
        Z1 = z1;
    }

    /// <summary>West edge, world x in whole metres.</summary>
    public int X0 { get; }

    /// <summary>South edge, world z in whole metres.</summary>
    public int Z0 { get; }

    /// <summary>East edge, past the hole.</summary>
    public int X1 { get; }

    /// <summary>North edge, past the hole.</summary>
    public int Z1 { get; }
}
