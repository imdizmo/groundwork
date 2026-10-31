using System.Collections.Generic;
using System.Numerics;

namespace Groundwork.Sdk;

/// <summary>
/// The area open in the editor's 3D view, as a scene provider reads it (<see cref="ISceneProvider.Build"/>): its
/// bounds, its objects (with the changes not saved yet), and its ground.
/// </summary>
public interface IAreaView
{
    /// <summary>The area's west edge, world x in metres.</summary>
    float MinX { get; }

    /// <summary>The area's south edge, world z in metres.</summary>
    float MinZ { get; }

    /// <summary>The area's east edge, world x in metres.</summary>
    float MaxX { get; }

    /// <summary>The area's north edge, world z in metres.</summary>
    float MaxZ { get; }

    /// <summary>
    /// The objects of an asset in the area, as they are with the changes not saved yet (deleted ones left out, added
    /// ones in); in no particular order.
    /// </summary>
    /// <param name="asset">The asset's name.</param>
    IReadOnlyList<IObjectView> ObjectsOf(string asset);

    /// <summary>
    /// The ground's height at a point (its heightmap with the edits, between the points a metre apart along the
    /// ground's triangles); the nearest edge's height outside the area.
    /// </summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    float GroundHeight(float x, float z);

    /// <summary>The map's layer a point mostly is (0: the first, the ground everywhere; 1 to 3: a painted one).</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    int Layer(float x, float z);

    /// <summary>
    /// The ground's colour at a ground point a whole metre, as the editor draws it (the map's layers blended by their
    /// paint; linear red, green, blue from 0 to 1): what <see cref="SceneMesh.Colors"/> takes.
    /// </summary>
    /// <param name="x">World x, a whole metre.</param>
    /// <param name="z">World z, a whole metre.</param>
    Vector3 GroundColor(int x, int z);

    /// <summary>
    /// The painted layers' weights at a ground point a whole metre: red the second layer, green the third, blue the
    /// fourth (0 to 255 each), red in the lowest byte.
    /// </summary>
    /// <param name="x">World x, a whole metre.</param>
    /// <param name="z">World z, a whole metre.</param>
    uint GroundMask(int x, int z);
}
