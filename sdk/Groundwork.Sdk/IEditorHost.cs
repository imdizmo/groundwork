namespace Groundwork.Sdk;

/// <summary>What a plugin can ask of the editor at any time (see <see cref="IPluginRegistry.Host"/>).</summary>
public interface IEditorHost
{
    /// <summary>The editor's data folder (its settings and caches): a plugin's own files go in a folder of its own there.</summary>
    string DataFolder { get; }

    /// <summary>
    /// Asks for the plugins' shapes and ground cuts to be made again. Cheap to call; the work happens at the next frame.
    /// </summary>
    void Redraw();
}
