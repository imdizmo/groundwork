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

    /// <summary>The 'unreal' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdUnreal(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        if (MapProject.Current is not { } map || w.View.Scene is not { } scene)
        {
            throw new InvalidOperationException("no map is open");
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string said = args.Length > 0 ? args[0] : "";
        said = said switch
        {
            "link" when args.Length > 1 => Link(map, Path.GetFullPath(string.Join(' ', args[1..]))),
            "landscape" => await UnrealLive.MakeLandscape(map, scene, replace: false),
            "paint" => await UnrealLive.PaintLandscape(map, scene),
            "models" => await UnrealLive.ImportModels(ModelStore.MapModels, UnrealLive.Placed(scene)),
            "objects" when map.HasUnrealLevel => await w.PlaceAllObjects(),
            _ => throw new InvalidOperationException("unreal link <folder>|landscape|paint|models|objects"),
        };
        return JsonSerializer.Serialize(new { said, ms = sw.ElapsedMilliseconds });

        static string Link(MapProject map, string project)
        {
            map.LinkUnreal(project);
            return "linked";
        }
    }

    /// <summary>The 'mappicture' command: mappicture &lt;file.png&gt; [colour|parchment] [pixels].</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMapPicture(Call call)
    {
        string[] args = call.Args;
        if (args.Length == 0 || call.W.Session is not { } s)
        {
            throw new InvalidOperationException("mappicture <file.png> [colour|parchment] [pixels], with a map open");
        }
        var style = args.Length > 1 && args[1] == "parchment" ? MapPicture.Style.Parchment : MapPicture.Style.Colour;
        int side = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 2048;
        return Task.FromResult(JsonSerializer.Serialize(new { said = MainWindow.SaveMapPicture(s, Path.GetFullPath(args[0]), style, side) }));
    }

    /// <summary>The 'replay' command: replay &lt;file.playlog&gt;[|&lt;file&gt;...] | replay team all|&lt;n&gt; | replay seek &lt;seconds&gt; | replay close.</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdReplay(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        string said = args.Length switch
        {
            2 when args[0] == "seek" && float.TryParse(args[1], System.Globalization.CultureInfo.InvariantCulture, out float t) => Seek(w, t),
            1 when args[0] == "close" => Closed(w),
            2 when args[0] == "team" => await FromTeam(w, args[1]),
            > 0 when args[0] is not ("seek" or "close") => w.OpenReplay([.. string.Join(' ', args).Split('|').Select(Path.GetFullPath)]),
            _ => throw new InvalidOperationException("replay <file.playlog>[|<file>...]|seek <seconds>|close"),
        };
        return JsonSerializer.Serialize(new { said, time = w.Replay.Time, player = w.View.ReplayPlayer is var (p, yaw) ? new[] { p.X, p.Y, p.Z, yaw } : null, lines = w.Replay.Lines.Count });

        // the team's sessions of the open map, as the picker lists them: all, or the n-th (1 the newest)
        static async Task<string> FromTeam(MainWindow w, string which)
        {
            if (MapProject.Current is not { } map || Team.Address(w.Settings) is not { } a || Team.Base(a) is not { } server)
            {
                throw new InvalidOperationException("no map open, or no team server set");
            }
            var (rows, error) = await Team.Playlogs(server);
            var mine = rows == null ? throw new InvalidOperationException(error) : MainWindow.TeamPlaylogsOf(rows, map);
            IReadOnlyList<Team.PlaylogRow> chosen = which == "all" ? mine
                : int.TryParse(which, System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= mine.Count ? [mine[n - 1]]
                : throw new InvalidOperationException($"replay team all|1..{mine.Count}");
            return await w.ReplayTeamPlaylogs(server, chosen);
        }

        static string Seek(MainWindow w, float t)
        {
            w.Replay.SeekTo(t);
            return "seeked";
        }

        static string Closed(MainWindow w)
        {
            w.Replay.Close();
            return "closed";
        }
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
