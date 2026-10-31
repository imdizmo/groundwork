using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Claude's tools for objects already there (ClaudeTools.Objects): the angles they are given brought within a turn.
/// </summary>
[Collection("World files")]
public class ClaudeToolsObjectsTests
{

    // Wrapped: each angle brought within 0 to 360 degrees, either way round.
    [Fact]
    public void AnglesAreBroughtWithinATurn()
    {
        Assert.Equal(new Vector3(0, 23, 350), ClaudeTools.Wrapped(new Vector3(360, 383, -10)));
        Assert.Equal(new Vector3(90, 0, 180), ClaudeTools.Wrapped(new Vector3(90, -720, 540)));
    }
}
