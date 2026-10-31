using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The prompt Claude's client offers (ClaudePrompts): what to make, the guide for it, and the steps.
/// </summary>
public class ClaudePromptsTests
{
    [Fact]
    public void ShapingTheMap()
    {
        string prompt = ClaudePrompts.ShapeMap("a river valley");
        Assert.StartsWith("In Groundwork, change the open map: a river valley", prompt);
        Assert.Contains(ClaudeGuide.Shaping, prompt);
        Assert.Contains("map_river", prompt);
        Assert.Contains("I save it", prompt);
    }
}
