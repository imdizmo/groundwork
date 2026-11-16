using Avalonia.Controls;
using Avalonia.Layout;
using Groundwork.App;

namespace Groundwork.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        private CheckBox _teamLauncher = null!;
        private TextBox _team = null!;

        /// <summary>
        /// Builds the Team section: the Launcher's address followed, or one typed here (empty: offline), and a check
        /// of who the server says this is.
        /// </summary>
        private StackPanel BuildTeam()
        {
            string launcher = Team.Launcher;
            _teamLauncher = new CheckBox
            {
                Content = launcher.Length > 0 ? Lang.T("settings.teamLauncher", launcher) : Lang.T("settings.teamLauncherNone"),
                IsChecked = _settings.Team == null,
            }.Classed("switch").Tip("settings.teamLauncher");
            _team = new TextBox { Name = "Team", Text = _settings.Team ?? launcher, PlaceholderText = Lang.T("settings.teamOffline"), Width = 260, IsEnabled = _settings.Team != null }.Tip("settings.team");
            _teamLauncher.IsCheckedChanged += (_, _) =>
            {
                _team.IsEnabled = _teamLauncher.IsChecked != true;
                if (_teamLauncher.IsChecked == true)
                {
                    _team.Text = launcher;
                }
            };
            var said = Hint("");
            var check = new Button { Content = Lang.T("settings.teamCheck") }.Tip("settings.teamCheck");
            check.Click += async (_, _) =>
            {
                string address = (_team.Text ?? "").Trim();
                if (address.Length == 0)
                {
                    said.Text = Lang.T("settings.teamOffline");
                }
                else if (Team.Base(address) is not { } server)
                {
                    said.Text = Lang.T("settings.badTeam");
                }
                else
                {
                    said.Text = Lang.T("settings.teamChecking");
                    var (who, error) = await Team.WhoAmI(server);
                    said.Text = who is { } w ? Team.Said(w) : error;
                }
            };
            return new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    H2(Lang.T("settings.team")),
                    _teamLauncher,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Lang.T("settings.address"), VerticalAlignment = VerticalAlignment.Center }, _team, check } },
                    said,
                    Hint(Lang.T("settings.teamHint")),
                },
            };
        }

        /// <summary>The team address to save: null follows the Launcher, empty is offline; false when not an address.</summary>
        private (bool Ok, string? Team) ReadTeam()
        {
            if (_teamLauncher.IsChecked == true)
            {
                return (true, null);
            }
            string a = (_team.Text ?? "").Trim();
            return (a.Length == 0 || Team.Base(a) != null, a);
        }
    }
}
