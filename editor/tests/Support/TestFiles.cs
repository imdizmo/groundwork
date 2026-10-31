namespace Groundwork.Tests;

/// <summary>
/// The files and folders Core keeps, pointed at the tests' own in both test projects.
/// </summary>
public static class TestFiles
{
    /// <summary>
    /// Before any test runs: the user's settings and data folder are never the tests'.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepTheUsersFiles()
    {
        Groundwork.App.AppSettings.PathOverride = Path.Combine(Path.GetTempPath(), $"gw-settings-{Environment.ProcessId}.json");
        // Logs, saved selections: a data folder of the tests', never the user's.
        Groundwork.App.AppSettings.DataDirOverride = Path.Combine(Path.GetTempPath(), $"gw-data-{Environment.ProcessId}");
    }
}
