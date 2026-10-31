namespace Groundwork.Desktop;

/// <summary>
/// Where the documentation speaks of each part of the app: what F1 and the panels' help buttons (Ui.HelpButton) open
/// in the Help window. HelpTopicsTests checks that each one leads to a page and heading the app carries.
/// </summary>
public static class HelpTopics
{
    /// <summary>The start page: the help's first page, where getting started begins.</summary>
    public static HelpTarget StartPage { get; } = HelpTarget.Home;

    /// <summary>Map projects: map.json and the map's files.</summary>
    public static HelpTarget Map { get; } = new("map.md");

    /// <summary>The 3D editor's window: the top bar, the rail, the camera.</summary>
    public static HelpTarget Editor { get; } = new("editor-basics.md");

    /// <summary>The History panel.</summary>
    public static HelpTarget History { get; } = new("editor-basics.md", "history");

    /// <summary>The View panel.</summary>
    public static HelpTarget ViewPanel { get; } = new("editor-basics.md", "view-panel");

    /// <summary>The help card (the mouse and the keys).</summary>
    public static HelpTarget Controls { get; } = new("help/shortcuts.md");

    /// <summary>The Mask card.</summary>
    public static HelpTarget Mask { get; } = new("masks.md");

    /// <summary>Live and Send to Unreal.</summary>
    public static HelpTarget Live { get; } = new("unreal.md");

    /// <summary>Settings.</summary>
    public static HelpTarget Settings { get; } = new("editor-basics.md", "settings");

    /// <summary>A tool's page (a brush's part of the sculpt and paint page).</summary>
    /// <param name="mode">The tool.</param>
    /// <param name="brush">The brush, for <c>ToolMode.Brush</c>.</param>
    public static HelpTarget Tool(ToolMode mode, BrushTool? brush = null) => mode switch
    {
        ToolMode.Brush when brush is { } b && ToolPanel.IsPaint(b) => new("terrain.md", "paint-brushes"),
        ToolMode.Brush => new("terrain.md", "sculpt-brushes"),
        ToolMode.Select => new("select.md"),
        ToolMode.Measure => new("measure.md"),
        ToolMode.Paste => new("select.md", "copy-and-paste"),
        ToolMode.Mod => new("editor-basics.md", "the-tool-rail"),
        _ => new("editor-basics.md", "camera"),
    };

    /// <summary>Every place above, each tool's included (HelpTopicsTests).</summary>
    public static IEnumerable<HelpTarget> All =>
        [StartPage, Map, Editor, History, ViewPanel, Controls, Mask, Live, Settings,
            .. Enum.GetValues<ToolMode>().Select(m => Tool(m)), .. Enum.GetValues<BrushTool>().Select(b => Tool(ToolMode.Brush, b))];
}
