using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The bridge's answer to a ping, checked before Live starts (UnrealLive.Mismatch): the protocol this editor speaks
/// connects; an older or newer one, or something that is not the Groundwork Bridge, is said and refused.
/// </summary>
public sealed class UnrealLiveBridgeTests
{
    [Fact]
    public void OnlyTheBridgeOfThisProtocolConnects()
    {
        Assert.Null(UnrealLive.Mismatch("""{"bridge": "groundwork", "version": 1}"""));
        Assert.Contains("version 2", UnrealLive.Mismatch("""{"bridge": "groundwork", "version": 2}"""));
        Assert.Contains("not the Groundwork Bridge", UnrealLive.Mismatch("another server: ready"));
        Assert.Contains("not the Groundwork Bridge", UnrealLive.Mismatch("""{"bridge": "other", "version": 1}"""));
    }
}
