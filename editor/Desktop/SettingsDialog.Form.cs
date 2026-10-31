using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

public static partial class SettingsDialog
{
    /// <summary>
    /// The Settings window and the controls it holds while it is open. Claude's section is built first
    /// (Form), then the mods' extensions are searched for, then the rest is built (Build), and Save applies the choices.
    /// </summary>
    internal sealed partial class Form
    {
        private readonly AppSettings _settings;
        private readonly SolidColorBrush _muted = new(Color.FromRgb(142, 151, 166));
        private CheckBox _claude = null!;
        private TextBox _port = null!;
        private ComboBox _address = null!;
        /// <summary>This computer's network addresses, asked once when the section is built.</summary>
        private IReadOnlyList<(System.Net.IPAddress Address, string Network)> _networks = [];
        /// <summary>This computer's name, asked once when the section is built.</summary>
        private string _hostName = "";
        private string _token = "";
        private TextBox _command = null!;
        private TextBlock _remote = null!;
        private TextBlock _unencrypted = null!;
        private StackPanel _claudeDetails = null!;
        private TextBox _desktop = null!;
        private List<(CheckBox Box, ModExtensions.Extension Extension)> _extensionBoxes = [];
        private StackPanel _extensions = null!;
        private IReadOnlyList<Lang.Language> _languages = [];
        private ComboBox _language = null!;
        private CheckBox _driverGl = null!;
        private TextBlock _error = null!;

        /// <summary>Builds the window and Claude's section.</summary>
        /// <param name="settings">The settings the window changes when saved.</param>
        public Form(AppSettings settings)
        {
            _settings = settings;
            Dialog = new Window
            {
                Title = Lang.T("settings.title"),
                Width = 720,
                // As tall as what it holds (it scrolls past that).
                SizeToContent = SizeToContent.Height,
                MaxHeight = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
            };
            BuildClaude();
        }

        /// <summary>The window, shown by Show once the rest is built.</summary>
        public Window Dialog { get; }

        /// <summary>Whether the user saved the choices (set by Save).</summary>
        public bool Saved { get; private set; }

        /// <summary>A section's heading: upper-case, muted, with space above.</summary>
        /// <param name="t">The heading's text.</param>
        private TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = _muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 14, 0, 4) };

        /// <summary>A muted, wrapped explanation under a control.</summary>
        /// <param name="t">The explanation's text.</param>
        private TextBlock Hint(string t) => new() { Text = t, FontSize = 12, Foreground = _muted, TextWrapping = TextWrapping.Wrap };
    }
}
