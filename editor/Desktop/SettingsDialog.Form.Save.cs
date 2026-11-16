using System.Globalization;
using Groundwork.App;

namespace Groundwork.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        /// <summary>
        /// Checks the port and applies the window's choices, saving them. A problem leaves the window open
        /// with its message, and the choices as they were; the extensions turned on run now, those turned off stop, and the
        /// history keeps the steps chosen from the next change on.
        /// </summary>
        private void Save()
        {
            if (!int.TryParse(_port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int claudePort) || claudePort is < 1024 or > 65535)
            {
                _error.Text = Lang.T("settings.badPort");
                return;
            }
            if (ChosenAddress() is not { } claudeAddress)
            {
                _error.Text = Lang.T("settings.badAddress");
                return;
            }
            if (ReadHistorySteps() is not int historySteps)
            {
                _error.Text = Lang.T("settings.badHistorySteps");
                return;
            }
            if (ReadTeam() is not (true, var team))
            {
                _error.Text = Lang.T("settings.badTeam");
                return;
            }
            // Taken back when they cannot be saved, so a Cancel after that keeps nothing.
            var before = (_settings.ClaudeConnect, _settings.ClaudePort, _settings.ClaudeAddress, _settings.ClaudeToken, _settings.DriverOpenGl, _settings.Language, _settings.HistorySteps, _settings.Team);
            var allowedBefore = new Dictionary<string, string>(_settings.Extensions);
            foreach (var (box, x) in _extensionBoxes)
            {
                ModExtensions.Allow(_settings, x, box.IsChecked == true);
            }
            _settings.ClaudeConnect = _claude.IsChecked == true;
            _settings.ClaudePort = claudePort;
            _settings.ClaudeAddress = claudeAddress.ToString();
            _settings.ClaudeToken = _token;
            _settings.DriverOpenGl = _driverGl.IsChecked == true;
            _settings.HistorySteps = historySteps;
            _settings.Team = team;
            _settings.Language = _language.SelectedIndex > 0 ? _languages[_language.SelectedIndex - 1].Code : null;
            if (_settings.Save() is string notSaved)
            {
                (_settings.ClaudeConnect, _settings.ClaudePort, _settings.ClaudeAddress, _settings.ClaudeToken, _settings.DriverOpenGl, _settings.Language, _settings.HistorySteps, _settings.Team) = before;
                _settings.Extensions = allowedBefore;
                _error.Text = Lang.T("settings.notSaved", notSaved);
                return;
            }
            // The extensions turned on run now, those turned off stop.
            ModExtensions.Refresh(_settings);
            EditSession.StepLimit = historySteps;
            Saved = true;
            Dialog.Close();
        }
    }
}
