using Groundwork.App;

namespace Groundwork.Desktop;

// ⋯ → Replay play sessions from the team: the sessions of the open map the team server keeps, taken down and replayed.
public sealed partial class MainWindow
{
    /// <summary>Most play sessions kept in the data folder's playlogs folder (the oldest go).</summary>
    private const int MostKeptPlaylogs = 300;

    /// <summary>The folder team play sessions are kept in once taken down.</summary>
    internal static string PlaylogFolder => Path.Combine(AppSettings.DataDir, "playlogs");

    /// <summary>The team's play sessions of the open map: one picked, or all of them, taken down and replayed.</summary>
    private async Task ReplayFromTeam()
    {
        if (MapProject.Current is not { } map || await TeamServer() is not { } server)
        {
            return;
        }
        var (rows, error) = await Retrying(() => Team.Playlogs(server));
        if (rows == null)
        {
            Msg(new Said($"The team's play sessions could not be listed: {error}", ""));
            return;
        }
        var mine = TeamPlaylogsOf(rows, map);
        if (mine.Count == 0)
        {
            await Tell($"The team has no play sessions of {map.Name} ({rows.Count} of other maps).");
            return;
        }
        List<string> lines = [$"All {mine.Count} sessions of this map", .. mine.Select(PlaylogLine)];
        if (await Dialogs.Choose(this, "Replay play sessions from the team", $"The play sessions of {map.Name} on the team server, newest first:", lines, "Replay") is not int i)
        {
            return;
        }
        Msg(new Said($"Taking {(i == 0 ? mine.Count : 1)} play session{(i == 0 && mine.Count > 1 ? "s" : "")} from the team...", ""));
        Msg(new Said(await ReplayTeamPlaylogs(server, i == 0 ? mine : [mine[i - 1]]), ""));
    }

    /// <summary>Team play sessions taken down and replayed over the open map; what happened, in words.</summary>
    internal async Task<string> ReplayTeamPlaylogs(Uri server, IReadOnlyList<Team.PlaylogRow> chosen)
    {
        var files = new List<string>();
        var failed = new List<string>();
        foreach (var row in chosen)
        {
            var (file, problem) = await TakePlaylog(server, row);
            if (file != null)
            {
                files.Add(file);
            }
            else
            {
                failed.Add(problem!);
            }
        }
        string said = files.Count > 0 ? OpenReplay(files) : "Nothing to replay.";
        return failed.Count > 0 ? $"{said} Not taken: {string.Join("; ", failed.Distinct())}" : said;
    }

    /// <summary>The sessions played on a map: by its team id when both have one, else by its name.</summary>
    internal static List<Team.PlaylogRow> TeamPlaylogsOf(IEnumerable<Team.PlaylogRow> rows, MapProject map) =>
        [.. rows.Where(r => map.TeamId != null && r.MapTeam.Length > 0 ? r.MapTeam == map.TeamId : r.MapName == map.Name)];

    /// <summary>A session as the picker lists it.</summary>
    private static string PlaylogLine(Team.PlaylogRow r) =>
        $"{(r.Started.Length >= 16 ? r.Started[..16].Replace('T', ' ') : r.Started)} · player {r.Player} · {TimeSpan.FromSeconds(r.Length):h\\:mm\\:ss} · {r.Deaths} deaths · {(r.Ended.Length > 0 ? r.Ended : "no end")}{(r.MapVersion > 0 ? $" · v{r.MapVersion}" : "")}";

    /// <summary>A session taken down into the playlogs folder (kept: an id names the same bytes for ever), or why not.</summary>
    internal static async Task<(string? File, string? Problem)> TakePlaylog(Uri server, Team.PlaylogRow row)
    {
        if (!Team.PlaylogId(row.Id))
        {
            return (null, "an id the team server does not make");
        }
        Directory.CreateDirectory(PlaylogFolder);
        string file = Path.Combine(PlaylogFolder, row.Id.Replace('/', '_')[..^".gz".Length]);
        if (File.Exists(file))
        {
            return (file, null);
        }
        if (await Team.DownloadPlaylog(server, row.Id, file) is string problem)
        {
            return (null, problem);
        }
        // ponytail: kept by count, the oldest written go; by bytes if sessions get big
        foreach (var old in new DirectoryInfo(PlaylogFolder).GetFiles("*.playlog").OrderByDescending(f => f.LastWriteTimeUtc).Skip(MostKeptPlaylogs))
        {
            old.Delete();
        }
        return (file, null);
    }
}
