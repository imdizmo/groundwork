namespace Groundwork.Desktop;

/// <summary>
/// Command line options.
/// <code>
///   --driver                                           driven by another program (see Driver)
///   --window &lt;width&gt;x&lt;height&gt;                          the window's size (the documentation's pictures)
///   --data &lt;folder&gt;                                    settings and memory kept there (tests)
///   --mcp-stdio                                        no window: relays Claude's MCP messages on stdin/stdout to
///                                                      the running editor (Claude Desktop; see ClaudeRelay)
///   --script-host                                      no window: runs one script for the editor that started
///                                                      this process (see ScriptHost.Serve)
/// </code>
/// </summary>
public static class Options
{
    /// <summary>Driven by another program, one command per line (see Driver).</summary>
    public static bool Driver { get; private set; }
    /// <summary>--data &lt;folder&gt;: settings and stamps go there instead of the user's (the visual tests).</summary>
    public static string? Data { get; private set; }

    /// <summary>--window: the window's width (0: the usual).</summary>
    public static int WindowWidth { get; private set; }
    /// <summary>--window: the window's height (0: the usual).</summary>
    public static int WindowHeight { get; private set; }

    /// <summary>Writes a line on the console (the log takes it too once started).</summary>
    public static void Say(string line) => Console.WriteLine(line);

    /// <summary>
    /// The value after the option at <paramref name="i"/> (moved past it); an option given last, without its value, is a
    /// command-line error.
    /// </summary>
    private static string Value(string[] args, ref int i) =>
        ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value after it.");

    /// <summary>
    /// Reads the command line, starting from the defaults each time (the tests parse several in one process). --data
    /// also points the settings and stamps there.
    /// </summary>
    public static void Parse(string[] args)
    {
        Data = null;
        Driver = false;
        WindowWidth = WindowHeight = 0;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--driver":
                    Driver = true;
                    break;
                case "--data":
                    Data = Path.GetFullPath(Value(args, ref i));
                    try
                    {
                        Directory.CreateDirectory(Data);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Said like any other option that cannot be used, before the editor starts.
                        throw new ArgumentException($"--data {Data} cannot be used: {ex.Message}", ex);
                    }
                    Groundwork.App.AppSettings.PathOverride = Path.Combine(Data, "settings.json");
                    Stamps.PathOverride = Path.Combine(Data, "stamps.json");
                    Groundwork.App.AppSettings.DataDirOverride = Data;
                    break;
                case "--window":
                {
                    string[] wh = Value(args, ref i).Split('x');
                    WindowWidth = Math.Clamp(int.Parse(wh[0]), 640, 7680);
                    WindowHeight = Math.Clamp(int.Parse(wh[1]), 480, 4320);
                    break;
                }
            }
        }
    }
}
