using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Settings window tests' setup: a temporary folder for the folders the window is given, a shown owner window, and
/// the window's boxes and buttons found.
/// </summary>
internal static class SettingsDialogRun
{
    /// <summary>
    /// A temporary folder for the folders the Settings window is given; deleted when disposed.
    /// </summary>
    internal sealed class Temp : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gw-settings-ui-" + Guid.NewGuid().ToString("N")[..8]);

        /// <summary>
        /// A new folder of this name in the temporary folder.
        /// </summary>
        public string Dir(string name)
        {
            string d = Path.Combine(Root, name);
            Directory.CreateDirectory(d);
            return d;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }

    /// <summary>
    /// A shown 900 × 700 window to own the Settings window.
    /// </summary>
    internal static Window Owner()
    {
        var w = new Window { Width = 900, Height = 700 };
        w.Show();
        return w;
    }

    /// <summary>
    /// The folders' boxes (not Claude's port and command, nor the history's steps).
    /// </summary>
    internal static List<TextBox> Boxes(Window d) => [.. d.GetLogicalDescendants().OfType<TextBox>().Where(b => b.Name?.StartsWith("Claude", StringComparison.Ordinal) != true && b.Name != "HistorySteps")];

    /// <summary>
    /// The text box with this name.
    /// </summary>
    internal static TextBox Named(Window d, string name) => d.GetLogicalDescendants().OfType<TextBox>().Single(b => b.Name == name);

    /// <summary>
    /// The buttons with this label.
    /// </summary>
    internal static List<Button> Buttons(Window d, string label) => [.. d.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == label)];

    /// <summary>
    /// Every text block's text in the window, joined with " | ".
    /// </summary>
    internal static string Texts(Window d) => string.Join(" | ", d.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
}
