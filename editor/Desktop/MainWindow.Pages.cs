namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The start page: the maps to open.
    /// </summary>
    /// <param name="error">Shown on a newly made start page; null to keep the page made before.</param>
    internal void ShowStart(string? error = null)
    {
        if (StartPage == null || error != null)
        {
            StartPage = new StartPage(error, () => Settings.Maps);
            StartPage.MapRequested += async folder => await OpenMap(folder);
            StartPage.OpenFolderRequested += async () =>
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = Groundwork.App.Lang.T("start.label.openMap") });
                if (picked.Count > 0 && Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(picked[0]) is string folder)
                {
                    await OpenMap(folder);
                }
            };
            StartPage.NewMapRequested += async () => await NewMap();
            StartPage.OpenUrl = Launcher.LaunchUriAsync;
            StartPage.SettingsRequested += async () =>
            {
                if (await SettingsDialog.Show(this, Settings))
                {
                    // Claude's connection, as the settings now say.
                    await ClaudeServer.Apply(Settings, this);
                }
            };
        }
        Title = $"Groundwork {BuildInfo.Version}";
        _pages.Content = StartPage.View;
    }

    /// <summary>Whether the busy veil is up.</summary>
    internal bool IsBusy => _busy.IsVisible;
}
