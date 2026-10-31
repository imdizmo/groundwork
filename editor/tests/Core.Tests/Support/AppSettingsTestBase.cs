using Groundwork.App;

namespace Groundwork.Tests;

/// <summary>
/// The set-up the AppSettings tests share (AppSettingsFileTests, SavedServersTests, SettingsDiscoveryTests): the app's data
/// folder pointed at a temporary one (XDG_DATA_HOME, LOCALAPPDATA), put back and deleted after.
/// </summary>
public abstract class AppSettingsTestBase : IDisposable
{
    /// <summary>The temporary data folder the tests use.</summary>
    protected string Data { get; } = Path.Combine(Path.GetTempPath(), "gw-data-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _old = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    private readonly string? _oldDataDir;

    /// <summary>Points the data folder at this test's own temporary folder.</summary>
    protected AppSettingsTestBase()
    {
        Directory.CreateDirectory(Data);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Data);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Data);
        // The data folder from the environment here, not the tests' own.
        _oldDataDir = AppSettings.DataDirOverride;
        AppSettings.DataDirOverride = null;
    }

    /// <summary>Puts the data folder back and deletes the temporary one.</summary>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _old);
        AppSettings.DataDirOverride = _oldDataDir;
        if (Directory.Exists(Data))
        {
            Directory.Delete(Data, true);
        }
        GC.SuppressFinalize(this);
    }
}
