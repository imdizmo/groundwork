using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A mod's own tool in the window (ModExtensions.Tools): on the rail once registered, its card shown while it is chosen,
/// told when it is chosen and left, its card's buttons reaching it, and its edits one step of the area's history. Runs
/// alone (the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowModToolTests : IDisposable
{
    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

}
