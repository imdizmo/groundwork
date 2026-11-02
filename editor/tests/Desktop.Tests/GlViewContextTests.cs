using System.Text.RegularExpressions;
using Groundwork.Tests;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The view's OpenGL names are all forgotten when its context goes (GlView.Context): a name kept into the new context
/// means another object there (the rivers' triangles were written into the ground's vertices: shards over the map).
/// </summary>
public partial class GlViewContextTests
{
    private static readonly string Desktop = Path.GetFullPath(Path.Combine(Fixtures.Editor, "Desktop"));

    [GeneratedRegex(@"private (?:readonly )?uint(?:\[\])? (_\w+(?:, _\w+)*)[;\s=]")]
    private static partial Regex NameFields();

    [GeneratedRegex(@"private readonly Dictionary<[^;=]*\buint\b[^;=]*> (_\w+)")]
    private static partial Regex NameDictionaries();

    [Fact]
    public void EveryOpenGlNameTheViewKeepsIsForgottenWithItsContext()
    {
        string source = string.Concat(Directory.EnumerateFiles(Desktop, "GlView*.cs").Select(File.ReadAllText));
        string context = File.ReadAllText(Path.Combine(Desktop, "GlView.Context.cs"));
        string deinit = context[context.IndexOf("OnOpenGlDeinit", StringComparison.Ordinal)..];
        var fields = NameFields().Matches(source).SelectMany(m => m.Groups[1].Value.Split(", ")).Distinct();
        var dictionaries = NameDictionaries().Matches(source).Select(m => m.Groups[1].Value).Distinct();
        var kept = fields.Where(f => !Regex.IsMatch(deinit, $@"\b{f}\b"))
            .Concat(dictionaries.Where(d => !deinit.Contains(d + ".Clear()", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal);
        Assert.Equal("", string.Join(" ", kept));
    }
}
