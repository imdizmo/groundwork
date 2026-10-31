using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Groundwork.Desktop.Tests.TestApp))]
// One Avalonia app for the whole run (each test closes its own windows: CloseWindowsAfterEachTest).
// A new app per test was set up now and then on another thread than the dispatcher's, which failed
// the test's cleanup ("The calling thread cannot access this object") at random, on CI too.
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The tests' Avalonia app (headless), and the files the editor keeps pointed at the tests' own.
/// </summary>
public static class TestApp
{

    /// <summary>
    /// The headless app the Avalonia tests run in, with windows closed after each test.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        CloseWindowsAfterEachTestAttribute.Hook();
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
