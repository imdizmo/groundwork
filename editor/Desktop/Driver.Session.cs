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
