using System.Numerics;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The Objects panel's list: the kinds standing on the map, how many of each, found by what is typed.</summary>
public class ObjectsListTests
{
    private static WorldScene.Thing T(int id, string model, float x) => new(id, AssetIds.Of(model), new Vector3(x, 30, 0), Vector3.Zero, 1, false);

    [Fact]
    public void ItCountsEachKindStandingOnTheMap()
    {
        var s = TestScenes.Flat(1, T(1, "Pine_1", 0), T(2, "Pine_1", 5), T(3, "Rock_2", 9)).Scene;
        s.Things[1] = s.Things[1] with { Gone = true };
        var rows = MapTools.ObjectsTool.KindsOnMap(s, "");
        Assert.Equal([("Pine_1", 1), ("Rock_2", 1)], rows.Select(r => (r.Model, r.Count)).Order());
    }

    [Fact]
    public void WhatIsTypedFindsByNameWhateverTheCase()
    {
        var s = TestScenes.Flat(1, T(1, "Pine_1", 0), T(2, "Rock_2", 9)).Scene;
        var rows = MapTools.ObjectsTool.KindsOnMap(s, "pine");
        Assert.Equal("Pine_1", Assert.Single(rows).Model);
    }

    [Fact]
    public void ThingsOfAKindAreItsStandingObjects()
    {
        var s = TestScenes.Flat(1, T(1, "Pine_1", 0), T(2, "Rock_2", 9), T(3, "Pine_1", 5)).Scene;
        Assert.Equal([0, 2], MapTools.ObjectsTool.ThingsOf(s, AssetIds.Of("Pine_1")));
    }
}
