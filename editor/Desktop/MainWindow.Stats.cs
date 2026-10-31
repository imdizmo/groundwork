namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The window is open: with <paramref name="load"/>, Claude's connection applied and the models read; then the
    /// start page shows, and the test driver starts when asked.
    /// </summary>
    /// <param name="load">False (tests): nothing is loaded.</param>
    private async Task OnOpenedAsync(bool load)
    {
        if (!load)
        {
            return;
        }
        Options.Say("window open");
        // Claude's connection, when chosen in Settings.
        await ClaudeServer.Apply(Settings, this);
        // The last map's models are opened while the start page shows (most often it is opened again).
        MapProject.Current ??= LastMap();
        if (!Options.Driver && MapProject.Current != null)
        {
            _modelsFolder = ModelStore.MapModels;
            _modelsLoading = Task.Run(ModelStore.Open);
        }
        ShowStart();
        if (Options.Driver)
        {
            Driver.Start(this);
        }
    }
}
