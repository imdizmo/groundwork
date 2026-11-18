using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Groundwork.Desktop;

/// <summary>
/// Replay: a recorded play session (PlayLog) over the open map. The path walked and its events are drawn on the
/// ground (GlView.Replay); a slider sets the time, Play runs it at a chosen speed, and the player stands where they
/// were then. The list of events moves the view to each one.
/// </summary>
public sealed class ReplayPanel
{
    /// <summary>The events listed and drawn as posts (the many small ones, positions, hits, gathering, panels and pauses, are not).</summary>
    private static readonly HashSet<string> Listed = ["death", "kill", "quest", "craft", "build", "place", "respawn", "teleport", "map", "end"];

    /// <summary>The speeds Play offers.</summary>
    private static readonly int[] Speeds = [1, 4, 16, 64];

    /// <summary>The panel's card (hidden while no session is open).</summary>
    public Control Card { get; }

    /// <summary>The session open, or null.</summary>
    internal PlayLog? Log { get; private set; }

    /// <summary>The sessions open: one (Log), or several at once (no time then).</summary>
    internal IReadOnlyList<PlayLog> Logs { get; private set; } = [];

    /// <summary>Most sessions shown at once.</summary>
    internal const int MostSessions = 200;

    /// <summary>The replay's time, seconds since the session began.</summary>
    internal float Time { get; private set; }

    /// <summary>The map the session is shown on (its legs and events there).</summary>
    internal string ShownMap { get; private set; } = "";

    /// <summary>For tests: the events listed, as their lines read.</summary>
    internal IReadOnlyList<string> Lines => [.. _items.Select(i => i.Text)];

    private readonly GlView _view;
    private readonly TextBlock _title = new() { FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _warning = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 150, 90)), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _time = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider _slider = new() { Minimum = 0, Maximum = 1 };
    private readonly ComboBox _speed = new() { ItemsSource = Speeds.Select(s => $"{s}×").ToArray(), SelectedIndex = 1, FontSize = 12 };
    private readonly ListBox _list = new() { MaxHeight = 260, FontSize = 12 };
    private readonly TextBlock _legend = new() { FontSize = 11, Foreground = Tokens.Dim, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _timeRow = new() { Spacing = 6 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly List<(float T, Vector3? P, string Text)> _items = [];
    private bool _setting;

    /// <summary>Play / Pause.</summary>
    internal Button PlayButton { get; } = new() { Content = "Play", FontSize = 12 };

    /// <summary>The camera follows the player while it plays.</summary>
    internal CheckBox FollowBox { get; } = new() { Content = "Follow", FontSize = 12, IsChecked = true };

    /// <summary>Closes the session.</summary>
    internal Button CloseButton { get; } = new() { Content = "Close", FontSize = 12 };

    /// <summary>Builds the (hidden) card over the view the session is drawn in.</summary>
    public ReplayPanel(GlView view)
    {
        _view = view;
        _timeRow.Children.Add(_slider);
        _timeRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { PlayButton, _speed, FollowBox, _time } });
        _slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && !_setting)
            {
                SeekTo((float)_slider.Value);
            }
        };
        PlayButton.Click += (_, _) => Playing(!_timer.IsEnabled);
        CloseButton.Click += (_, _) => Close();
        _timer.Tick += (_, _) =>
        {
            float t = Time + (0.1f * Speeds[Math.Max(0, _speed.SelectedIndex)]);
            if (Log == null || t >= Log.Length)
            {
                Playing(false);
                t = Log?.Length ?? 0;
            }
            SeekTo(t);
            if (FollowBox.IsChecked == true && _view.ReplayPlayer is var (p, _) && _view.WorldCamera is var (yaw, pitch, distance, _))
            {
                _view.WorldCamera = (yaw, pitch, distance, p);
            }
        };
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex is int k and >= 0 && k < _items.Count)
            {
                Playing(false);
                if (Log != null)
                {
                    SeekTo(_items[k].T);
                }
                if (_items[k].P is { } p)
                {
                    float yaw = _view.Camera.Yaw * 180 / MathF.PI;
                    _view.Orbit(p.X, p.Z, yaw, 50, 60, p.Y);
                }
            }
        };
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _title, Ui.Col(CloseButton, 1) } },
                    _warning,
                    _timeRow,
                    _legend,
                    _list,
                },
            },
        });
        Card.IsVisible = false;
    }

    /// <summary>Shows a session on the open map.</summary>
    /// <param name="log">The session.</param>
    /// <param name="mapName">The open map's name: the session's legs on a map of that name are shown, else those on the map it started on.</param>
    /// <param name="warning">What does not match (another map, another version), or null.</param>
    internal void Open(PlayLog log, string mapName, string? warning)
    {
        Playing(false);
        Log = log;
        Logs = [log];
        _timeRow.IsVisible = true;
        _legend.Text = "Red: deaths. Orange: where the player stood still. Green: quest steps.";
        ShownMap = log.Legs.Any(l => l.Map == mapName) ? mapName : log.MapName;
        _title.Text = $"Replay: player {log.Player}, {log.Started?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "no start time"}";
        _warning.Text = warning ?? "";
        _warning.IsVisible = warning != null;
        _items.Clear();
        var marks = new List<(Vector3, GlView.ReplayMark, float)>();
        foreach (var e in log.Events.Where(e => Listed.Contains(e.E) && e.Map == ShownMap))
        {
            // a quest step or a craft carries no place: where the player was then
            Vector3? p = e.P ?? log.At(e.T)?.P;
            string what = e.E switch
            {
                "death" => $"death, by {e.Text("by") ?? "?"}",
                "kill" => $"killed {e.Text("what") ?? "?"}",
                "quest" => $"quest {e.Text("id") ?? "?"}: {e.Text("step") ?? "?"}",
                "craft" => $"crafted {e.Text("recipe") ?? "?"}",
                "build" => $"{e.Text("op") ?? "built"} {e.Text("piece") ?? "?"}",
                "place" => $"reached {e.Text("id") ?? "?"}",
                "map" => $"to map {e.Text("name") ?? "?"}",
                "end" => $"end: {e.Text("why") ?? "?"}",
                _ => e.E,
            };
            _items.Add((e.T, p, $"{Clock(e.T)}  {what}"));
            if (p is { } at && e.E != "end")
            {
                marks.Add((at, e.E switch { "death" => GlView.ReplayMark.Death, "quest" => GlView.ReplayMark.Quest, _ => GlView.ReplayMark.Other }, 8));
            }
        }
        foreach (var s in log.Stops.Where(s => s.Map == ShownMap))
        {
            _items.Add((s.From, s.P, $"{Clock(s.From)}  stood still {Clock(s.Seconds)}"));
            marks.Add((s.P, GlView.ReplayMark.Stop, 8));
        }
        _items.Sort((a, b) => a.T.CompareTo(b.T));
        _list.ItemsSource = _items.Select(i => i.Text).ToList();
        _view.Replay = new GlView.ReplayShown([.. log.Legs.Where(l => l.Map == ShownMap).Select(l => l.Points.Select(q => q.P).ToArray())], marks);
        _slider.Maximum = Math.Max(1, log.Length);
        Card.IsVisible = true;
        SeekTo(0);
    }

    /// <summary>
    /// Shows several sessions at once: every path faint, and where they died and stood still as posts, taller where more
    /// did (PlayLog.Spots); the list holds those places, most first.
    /// </summary>
    /// <param name="logs">The sessions (two or more).</param>
    /// <param name="mapName">The open map's name: the sessions' legs on a map of that name are shown, else those on the first one's map.</param>
    /// <param name="warning">What does not match, or null.</param>
    internal void OpenMany(IReadOnlyList<PlayLog> logs, string mapName, string? warning)
    {
        Playing(false);
        Log = null;
        Logs = logs;
        ShownMap = logs.Any(l => l.Legs.Any(g => g.Map == mapName)) ? mapName : logs[0].MapName;
        _title.Text = $"Replay: {logs.Count} sessions of {logs.Select(l => l.Player).Distinct().Count()} players";
        _warning.Text = warning ?? "";
        _warning.IsVisible = warning != null;
        _timeRow.IsVisible = false;
        _legend.Text = "Red: where they died. Orange: where they stood still. Taller: more of them.";
        _items.Clear();
        var marks = new List<(Vector3, GlView.ReplayMark, float)>();
        foreach (var s in PlayLog.Spots(logs, ShownMap))
        {
            bool death = s.Kind == "death";
            string what = death ? $"{s.Count} died ({string.Join(", ", s.Causes.Take(3).Select(c => $"{c.By} {c.Count}"))})" : $"{s.Count} stood still, {Clock(s.Seconds)} in all";
            _items.Add((0, s.P, $"{what}, {s.Sessions} sessions, at {s.P.X:0} {s.P.Z:0}"));
            marks.Add((s.P, death ? GlView.ReplayMark.Death : GlView.ReplayMark.Stop, MathF.Min(40, 6 + (4 * s.Count))));
        }
        _list.ItemsSource = _items.Select(i => i.Text).ToList();
        _view.Replay = new GlView.ReplayShown([.. logs.SelectMany(l => l.Legs.Where(g => g.Map == ShownMap).Select(g => g.Points.Select(q => q.P).ToArray()))], marks, Faint: true);
        _view.ReplayPlayer = null;
        Card.IsVisible = true;
    }

    /// <summary>Closes the session: nothing is drawn any more.</summary>
    internal void Close()
    {
        Playing(false);
        Log = null;
        Logs = [];
        _items.Clear();
        _list.ItemsSource = null;
        _view.Replay = null;
        _view.ReplayPlayer = null;
        Card.IsVisible = false;
    }

    /// <summary>Sets the replay's time: the slider, the clock and the player follow.</summary>
    internal void SeekTo(float t)
    {
        if (Log == null)
        {
            return;
        }
        Time = Math.Clamp(t, 0, Log.Length);
        _setting = true;
        _slider.Value = Time;
        _setting = false;
        _time.Text = $"{Clock(Time)} / {Clock(Log.Length)}";
        _view.ReplayPlayer = Log.At(Time) is var (p, yaw, map) && map == ShownMap ? (p, yaw) : null;
    }

    /// <summary>Plays or stops.</summary>
    private void Playing(bool on)
    {
        if (on && Log != null && Time >= Log.Length)
        {
            SeekTo(0);
        }
        _timer.IsEnabled = on && Log != null;
        PlayButton.Content = _timer.IsEnabled ? "Pause" : "Play";
    }

    /// <summary>Seconds as m:ss (h:mm:ss from an hour).</summary>
    private static string Clock(float s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
}
