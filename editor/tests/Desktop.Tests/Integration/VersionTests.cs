using System.Text.RegularExpressions;
using Groundwork.Tests;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// One version for the editor, the plugin and the packages: the VERSION file.
/// </summary>
public class VersionTests
{
    private static readonly string Repo = Path.GetFullPath(Path.Combine(Fixtures.Editor, ".."));

    /// <summary>
    /// The VERSION file's text.
    /// </summary>
    private static string Version => File.ReadAllText(Path.Combine(Repo, "VERSION")).Trim();

    [Fact]
    public void VersionIsMajorMinorPatch() => Assert.Matches(@"^\d+\.\d+\.\d+$", Version);

    [Fact]
    public void TheEditorHasTheVersionOfTheVersionFile() => Assert.Equal(Version, typeof(MainWindow).Assembly.GetName().Version!.ToString(3));

    [Fact]
    public void TheChangelogHasASectionForIt()
    {
        Assert.Matches(new Regex($@"^## v{Regex.Escape(Version)}\b", RegexOptions.Multiline, TimeSpan.FromSeconds(1)), File.ReadAllText(Path.Combine(Repo, "CHANGELOG.md")));
    }
}
