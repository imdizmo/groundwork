using System.Numerics;
using Groundwork.App;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Scene objects made up for the tests.
/// </summary>
internal static class TestThings
{
    /// <summary>
    /// A rock 30 m up at the middle of the area, with the prefab Rock_4.
    /// </summary>
    internal static WorldScene.Thing Rock() => new(3, AssetIds.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
}
