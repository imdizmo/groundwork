using Markdig.Syntax;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Where F1 and the panels' help buttons lead: every place is a page and heading the app carries.</summary>
public class HelpTopicsTests
{
    [Fact]
    public void EveryTopicLeadsToAPageAndHeadingTheAppCarries()
    {
        var broken = HelpTopics.All.Distinct()
            .Where(t => !HelpDocs.Has(t.Page)
                || (t.Anchor != null && !HelpDocs.Read(t.Page).Descendants<HeadingBlock>().Any(h => HelpText.Id(h) == t.Anchor)))
            .Select(t => t.ToString());
        Assert.Empty(broken);
    }
}
