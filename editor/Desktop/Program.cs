using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Groundwork.Desktop;

/// <summary>
/// The native editor: an Avalonia window, the 3D view and the map drawn with OpenGL. It opens on the start page; the
/// options (see Options) open an area directly or let a program drive it.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the editor, or with <c>--script-host</c>, <c>--mcp-stdio</c>, <c>--export-test-data</c> or
    /// <c>--export-locations</c> does only that and ends.
    /// </summary>
    /// <param name="args">The command line (see <see cref="Options"/>).</param>
    [STAThread]
    public static void Main(string[] args)
    {
        // --mcp-stdio: only the relay to the running editor for Claude (nothing else on stdout: no log,
        // no window).
        if (args.Contains("--mcp-stdio"))
        {
            ClaudeRelay.Run();
            return;
        }
        // A command line that cannot be read (an option without its value, a number that is none): said, and the
        // editor does not start.
        try
        {
            Options.Parse(args);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            // Before the log starts, and the answer to whoever typed the command: the error output only, as typed.
            Console.Error.WriteLine($"Groundwork: {e.Message}");
            Environment.ExitCode = 2;
            return;
        }
        // Everything said also goes to Groundwork.log in the data folder (a window app has no console on Windows).
        Groundwork.App.Log.Start(BuildInfo.Version);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Options.Say($"crash: {Groundwork.App.Log.Scrub(e.ExceptionObject.ToString() ?? "")}");
        TaskScheduler.UnobservedTaskException += (_, e) => Options.Say($"error in a background task: {Groundwork.App.Log.Scrub(e.Exception.ToString())}");
        // Logging out, shutting down or `kill` (SIGTERM, SIGHUP): the tunnel closes,
        // then the app ends (changes not saved are lost, as when the computer turns off).
        using var term = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, Quit);
        using var hup = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP, Quit);
        // GROUNDWORK_TRACE=1: Avalonia's warnings on the console.
        if (Environment.GetEnvironmentVariable("GROUNDWORK_TRACE") == "1")
        {
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        }
        var settings = Groundwork.App.AppSettings.Load();
        // The language the windows are built in (Settings; the computer's by default).
        Options.Say($"language: {Groundwork.App.Lang.Use(settings.Language)}");
        // How many steps the history keeps (Settings).
        EditSession.StepLimit = settings.HistorySteps;
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
            .With(new Win32PlatformOptions { RenderingMode = WindowsRendering(settings.DriverOpenGl) })
            // Linux: Avalonia turns OpenGL off for software renderers (Mesa's llvmpipe, VMware's
            // SVGA3D), where its own drawing is faster without it; the 3D view and the map need
            // OpenGL, so they were blank in virtual machines and without GPU drivers.
            .With(new X11PlatformOptions { GlxRendererBlacklist = [] })
            // macOS: OpenGL (4.1, core), which the 3D view and the map draw with; Metal would leave them blank.
            .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// SIGTERM or SIGHUP: closes the tunnel, then shuts the app down on the UI thread (instead of the process ending at
    /// once).
    /// </summary>
    internal static void Quit(System.Runtime.InteropServices.PosixSignalContext c)
    {
        c.Cancel = true;
        Options.Say($"quit: {c.Signal}");
        Avalonia.Threading.Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }

    /// <summary>
    /// How Windows draws, in the order tried: ANGLE (OpenGL ES on Direct3D 11) first, then the graphics driver's own
    /// OpenGL (WGL), drawing in software last. Old or laptop drivers' OpenGL showed old frames (objects coming and going)
    /// and black lines across the window (issue #115); Direct3D drivers are the ones games use. Settings' "Draw with the
    /// graphics driver's OpenGL" puts WGL first again, for a driver whose Direct3D draws worse.
    /// </summary>
    /// <param name="driverOpenGl">Whether the driver's OpenGL is tried first.</param>
    /// <returns>The modes, the first that works taken.</returns>
    internal static Win32RenderingMode[] WindowsRendering(bool driverOpenGl) => driverOpenGl
        ? [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
        : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software];
}

