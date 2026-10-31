using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>The editor's settings, loaded when the window is made.</summary>
    internal AppSettings Settings { get; } = AppSettings.Load();
    /// <summary>Writes the settings; tests keep them in memory, so no other window reads them.</summary>
    internal Action<AppSettings> SaveSettings { get; set; } = s => s.Save();
}
