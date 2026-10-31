using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The command line's options as the app reads them: a world folder, an option and its missing value, the game data folder
/// that cannot be read, an area that opens straight in the editor, and the window's size.
/// </summary>
[Collection("DataDir")]
public sealed class StartupOptionsTests
{
    [Fact]
    public void AnOptionGivenLastWithoutItsValueIsAnError()
    {
        foreach (string option in new[] { "--data", "--window" })
        {
            var e = Assert.Throws<ArgumentException>(() => Options.Parse(["/worlds/Midgard", option]));
            Assert.Equal($"{option} needs a value after it.", e.Message);
        }
    }

    [Fact]
    public void TheWindowsSizeCanBeGivenWithinLimits()
    {
        Options.Parse(["--window", "1440x900"]);
        Assert.Equal((1440, 900), (Options.WindowWidth, Options.WindowHeight));
        Options.Parse(["--window", "10x99999"]);
        Assert.Equal((640, 4320), (Options.WindowWidth, Options.WindowHeight));
    }
}
