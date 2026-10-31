using System.Text.Json;

namespace Groundwork.Desktop;

/// <summary>
/// The commands that open and close maps, the panels and the messages.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'map' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdMap(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (a.Length > 1)
        {
            await w.OpenMap(Path.GetFullPath(a[1]));
        }
        else
        {
            await w.ShowMap(w.LastMap());
        }
        return JsonSerializer.Serialize(new { opened = sw.ElapsedMilliseconds, map = w.InMap, objects = w.View.Scene?.Things.Count(t => !t.Gone) ?? 0 });
    }

    /// <summary>The 'panel' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdPanel(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        w.ShowRightPanel(args[0]);
        return Task.FromResult(State(w));
    }

    /// <summary>The 'message' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMessage(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        w.StatusMessage = a.Length > 1 ? a[1] : "";
        return Task.FromResult(State(w));
    }

    /// <summary>The 'wait' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdWait(Call call)
    {
        MainWindow w = call.W;
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(call.F(0), 0, 60000)));
        return State(w);
    }

    /// <summary>The 'state' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdState(Call call)
    {
        MainWindow w = call.W;
        return Task.FromResult(State(w));
    }
}
