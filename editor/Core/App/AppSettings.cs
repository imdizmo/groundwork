using System.Text.Json.Serialization;

namespace Groundwork.App;

/// <summary>
/// What the app remembers between runs, per user: settings.json in the app's data folder
/// (~/.local/share/Groundwork on Linux, %LOCALAPPDATA%\Groundwork on Windows).
/// </summary>
public sealed partial class AppSettings
{

    /// <summary>
    /// The mods' editor extensions allowed to run (ModExtensions): each DLL's full path and the SHA-256 of the file as
    /// it was allowed. A changed file runs only once allowed again: it is other code.
    /// </summary>
    public Dictionary<string, string> Extensions { get; set; } = [];

    /// <summary>The map projects opened (their folders), the last first: the start page's list.</summary>
    public List<string> Maps { get; set; } = [];

    /// <summary>
    /// The scripts the user let a map run (Send to Unreal), as "map folder|script|SHA-256 of the script": a map.json
    /// from elsewhere runs nothing until the user says yes to its script, and a changed script is asked about again.
    /// </summary>
    public List<string> TrustedScripts { get; set; } = [];

    /// <summary>Where New map puts maps; null: Documents/Groundwork.</summary>
    public string? MapsFolder { get; set; }

    /// <summary>
    /// The language the app shows (Settings): a catalogue's code ("en", "fr"…, see <c>Lang</c>); null: the computer's
    /// language when there is a catalogue of it, else English. Taken when the app starts.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Claude's connection (Settings): the editor's MCP server, on <c>ClaudeAddress</c> (127.0.0.1 unless chosen) at
    /// <c>ClaudePort</c>, for callers that send <c>ClaudeToken</c>. Off unless chosen.
    /// </summary>
    public bool ClaudeConnect { get; set; }

    /// <summary>
    /// Windows: draw with the graphics driver's own OpenGL first instead of Direct3D (ANGLE); taken when the editor
    /// starts.
    /// </summary>
    public bool DriverOpenGl { get; set; }

    /// <summary>
    /// The team server (host or host:port) chosen in Settings: null follows the Launcher's JATIFY_TEAM, empty is offline.
    /// </summary>
    public string? Team { get; set; }

    /// <summary>
    /// How many steps the editor's history keeps (Settings); 0 or less: no limit (only a memory budget).
    /// </summary>
    public int HistorySteps { get; set; }

    /// <summary>
    /// The local port the MCP server for Claude listens on.
    /// </summary>
    public int ClaudePort { get; set; } = 5731;

    /// <summary>
    /// The address the MCP server for Claude listens on: 127.0.0.1 (this computer only, the default), one of this
    /// computer's network addresses, or 0.0.0.0 (all of them), for a Claude on another computer. The token is asked
    /// for on every address.
    /// </summary>
    public string ClaudeAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// The token callers of the MCP server for Claude must send.
    /// </summary>
    public string? ClaudeToken { get; set; }

    /// <summary>
    /// The data folder: settings, plugins, stamps, the log (DataDirOverride: another folder, for
    /// the tests' editor). The copied game files stay in the user's (UserDataDir), read only by tests.
    /// </summary>
    public static string DataDir
    {
        get
        {
            if (DataDirOverride is string o)
            {
                Directory.CreateDirectory(o);
                return o;
            }
            return UserDataDir;
        }
    }

    /// <summary>
    /// Another data folder instead of <c>UserDataDir</c> (the tests' editor, set by the --data option); created when
    /// read.
    /// </summary>
    public static string? DataDirOverride { get; set; }

    /// <summary>
    /// The user's own data folder (created if missing): %LOCALAPPDATA%\Groundwork on Windows (the variable, as
    /// the tests' pretend homes set it; .NET's LocalApplicationData comes from the shell and ignores it), else
    /// LocalApplicationData/Groundwork, or ~/.local/share when that is not set.
    /// </summary>
    public static string UserDataDir
    {
        get
        {
            string root = OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local
                ? local
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }
            string dir = Path.Combine(root, "Groundwork");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Tests: another file, so they never touch the user's settings.
    /// </summary>
    public static string? PathOverride { get; set; }

    /// <summary>
    /// settings.json in <c>DataDir</c>, or <c>PathOverride</c>.
    /// </summary>
    private static string FilePath => PathOverride ?? Path.Combine(DataDir, "settings.json");

    /// <summary>
    /// Serialises reads and writes of the settings file.
    /// </summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// Whether the settings file was there but could not be read (locked by another program, no permission): these
    /// are the defaults for now, and <c>Save</c> leaves the file as it is rather than writing them over the user's
    /// settings.
    /// </summary>
    [JsonIgnore]
    public bool FileUnread { get; private init; }

    /// <summary>
    /// Whether there was no settings file when these were loaded: a file there when <c>Save</c> writes was put there
    /// by another program since (a sync program putting the user's back), and is left as it is. Cleared once these
    /// are written.
    /// </summary>
    private bool _fileMissing;

    /// <summary>
    /// These settings with the extensions the file gave as null made empty: the rest of the app reads them as a map.
    /// </summary>
    private AppSettings WithoutNulls()
    {
        Extensions ??= [];
        Maps ??= [];
        TrustedScripts ??= [];
        return this;
    }
}
