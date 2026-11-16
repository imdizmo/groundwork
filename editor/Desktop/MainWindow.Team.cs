using System.IO.Compression;
using Groundwork.App;

namespace Groundwork.Desktop;

// The team server: the open map published to it (packed with its models), a teammate's map taken from it.
public partial class MainWindow
{
    /// <summary>The team server's base, or null (said why) when none is set or it is not an address.</summary>
    private async Task<Uri?> TeamServer()
    {
        if (Team.Address(Settings) is not { } a)
        {
            await Tell("No team server is set: Settings → Team, or start Groundwork from the Jatify Launcher.");
            return null;
        }
        if (Team.Base(a) is not { } server)
        {
            await Tell(Lang.T("settings.badTeam"));
            return null;
        }
        return server;
    }

    /// <summary>A call to the team server, again while the person asks (Retry) after a failure; null error when it went.</summary>
    private async Task<(T? Value, string? Error)> Retrying<T>(Func<Task<(T? Value, string? Error)>> call)
    {
        while (true)
        {
            var got = await call();
            if (got.Error == null || !await Ask("Team server", got.Error, "Retry", "Carry on offline"))
            {
                return got;
            }
        }
    }

    /// <summary>The open map up to the team, with the models its objects use: a new map, or a new version of the one it came from.</summary>
    private async Task PublishMap()
    {
        if (MapProject.Current is not { } map || await TeamServer() is not { } server)
        {
            return;
        }
        if (MapDirty && (!await Ask("Publish to the team", "The map has changes not saved; the team gets the map as saved.", "Save and publish", Lang.T("common.cancel")) || !await SaveMap(quiet: true)))
        {
            return;
        }
        var (who, said) = await Retrying(() => Team.WhoAmI(server));
        if (who is not { Role: not null })
        {
            Msg(new Said($"Not published: {(who is { } w ? Team.Said(w) : said)}", ""));
            return;
        }
        var (_, count, _) = MapPack.Used(map);
        // an id from map.json (a teammate's pack, or edited by hand) that is not plain: published as a new map
        bool known = Team.PlainId(map.TeamId);
        string again = known ? $" It is version {map.TeamVersion + 1} of the team's map." : "";
        if (!await Ask("Publish to the team", $"{Team.Said(who)}\n\n{map.Name} goes to the team server at {server.Authority} with the {count} models its objects use; everyone on the team can open it.{again}", "Publish", Lang.T("common.cancel")))
        {
            return;
        }
        string id = known ? map.TeamId! : Guid.NewGuid().ToString("N");
        string zip = Path.Combine(Directory.CreateDirectory(Path.Combine(AppSettings.DataDir, "team")).FullName, $"{id}.zip");
        try
        {
            Msg(new Said("Packing the map for the team...", ""));
            var packed = await Task.Run(() => MapPack.Write(map, zip, withModels: true));
            Msg(new Said($"Sending {map.Name} to the team ({new FileInfo(zip).Length / 1e6:0.#} MB)...", ""));
            var (row, error) = await Retrying(() => Team.Publish(server, id, map.Name, zip, known ? map.TeamVersion : 0));
            if (row == null)
            {
                Msg(new Said($"Not published: {error}", ""));
                return;
            }
            map.SetTeam(id, row.Version);
            string left = packed.Left.Count == 0 ? "" : $" Left out: {string.Join("; ", packed.Left.Take(5))}{(packed.Left.Count > 5 ? $" and {packed.Left.Count - 5} more" : "")}.";
            Msg(new Said($"Published to the team: {map.Name}, version {row.Version}, {packed.Models} models.{left}", ""));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Tell("The map could not be published: " + ex.Message);
        }
        finally
        {
            File.Delete(zip);
        }
    }

    /// <summary>A map the team keeps, chosen from its list, down into a folder picked and opened.</summary>
    private async Task OpenFromTeam()
    {
        if (await TeamServer() is not { } server)
        {
            return;
        }
        var (who, said) = await Retrying(() => Team.WhoAmI(server));
        if (who == null)
        {
            Msg(new Said($"The team's maps could not be listed: {said}", ""));
            return;
        }
        var (maps, error) = await Retrying(() => Team.Maps(server));
        if (maps == null)
        {
            Msg(new Said($"The team's maps could not be listed: {error}", ""));
            return;
        }
        if (maps.Count == 0)
        {
            await Tell("The team has no maps yet: open one of yours and choose ⋯ → Publish to the team.");
            return;
        }
        var lines = maps.Select(m => $"{m.Name}   v{m.Version} · {m.Size / 1e6:0.#} MB · {m.By}{(m.At.Length >= 10 ? " · " + m.At[..10] : "")}").ToList();
        if (await Dialogs.Choose(this, "Open a map from the team", $"{Team.Said(who)}\n\nThe maps your team keeps (the newest version of each):", lines, "Open") is not int i
            || await Pickers.Folder(StorageProvider, "Put the team's map in") is not { } under)
        {
            return;
        }
        var chosen = maps[i];
        string temp = Path.Combine(under, $".{chosen.Id}.{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(temp);
            Msg(new Said($"Taking {chosen.Name} from the team ({chosen.Size / 1e6:0.#} MB)...", ""));
            string zip = Path.Combine(temp, "map.zip");
            if (await Retrying(async () => (0, await Team.Download(server, chosen.Id, zip))) is (_, { } failed))
            {
                Msg(new Said($"Not taken: {failed}", ""));
                return;
            }
            string folder = await Task.Run(() => Unpack(zip, temp, under, chosen.Name));
            MapProject.Open(folder).SetTeam(chosen.Id, chosen.Version);
            await OpenMap(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            await Tell("The team's map could not be opened: " + ex.Message);
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    /// <summary>
    /// A pack's map put in a folder of its own under <paramref name="under"/>, named after it (" 2", " 3" when taken):
    /// the pack holds one folder with a map.json; nothing in it may land outside (the extraction refuses).
    /// </summary>
    internal static string Unpack(string zip, string temp, string under, string name)
    {
        string into = Path.Combine(temp, "x");
        ZipFile.ExtractToDirectory(zip, into);
        string[] tops = Directory.GetDirectories(into);
        if (tops.Length != 1 || Directory.GetFiles(into).Length > 0 || !File.Exists(Path.Combine(tops[0], MapProject.FileName)))
        {
            throw new InvalidDataException("the pack is not one map folder with a map.json");
        }
        string safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim('.', ' ') is { Length: > 0 } s ? s : "Team map";
        string folder = Path.Combine(under, safe);
        for (int n = 2; Directory.Exists(folder) || File.Exists(folder); n++)
        {
            folder = Path.Combine(under, $"{safe} {n}");
        }
        Directory.Move(tops[0], folder);
        return folder;
    }
}
