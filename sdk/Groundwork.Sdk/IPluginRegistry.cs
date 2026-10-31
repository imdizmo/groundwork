namespace Groundwork.Sdk;

/// <summary>
/// Where a plugin registers its hooks, during <see cref="IGroundworkPlugin.Register"/>. Assets are named as the
/// map's objects name them. Hooks are called from the editor's background threads as well as its window's, possibly at
/// once: they must not keep state between calls (or must lock it), and must be quick. A hook that throws turns its
/// plugin off until the editor starts again.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>The API version of the editor loading the plugin (at least the one it was built against).</summary>
    int ApiVersion { get; }

    /// <summary>The editor's version ("1.33.0").</summary>
    string EditorVersion { get; }

    /// <summary>
    /// Draws more of an object than its asset's model: shapes the plugin builds from the object's data (a sail, a
    /// rope to another object).
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="assets">The assets it draws for.</param>
    void AddShapes(IShapeProvider provider, params string[] assets);

    /// <summary>
    /// Leaves the ground undrawn where an object opens it: the mouth of a tunnel, a pit.
    /// </summary>
    /// <param name="cutter">The cutter.</param>
    /// <param name="assets">The assets it cuts for.</param>
    void AddGroundCuts(IGroundCutter cutter, params string[] assets);

    /// <summary>
    /// Draws meshes over the area (a canal's trench, in colours or the ground's) and leaves the editor's ground undrawn
    /// where they stand in for it.
    /// </summary>
    /// <param name="provider">The provider.</param>
    void AddScene(ISceneProvider provider);

    /// <summary>A tool on the editor's rail (its controls on a card, its clicks in the 3D view, its edits on the history).</summary>
    /// <param name="tool">The tool.</param>
    void AddTool(IEditorTool tool);

    /// <summary>The editor: its data folder, asking it to draw again.</summary>
    IEditorHost Host { get; }

    /// <summary>A line in the editor's log, under the plugin's name.</summary>
    /// <param name="message">The line.</param>
    void Log(string message);
}
