using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The map tools' letters: one each, and none another key already takes.</summary>
public class ToolLettersTests
{
    [Fact]
    public void EachLetterPicksOneToolAndNoneIsTakenElsewhere()
    {
        string[] ids = ["object", "brush", "biome", "stamp", "shape", "mountain", "river", "path", "areas", "names"];
        var letters = ids.Select(ToolPanel.OwnKey).ToList();
        Assert.All(letters, Assert.NotNull);
        Assert.Equal(letters.Count, letters.Distinct().Count());
        // the camera's, the panels', the other tools' and placing's keys
        Assert.Empty(letters.Intersect(["W", "A", "S", "D", "C", "F", "R", "V", "L", "E", "M", "O", "T", "H"]));
        Assert.Null(ToolPanel.OwnKey("notes"));
    }
}
