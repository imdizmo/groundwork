using Avalonia.Threading;
using Groundwork.App;
using Taken = Groundwork.Desktop.Inbox.Taken;

namespace Groundwork.Desktop;

// The inbox (Inbox, docs/inbox.md): models other programs send come into the open map within 2 seconds of arriving, or when a map is
// opened; the 3D view and the objects drawer show them at once.
public sealed partial class MainWindow
{
    /// <summary>Whether deliveries are being taken in (another look waits for it).</summary>
    private bool _taking;

    /// <summary>Whether a delivery came while they were being taken in.</summary>
    private bool _takeAgain;

    /// <summary>Looks in the inbox every 2 seconds (made with the first map shown).</summary>
    private DispatcherTimer? _inboxTimer;

    /// <summary>Starts looking in the inbox (once) and takes in what waits there.</summary>
    // ponytail: a 2 s look at one folder, not a FileSystemWatcher (it missed renames here, and on shared drives)
    private void WatchInbox()
    {
        if (_inboxTimer == null)
        {
            _inboxTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _inboxTimer.Tick += (_, _) =>
            {
                if (!_taking && Inbox.Waiting().Count > 0)
                {
                    _ = TakeInbox();
                }
            };
            _inboxTimer.Start();
        }
        _ = TakeInbox();
    }

    /// <summary>Takes the waiting deliveries into the open map, then shows what they brought.</summary>
    internal async Task TakeInbox()
    {
        if (_taking)
        {
            _takeAgain = true;
            return;
        }
        _taking = true;
        try
        {
            do
            {
                _takeAgain = false;
                if (MapProject.Current is not { } map || Session is not { Scene.IsMap: true })
                {
                    return;
                }
                var taken = await Task.Run(() => Inbox.TakeAll(map));
                if (taken.Count == 0 || map != MapProject.Current)
                {
                    continue;
                }
                var models = taken.SelectMany(t => t.Models).ToList();
                if (models.Count > 0)
                {
                    await ModelsSentIn(models);
                }
                // One line for all that came in at once: what was added, by program, then what was refused.
                static string From(Taken t) => t.Source.Length > 0 ? t.Source : "another program";
                var said = taken.Where(t => t.Error == null).GroupBy(From).Select(g => g.Sum(t => t.Models.Count) is int n ? $"{n} model{(n == 1 ? "" : "s")} from {g.Key} added to the map: in the objects drawer" : "")
                    .Concat(taken.Where(t => t.Error != null).Select(t => $"Models sent from {From(t)} were refused: {t.Error}"));
                Msg(Said.Raw(string.Join(" · ", said)));
            }
            while (_takeAgain);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Run without being awaited: what goes wrong is said, never lost.
            Log.Caught(e, "taking in models sent to the inbox");
            Msg(Said.Raw($"Models sent to Groundwork could not be taken in: {e.Message}"));
        }
        finally
        {
            _taking = false;
        }
    }

    /// <summary>The models folder and catalog read again: the view draws the new models, the drawer offers them.</summary>
    private async Task ModelsSentIn(List<string> models)
    {
        ModelStore.Rescan();
        _modelsFolder = ModelStore.MapModels;
        _models = await Task.Run(ModelStore.Open);
        View.SwapModels(_models);
        ObjectKinds.UseCategories(MapTools.Catalog().Kinds.Select(k => (k.Model, k.Category)));
        // The tools too: their kind lists must match the drawer's, whose picks they take by index.
        MapRailTools();
        RenewDrawer();
        // The drawer finds by label: one model shown alone, several on All.
        _drawer?.Reveal(models.Count == 1 ? MapTools.Catalog().Kinds.FirstOrDefault(k => k.Model == models[0])?.Label ?? "" : "");
    }
}
