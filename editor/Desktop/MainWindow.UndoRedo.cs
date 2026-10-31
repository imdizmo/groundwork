namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Ctrl+Shift+Z: does the last step taken back again.</summary>
    internal Task Redo()
    {
        View.SelectTool.Commit();
        _ = Session?.Redo();
        UpdateSaveBar();
        return Task.CompletedTask;
    }
}
