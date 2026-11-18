
namespace Groundwork.Desktop;

/// <summary>
/// --driver: the editor driven by another program (the visual tests), one command per line on its
/// input, one answer per line on its output ("@@ ok &lt;json&gt;" or "@@ error &lt;message&gt;"), so a single
/// editor serves many tests. Commands:
/// </summary>
/// <remarks>
/// <code>
///    stroke &lt;tool&gt; &lt;x&gt; &lt;z&gt; &lt;steps&gt;  a brush stroke at world x, z (tool: raise, lower, smooth...)
///    picture &lt;file.png&gt;             a picture of the 3D view, once it is drawn
///    shot &lt;file.png&gt;                a picture of the whole window, panels and view (documentation)
///    look &lt;models|seethrough|block|names&gt; &lt;on|off&gt;, look res &lt;sharp|balanced|fast&gt;
///                                   the View panel's Look switches
///    camera &lt;x&gt; &lt;z&gt; &lt;yaw°&gt; &lt;pitch°&gt; &lt;distance&gt; [y]  the 3D view's camera on world x, z (at height y:
///                                               inside a dungeon)
///    camera reset                   the camera's mode, turn, tilt and distance as the editor starts with them
///    click &lt;text&gt;                   the visible button, switch or box labelled so, or whose words
///                                   begin so (windows and dialogs)
///    choose &lt;text&gt;                  the entry so named in whichever visible list (drop-down or list box) has it
///    type &lt;hint&gt;|&lt;text&gt;             the visible text box whose placeholder contains hint gets text
///    set &lt;label&gt;|&lt;value&gt;            the slider, text box(es: a,b) or list beside the label so worded
///    panel &lt;view|history|help|none&gt; the right-hand panel shown
///    message [text]                 the status bar's message (none: cleared)
///    wait &lt;ms&gt;                      a pause (a brush stroke works frame by frame while held)
///    bench &lt;seconds&gt;                the 3D camera turns on its own; the frame rates come back
///    mouse &lt;down|move|up&gt; &lt;x&gt; &lt;z&gt; [left|right|middle] [shift|ctrl|alt ...]
///                                   the mouse at world x, z over the 3D view (real pointer events)
///    wheel &lt;x&gt; &lt;z&gt; &lt;steps&gt;          the wheel there (positive: zoom in)
///    key &lt;name&gt; [shift|ctrl|alt ...] a key pressed in the window (Avalonia key names: E, Escape, Enter, D1...)
///    map [folder]                   a map opened (the last one, or the one in that folder); how long it took
///    unreal link &lt;project folder&gt;  the open map linked to that Unreal project (map.json's unreal.project)
///    unreal landscape|paint|models|objects  into the open Unreal level: the landscape made (and painted, its water
///                                   made), or only painted and its water made, the models it lacks imported,
///                                   every object placed; Unreal's answer
///    mappicture &lt;file.png&gt; [colour|parchment|engraving|tilted] [pixels]  the open map's picture from above (⋯ → Save a picture of the map)
///    replay &lt;file&gt;[|&lt;file&gt;...]|team all|&lt;n&gt;|seek &lt;s&gt;|close  recorded play sessions over the open map
///                                   (Replay; team: the team server's sessions of the map, all or the n-th newest)
///    docs &lt;page[#anchor]&gt;            the Help window on that place of the documentation ("map.md#controls")
///    docs search &lt;words&gt;            the Help window's search: {"hits": what it found}
///    docs shot &lt;file.png&gt;            a picture of the Help window
///    docs close                     the Help window closed
///    state                          what is shown, the objects, what is pending, frames drawn
///    quit
/// </code>
/// Also spirv (SPIRV-Cross loads), mouse3 (a point in the air), inspect &lt;prefab&gt; and check &lt;label&gt;|on|off.
/// </remarks>
public static partial class Driver
{
    /// <summary>
    /// How long a picture may wait for the view to be ready: the world map's first build on a slow computer (CI's
    /// two cores) takes more than a minute. GROUNDWORK_PICTURE_WAIT (seconds) gives longer: the tests under coverage, which
    /// measures this app too.
    /// </summary>
    private static readonly TimeSpan PictureWait = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("GROUNDWORK_PICTURE_WAIT"), out int s) && s > 0 ? s : 180);

    /// <summary>The View panel's 3D resolution choices, in their list's order.</summary>
    private static readonly string[] Resolutions = ["sharp", "balanced", "fast"];

    /// <summary>The handler of one command: the window and the command's words and arguments.</summary>
    private delegate Task<string> Handler(Call call);

    /// <summary>
    /// One command as given: the window, the command's words (the first is the command) and its arguments.
    /// </summary>
    internal sealed record Call(MainWindow W, string[] A, string[] Args)
    {
        /// <summary>The argument at this position as a number (invariant culture).</summary>
        public float F(int i) => float.Parse(Args[i], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Each command's handler, by its name (the table's order follows the class summary).</summary>
    private static readonly Dictionary<string, Handler> Commands = new()
    {
        ["stroke"] = CmdStroke,
        ["mouse"] = CmdMouse,
        ["mouse3"] = CmdMouse3,
        ["wheel"] = CmdWheel,
        ["key"] = CmdKey,
        ["picture"] = CmdPicture,
        ["shot"] = CmdShot,
        ["look"] = CmdLook,
        ["type"] = CmdType,
        ["set"] = CmdSet,
        ["panel"] = CmdPanel,
        ["message"] = CmdMessage,
        ["wait"] = CmdWait,
        ["camera"] = CmdCamera,
        ["click"] = CmdClick,
        ["check"] = CmdCheck,
        ["choose"] = CmdChoose,
        ["bench"] = CmdBench,
        ["map"] = CmdMap,
        ["mappicture"] = CmdMapPicture,
        ["unreal"] = CmdUnreal,
        ["replay"] = CmdReplay,
        ["bounds"] = CmdBounds,
        ["docs"] = CmdDocs,
        ["state"] = CmdState,
        ["quit"] = CmdState,
    };

    /// <summary>Runs one command; its answer's JSON (usually <see cref="State"/>). Throws for an unknown or failed command.</summary>
    internal static async Task<string> Run(MainWindow w, string cmd)
    {
        string[] a = cmd.Split(' ', 2);
        string[] args = a.Length > 1 ? a[1].Split(' ') : [];
        if (!Commands.TryGetValue(a[0], out var handler))
        {
            throw new InvalidOperationException("unknown command " + a[0]);
        }
        return await handler(new Call(w, a, args));
    }

    /// <summary>A command's argument split at its first '|' (type, set, check).</summary>
    /// <param name="a">The command and its argument.</param>
    /// <param name="usage">How the command is written, for the message.</param>
    /// <exception cref="InvalidOperationException">There is no argument or it has no '|'.</exception>
    internal static (string Before, string After) Bar(string[] a, string usage)
    {
        int bar = a.Length > 1 ? a[1].IndexOf('|') : -1;
        return bar >= 0 ? (a[1][..bar], a[1][(bar + 1)..]) : throw new InvalidOperationException("usage: " + usage);
    }
}
