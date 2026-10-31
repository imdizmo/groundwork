using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Claude tools' placements: a lone coordinate is refused.
/// </summary>
[Collection("World files")]
public class ClaudeToolsPlacementTests
{
    // A point's coordinates come both or neither: a lone one was dropped (list_objects' near) or mixed with a default
    // (move_objects' pivot with the objects' middle z).
    [Fact]
    public void ALoneCoordinateIsRefused()
    {
        Assert.Null(ClaudeTools.LoneCoordinate(null, null));
        Assert.Null(ClaudeTools.LoneCoordinate(1, 2));
        Assert.Equal("Give x and z together, or neither: only x was given.", ClaudeTools.LoneCoordinate(1, null));
        Assert.Equal("Give pivotX and pivotZ together, or neither: only pivotZ was given.", ClaudeTools.LoneCoordinate(null, 2, "pivotX", "pivotZ"));
    }
}
