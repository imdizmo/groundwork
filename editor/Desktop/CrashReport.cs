using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// A run that ended badly, offered to the team at the next start. A marker in the data folder says a run is going; a
/// clean exit (closing the window, SIGTERM) removes it unless the run had an unhandled error. Found at the next start,
/// the old run's log is kept beside the new one, and with a team server set the person is asked, seeing exactly what
/// goes, whether to send it (POST /team/crashes). Nothing is sent without that click.
/// </summary>
internal static class CrashReport
{
    /// <summary>The largest report the server takes; the log keeps its end when longer.</summary>
    private const int MaxBytes = 20 << 20;

    private static string Marker => Path.Combine(AppSettings.DataDir, "running");

    /// <summary>The run before this one's log, kept when that run ended badly.</summary>
    internal static string PreviousLog => Path.Combine(AppSettings.DataDir, "Groundwork.previous.log");

    /// <summary>Why the last run ended badly (null: it did not).</summary>
    internal static string? Pending { get; private set; }

    private static int _errors;

    /// <summary>Before the log starts: the last run's marker found (its log kept), this run's written.</summary>
    public static void Begin()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            if (File.Exists(Marker))
            {
                Pending = File.ReadAllText(Marker).Trim() is { Length: > 0 } why ? why : "it ended without closing";
                if (File.Exists(Log.FilePath))
                {
                    File.Copy(Log.FilePath, PreviousLog, overwrite: true);
                }
            }
            File.WriteAllText(Marker, "it ended without closing (stopped, killed or crashed)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"The run marker could not be written: {e.Message}");
        }
    }

    /// <summary>An unhandled error in this run (the run is reported even if it then closes cleanly).</summary>
    public static void Error(string what)
    {
        if (Interlocked.Increment(ref _errors) == 1)
        {
            try
            {
                File.WriteAllText(Marker, $"it had an error: {what}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>A clean exit: the marker goes, unless the run had an error.</summary>
    public static void End()
    {
        if (_errors == 0)
        {
            try
            {
                File.Delete(Marker);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>What is sent: a note of the run (as JSON) and its log, as files in one zip.</summary>
    internal static (string Info, string Log) Contents(string why)
    {
        string info = new JsonObject
        {
            ["tool"] = "groundwork",
            ["version"] = BuildInfo.Version,
            ["os"] = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
            ["osDescription"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["why"] = why,
            ["memoryAvailableToDotnet"] = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            ["memoryMax"] = long.TryParse(Environment.GetEnvironmentVariable("JATIFY_MEMORY_MAX"), out long cap) ? cap : null,
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        string log;
        try
        {
            log = File.Exists(PreviousLog) ? File.ReadAllText(PreviousLog) : "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log = $"(the log could not be read: {e.Message})";
        }
        int room = MaxBytes - (1 << 20);
        if (log.Length > room)
        {
            log = "(the start of the log is left out: too long)\n" + log[^room..];
        }
        return (info, log);
    }

    /// <summary>The report as one zip: info.json and Groundwork.log.</summary>
    internal static byte[] Zip(string info, string log)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in new[] { ("info.json", info), ("Groundwork.log", log) })
            {
                using var w = new StreamWriter(z.CreateEntry(name, CompressionLevel.Optimal).Open());
                w.Write(text);
            }
        }
        return ms.ToArray();
    }

    /// <summary>At the start, when the last run ended badly and a team server is set: asks whether to send its report.</summary>
    public static async Task Offer(Window owner, AppSettings settings, Func<string, Task> say)
    {
        if (Pending is not { } why || Team.Address(settings) is not { } address || Team.Base(address) is not { } server)
        {
            return;
        }
        Pending = null;
        var (info, log) = Contents(why);
        if (!await Ask(owner, why, info, log))
        {
            return;
        }
        var (id, error) = await Team.SendCrash(server, BuildInfo.Version, Zip(info, log));
        await say(id != null ? "The report was sent to the team. Thank you." : $"The report could not be sent: {error}");
    }

    /// <summary>The question, with everything that would be sent shown under it: true for Send.</summary>
    private static async Task<bool> Ask(Window owner, string why, string info, string log)
    {
        bool send = false;
        var dialog = new Window
        {
            Title = "Groundwork closed unexpectedly",
            Width = 720,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Tokens.Raised,
        };
        var yes = new Button { Content = "Send", IsDefault = true }.Classed("primary");
        yes.Click += (_, _) => { send = true; dialog.Close(); };
        var no = new Button { Content = "Don't send", IsCancel = true };
        no.Click += (_, _) => dialog.Close();
        var shown = new TextBox
        {
            Text = $"info.json\n{info}\n\nGroundwork.log\n{log}",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
        };
        var head = new TextBlock
        {
            Text = $"Groundwork closed unexpectedly the last time ({why}). Send the report to the team? It is what is below: a note of the run and its log, nothing else.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } };
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dialog.Content = new DockPanel { Margin = new Thickness(18), LastChildFill = true, Children = { head, buttons, new Border { Margin = new Thickness(0, 12), Child = shown } } };
        await dialog.ShowDialog(owner);
        return send;
    }
}
