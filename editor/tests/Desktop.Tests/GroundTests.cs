using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The area's ground (Ground) with No limit: what is past the game's ±8 m goes into the lift, and the limit counts from
/// the lifted ground; and where its grid starts in the world.
/// </summary>
public class GroundTests
{

    [Fact]
    public void TheGridStartsHalfAZoneBeforeTheFirstZonesMiddle()
    {
        var g = new Ground(65, 65, 2, -1, 1);
        Assert.Equal((96f, -96f), (g.OriginX, g.OriginZ));
    }
}
