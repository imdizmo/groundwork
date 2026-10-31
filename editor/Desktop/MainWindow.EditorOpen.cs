namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>A map was opened: the history is its own now (an open panel showed the last one's).</summary>
    private void EditorOpened() => History.Refresh();
}
