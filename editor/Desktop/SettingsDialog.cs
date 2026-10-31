using Avalonia.Controls;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Settings: the language, Claude's connection, the extensions, the history's length; on Windows, what draws.
/// </summary>
public static partial class SettingsDialog
{
    /// <summary>Whether the Drawing part is shown (Windows only; tests set it).</summary>
    internal static bool OnWindows { get; set; } = OperatingSystem.IsWindows();

    /// <summary>
    /// Shows the Settings dialog over <paramref name="owner"/> and waits for it to close. Saved, the mods' manifests and
    /// extensions are found again with the new choices (the extensions' switches).
    /// </summary>
    /// <returns>True when the user saved (Claude's connection settings, the extensions and what draws), false when cancelled or closed.</returns>
    public static async Task<bool> Show(Window owner, AppSettings settings)
    {
        var form = new Form(settings);
        // The mods' editor extensions: found again now (a mod installed since).
        await FindExtensions(settings);
        form.Build();
        await form.Dialog.ShowDialog(owner);
        if (form.Saved)
        {
            await FindExtensions(settings);
        }
        return form.Saved;
    }

    /// <summary>
    /// The plugins found again with these settings, away from the window's thread: every extension file is read and
    /// hashed.
    /// </summary>
    private static Task FindExtensions(AppSettings settings) => Task.Run(() =>
    {
        _ = ModExtensions.Refresh(settings);
    });
}
