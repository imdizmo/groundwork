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
            StartPage.HeightmapDropped += async path => await NewMap(path);
            StartPage.RemoveRequested += folder =>
            {
                _ = Settings.Maps.Remove(folder);
                SaveSettings(Settings);
                StartPage.ShowMaps();
            };
            StartPage.DuplicateRequested += async folder => await DuplicateMap(folder);
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

    /// <summary>
    /// A copy of a map beside it ("Name copy"), listed after it: the folder copied whole (symbolic links left out, as
    /// a map's files never follow them), the copy's name said in its map.json.
    /// </summary>
    private async Task DuplicateMap(string folder)
    {
        string parent = Path.GetDirectoryName(folder) ?? folder, leaf = Path.GetFileName(folder);
        string copy = Path.Combine(parent, leaf + " copy");
        for (int n = 2; Directory.Exists(copy) || File.Exists(copy); n++)
        {
            copy = Path.Combine(parent, $"{leaf} copy {n}");
        }
        _busyText.Text = Groundwork.App.Lang.T("start.duplicating", leaf);
        _busy.IsVisible = true;
        try
        {
            await Task.Run(() =>
            {
                CopyTree(new DirectoryInfo(folder), new DirectoryInfo(copy));
                var m = MapProject.Open(copy);
                m.Name += " copy";
                m.WriteJson();
            });
            int after = Settings.Maps.IndexOf(folder);
            Settings.Maps.Insert(after < 0 ? 0 : after + 1, copy);
            SaveSettings(Settings);
            StartPage?.ShowMaps();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ShowError(ex);
        }
        finally
        {
            _busy.IsVisible = false;
        }
    }

    private static void CopyTree(DirectoryInfo from, DirectoryInfo to)
    {
        to.Create();
        foreach (var f in from.EnumerateFiles().Where(f => f.LinkTarget == null))
        {
            _ = f.CopyTo(Path.Combine(to.FullName, f.Name));
        }
        foreach (var d in from.EnumerateDirectories().Where(d => d.LinkTarget == null))
        {
            CopyTree(d, new DirectoryInfo(Path.Combine(to.FullName, d.Name)));
        }
    }

    /// <summary>Whether the busy veil is up.</summary>
    internal bool IsBusy => _busy.IsVisible;
}
