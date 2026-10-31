using Groundwork.App;

namespace Groundwork.Tests;

/// <summary>
/// Where the tests find the editor's sources (the editor folder, found up from the test binaries), and asset numbers.
/// </summary>
public static class Fixtures
{
    /// <summary>The editor folder (holding Core, Desktop and tests).</summary>
    public static string Editor
    {
        get
        {
            for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
            {
                foreach (string e in new[] { d, Path.Combine(d, "editor") })
                {
                    if (Directory.Exists(Path.Combine(e, "tests")) && Directory.Exists(Path.Combine(e, "Core")))
                    {
                        return e;
                    }
                }
            }
            throw new DirectoryNotFoundException("the editor folder (Core, tests) not found above " + AppContext.BaseDirectory);
        }
    }

    /// <summary>An asset's number (AssetIds), for objects the tests make.</summary>
    public static int Hash(string name) => AssetIds.Of(name);
}
