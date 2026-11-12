using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Groundwork.Desktop;

/// <summary>The Avalonia application: the Fluent theme in dark with the editor's styles, and the main window.</summary>
public sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Ui.Apply(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>
    /// Opens the main window, and catches errors in the UI thread's handlers: logged and shown instead of closing the
    /// app and losing what is not saved.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The loading window first, so something shows while the rest is built; it says each step as it comes.
            var splash = new Splash();
            desktop.MainWindow = splash;
            splash.Show();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            // The last map's models are opened behind the loading window (most often that map is opened again).
            MapProject? last = null;
            Task<ModelStore?>? models = null;
            if (!Options.Driver && MainWindow.LastMapOf(Groundwork.App.AppSettings.Load().Maps) is { } map)
            {
                last = map;
                MapProject.Current = map;
                splash.Show(Groundwork.App.Lang.T("splash.models", map.Name), 0.2);
                models = Task.Run(ModelStore.Open);
            }
            splash.Show(Groundwork.App.Lang.T("splash.window"), 0.5);
            // Posted, so the loading window draws that line before the main window's building takes the UI thread.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                long started = clock.ElapsedMilliseconds;
                var window = new MainWindow();
                window.Preloaded(last, models);
                Options.Say($"start: the models' folder read at {started} ms, the window built in {clock.ElapsedMilliseconds - started} ms");
                // An error in a button's or a key's handler would close the app and lose what is not saved:
                // it is logged and shown instead.
                Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
                {
                    Options.Say($"error: {Groundwork.App.Log.Scrub(e.Exception.ToString())}");
                    e.Handled = true;
                    window.ShowError(e.Exception);
                };
                desktop.MainWindow = window;
                // The loading window stays until the main one has drawn: no blank moment between them.
                window.Opened += (_, _) => splash.Close();
                window.Show();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
