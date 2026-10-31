namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Save: writes the open map (not while busy).</summary>
    internal async Task Save()
    {
        if (Session != null && !IsBusy && InMap)
        {
            await SaveMap();
        }
    }
}
