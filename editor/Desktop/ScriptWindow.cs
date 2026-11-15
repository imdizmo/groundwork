using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The script window (⋯ → Script…): a C# script with the editor's API (Map, Ground, Objects, Noise, Rnd, Print), the
/// examples and the scripts saved in the data folder (scripts/*.csx), Run (Ctrl+Enter) and Stop, and what the script
/// printed, changed or got wrong. A run is compiled, run in a process of its own on a snapshot of the map
/// (ScriptHost.RunInChild), and what it changed goes into the map as one step in History; nothing changes when it has
/// mistakes, fails, is stopped, runs 5 minutes, or the map changed meanwhile.
/// </summary>
public sealed class ScriptWindow : Window
{
    /// <summary>How long a script may run.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    private readonly Func<(EditSession Session, MapProject Map)?> _open;
    private readonly Action _changed;
    private readonly ComboBox _list = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 420 };
    private readonly TextBox _name = new() { PlaceholderText = "Name to save it as", FontSize = 12, Width = 200 };
    private readonly Button _run = new Button { Content = "Run (Ctrl+Enter)", FontSize = 12 }.Classed("primary");
    private readonly Button _stop = new() { Content = "Stop", FontSize = 12, IsEnabled = false };
    private List<(string Name, string Code, bool Example)> _entries = [];
    private bool _filling;
    private CancellationTokenSource? _running;

    /// <summary>The script's code.</summary>
    internal TextBox Code { get; } = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("monospace"),
        FontSize = 12.5,
    };

    /// <summary>What the script printed, changed or got wrong.</summary>
    internal SelectableTextBlock Output { get; } = new() { FontFamily = new FontFamily("monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap };

    /// <summary>The folder saved scripts go in.</summary>
    internal static string Folder => Path.Combine(AppSettings.DataDir, "scripts");

    /// <summary>The window over the open map (asked at each run) and what to call after a run changed it.</summary>
    internal ScriptWindow(Func<(EditSession, MapProject)?> open, Action changed)
    {
        (_open, _changed) = (open, changed);
        Title = "Script";
        Width = 860;
        Height = 780;
        Background = Ui.Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _list.SelectionChanged += (_, _) =>
        {
            if (!_filling && _list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Count)
            {
                var (name, code, example) = _entries[_list.SelectedIndex];
                Code.Text = code;
                _name.Text = example ? "" : name;
            }
        };
        _run.Click += async (_, _) => await Run();
        _stop.Click += (_, _) => _running?.Cancel();
        var save = new Button { Content = "Save", FontSize = 12 };
        save.Click += (_, _) => Save();
        // On the way down, before the text box takes Enter for a new line.
        Code.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                e.Handled = true;
                await Run();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ToolTip.SetTip(_list, "An example or a saved script: choosing one replaces the code (save yours first).");
        ToolTip.SetTip(_stop, "Stops the script, whatever it is doing: nothing it did is kept.");
        ToolTip.SetTip(save, $"Saves the code as <name>.csx in {Folder}.");
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _run, _stop, _name, save } };
        var hint = new TextBlock
        {
            Text = "Map (MinX…MaxZ, CenterX, Sea, Points, Inside) · Ground (Height, Original, Set, Raise, Lower, Shape, Mountain, Paint, Layers) · Objects (All, Kinds, OfGroup, OfKind, Near, Place, Move, Remove) · Noise.At · Rnd · Print. Metres, x east, z north. The Help's Script page has them all. A script runs with your rights on this computer: run only scripts you wrote or trust.",
            FontSize = 11,
            Foreground = Ui.Muted,
            TextWrapping = TextWrapping.Wrap,
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,140,Auto"), Margin = new Thickness(12), RowSpacing = 8 };
        grid.Children.Add(_list);
        var code = new Border { Child = Code, BorderBrush = Ui.Line, BorderThickness = new Thickness(1) };
        Grid.SetRow(code, 1);
        grid.Children.Add(code);
        Grid.SetRow(bar, 2);
        grid.Children.Add(bar);
        var output = new ScrollViewer { Content = Output };
        Grid.SetRow(output, 3);
        grid.Children.Add(output);
        Grid.SetRow(hint, 4);
        grid.Children.Add(hint);
        Content = grid;
        Fill();
        _list.SelectedIndex = 0;
        Closed += (_, _) => _running?.Cancel();
    }

    /// <summary>Fills the list: the examples, then the saved scripts (newest first); selects the saved one of that name if given.</summary>
    private void Fill(string? select = null)
    {
        _filling = true;
        _entries = [.. ScriptExamples.All.Select(e => (e.Name, e.Code, true))];
        try
        {
            if (Directory.Exists(Folder))
            {
                foreach (var f in new DirectoryInfo(Folder).GetFiles("*.csx").OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    try
                    {
                        _entries.Add((Path.GetFileNameWithoutExtension(f.Name), File.ReadAllText(f.FullName), false));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Log.Caught(ex, "reading a script to list it (it is left out)");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "listing the scripts folder");
        }
        _list.ItemsSource = _entries.Select(e => e.Example ? "Example: " + e.Name : e.Name).ToList();
        _list.SelectedIndex = select != null ? _entries.FindIndex(e => !e.Example && e.Name == select) : -1;
        _filling = false;
    }

    /// <summary>
    /// Saves the code under the typed name (characters a file name cannot hold become _). Another saved script of that
    /// name is replaced only when it is the one chosen in the list.
    /// </summary>
    internal void Save()
    {
        string name = string.Concat((_name.Text ?? "").Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (name.Length == 0)
        {
            Output.Text = "Type a name to save the script as.";
            return;
        }
        string path = Path.Combine(Folder, name + ".csx");
        bool chosen = _list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Count && _entries[_list.SelectedIndex] is { Example: false } e && e.Name == name;
        if (File.Exists(path) && !chosen)
        {
            Output.Text = $"A script named {name} is saved already: choose it in the list first to save over it, or type another name.";
            return;
        }
        try
        {
            _ = Directory.CreateDirectory(Folder);
            SafeFile.WriteAllText(path, Code.Text ?? "");
            Fill(name);
            Output.Text = $"Saved {name} in {Folder}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Output.Text = $"The script could not be saved: {ex.Message}";
        }
    }

    /// <summary>Runs the code on the open map; what it printed and changed, or what went wrong, in the output.</summary>
    internal async Task Run()
    {
        if (_running != null)
        {
            return;
        }
        string label = "Script: " + (_list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Count ? _entries[_list.SelectedIndex].Name : "unnamed");
        using var cancel = new CancellationTokenSource(Limit);
        _running = cancel;
        _run.IsEnabled = false;
        _stop.IsEnabled = true;
        try
        {
            Output.Text = await RunCode(Code.Text ?? "", label, _open, _changed, cancel.Token, said => Output.Text = said);
        }
        finally
        {
            _running = null;
            _run.IsEnabled = true;
            _stop.IsEnabled = false;
        }
    }

    /// <summary>
    /// A script on the open map: compiled in the background, run in its own process on a snapshot, its changes one step
    /// in History. What it printed, then what it changed or why nothing changed.
    /// </summary>
    internal static async Task<string> RunCode(string code, string label, Func<(EditSession Session, MapProject Map)?> open, Action changed, CancellationToken cancel, Action<string>? progress = null)
    {
        if (open() is not var (s, map))
        {
            return "No map is open.";
        }
        progress?.Invoke("Compiling…");
        var snap = ScriptHost.Take(s, map);
        int generation = s.Generation;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var (image, pdb, errors) = await Task.Run(() => ScriptHost.Compile(code), CancellationToken.None);
            if (image == null)
            {
                return "Mistakes (nothing ran):\n" + string.Join("\n", errors);
            }
            progress?.Invoke("Running…");
            var ch = await ScriptHost.RunInChild(image, pdb, snap, cancel);
            string printed = ch.Output.ToString();
            string Said(string line) => printed + (printed.Length > 0 && !printed.EndsWith('\n') ? "\n" : "") + line;
            if (open() is not var (now, _) || now != s)
            {
                return Said("The map was closed or another opened meanwhile: nothing changed.");
            }
            // Its heights and object numbers are those of the map when it started: changed since, they would undo that.
            if (s.Generation != generation)
            {
                return Said("The map changed while the script ran: nothing changed. Run it again.");
            }
            if (ch.Empty)
            {
                return Said("The script changed nothing.");
            }
            var parts = new List<string>();
            if (ch.Heights.Count > 0)
            {
                parts.Add($"{ch.Heights.Count} ground points");
            }
            if (ch.Paint.Count > 0)
            {
                parts.Add($"{ch.Paint.Select(p => p.Point).Distinct().Count()} points painted");
            }
            if (ch.Add.Count > 0)
            {
                parts.Add($"{ch.Add.Count} objects placed");
            }
            int moves = ch.Moves.Keys.Count(i => !ch.Remove.Contains(i));
            if (moves > 0)
            {
                parts.Add($"{moves} moved");
            }
            if (ch.Remove.Count > 0)
            {
                parts.Add($"{ch.Remove.Count} taken away");
            }
            ScriptHost.Apply(s, map, ch, label);
            changed();
            return Said($"Changed {string.Join(", ", parts)} in {watch.Elapsed.TotalSeconds:0.0} s: one step in History (Ctrl+Z takes it back).");
        }
        catch (OperationCanceledException)
        {
            return watch.Elapsed >= Limit ? $"The script ran {Limit.TotalMinutes:0} minutes and was stopped: nothing changed." : "Stopped: nothing changed.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Caught(ex, "running a script");
            return "The script failed (nothing changed):\n" + ScriptHost.Describe(ex);
        }
    }
}
