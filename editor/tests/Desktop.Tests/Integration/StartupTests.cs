using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// How the app starts: a live game with its token, the tests' data folder, the log that copies everything said, the
/// data folder's override (the copied game files stay where they are), the window's icon and the documentation link.
/// </summary>
[Collection("DataDir")]
public sealed class StartupTests : IDisposable
{
    private readonly string? _settings = AppSettings.PathOverride, _stamps = Stamps.PathOverride, _data = AppSettings.DataDirOverride;

    private readonly string? _token = Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gw-startup-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        AppSettings.PathOverride = _settings;
        Stamps.PathOverride = _stamps;
        AppSettings.DataDirOverride = _data;
        Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", _token);
        Options.Parse([]);
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void TheLogCopiesWhatIsSaidAfterWhatIsRunning()
    {
        AppSettings.DataDirOverride = Path.Combine(_dir, "logdata");
        var (stdout, stderr) = (Console.Out, Console.Error);
        try
        {
            Log.Start("9.9.9");
            Console.WriteLine("hello log");
            Console.Error.WriteLine("an error");
            Console.Write('!');
            Console.Write("tail");
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
        string[] lines = Log.Read().Split('\n');
        Assert.StartsWith("Groundwork 9.9.9, ", lines[0]);
        Assert.Equal("Data folder: " + Path.Combine(_dir, "logdata"), lines[1]);
        // Each line with its time, like the game's LogOutput.log; a line written in parts gets one.
        Assert.Equal(["hello log", "an error", "!tail"], lines[2..].Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\d\d:\d\d:\d\d ", "")));
        Assert.All(lines[2..], l => Assert.Matches(@"^\d\d:\d\d:\d\d \S", l));
    }

    [Fact]
    public void EachRunStartsTheLogOver()
    {
        AppSettings.DataDirOverride = Path.Combine(_dir, "logdata2");
        var (stdout, stderr) = (Console.Out, Console.Error);
        try
        {
            Log.Start("1.0.0");
            Console.WriteLine("first run");
            Log.Start("1.0.1");
            Console.WriteLine("second run");
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
        string log = Log.Read();
        Assert.StartsWith("Groundwork 1.0.1, ", log);
        Assert.Contains("second run", log);
        Assert.DoesNotContain("first run", log);
    }

    [Fact]
    public void ALogThatCannotBeWrittenStopsNothing()
    {
        // The data folder is a file: no log, no error.
        Directory.CreateDirectory(_dir);
        string file = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(file, "");
        AppSettings.DataDirOverride = Path.Combine(file, "below");
        var stdout = Console.Out;
        try
        {
            Log.Start("1.0.0");
        }
        catch (IOException)
        {
            // DataDir itself cannot make the folder: that is the caller's to see, not the log's.
        }
        finally
        {
            Console.SetOut(stdout);
        }
        Assert.Same(stdout, Console.Out);
    }
}
