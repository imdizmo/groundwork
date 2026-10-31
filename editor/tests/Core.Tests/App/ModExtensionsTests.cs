using Groundwork.App;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// Mods' editor extensions (ModExtensions): offered by a worldeditor.json ("extension", inside the mod's folder only),
/// off until allowed, run once allowed (the SDK sample's DLL, loaded as a mod's would be), off again when the file
/// changes; a DLL that is no extension, or one needing a newer editor, not run, with why; a hook that throws turns its
/// extension off without failing the editor. Runs alone (the manifests and extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gw-extensions-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Forgets the manifests and extensions and deletes the folder.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }
}
