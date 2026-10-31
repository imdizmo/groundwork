using Groundwork.App;
using Xunit;

namespace Groundwork.Tests;

/// <summary>
/// The settings file (AppSettings): read and saved, a broken or held file kept aside or not saved over, a folder that cannot
/// be made, the recent worlds, and only the user able to read it.
/// </summary>
[Collection("DataDir")]
public class AppSettingsFileTests : AppSettingsTestBase
{
    [Fact]
    public void DataFolderFollowsTheEnvironment()
    {
        Assert.StartsWith(Data, AppSettings.DataDir);
    }

    // A save that fails is said in the log, without the settings file's path.
    [Fact]
    public void AFailedSaveIsLoggedWithoutItsPath()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            // A folder where the file goes: the write fails.
            Directory.CreateDirectory(path);
            using var log = Log.Listen();

            Assert.NotNull(new AppSettings().Save());
            string line = Assert.Single(log.Lines);
            // The file is AppSettings.cs, or one of its partial files (AppSettings.Writing.cs): only the name's start is fixed.
            Assert.Matches(@"^Caught at AppSettings(\.\w+)?\.cs:\d+ ", line);
            Assert.Contains("while saving settings.json (the settings stay in memory): ", line);
            Assert.DoesNotContain(Data, line);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    // A data folder that cannot be made (a file where it should be): the defaults, not saved, and neither the load nor
    // the save throws while saying so.
    [Fact]
    public void SettingsWhoseFolderCannotBeMadeAreDefaults()
    {
        string blocker = Path.Combine(Data, "not-a-folder");
        File.WriteAllText(blocker, "");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = null;
        AppSettings.DataDirOverride = Path.Combine(blocker, "data");
        try
        {
            var settings = AppSettings.Load();
            Assert.True(settings.FileUnread);
            settings.Save();
        }
        finally
        {
            AppSettings.PathOverride = old;
            AppSettings.DataDirOverride = null;
        }
    }

    // The settings hold Claude's token: only the user can read them, even over a file others could read.
    [Fact]
    public void OnlyTheUserCanReadTheSettings()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            File.WriteAllText(path, "{}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            new AppSettings { ClaudeToken = "secret" }.Save();
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal("secret", AppSettings.Load().ClaudeToken);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }
}
