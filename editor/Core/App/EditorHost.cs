using Groundwork.Sdk;

namespace Groundwork.App;

/// <summary>What an extension asks of the editor (IEditorHost).</summary>
internal sealed class EditorHost : IEditorHost
{
    /// <inheritdoc/>
    public string DataFolder => AppSettings.DataDir;

    /// <inheritdoc/>
    public void Redraw() => ModExtensions.Redraw();
}
