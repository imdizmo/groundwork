using Avalonia.Controls;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Awaited before an area's zones are read (tests hold loads there to order them).
    /// </summary>
    internal Func<Task> BeforeLoad { get; set; } = () => Task.CompletedTask;

    /// <summary>
    /// Shows a map in the 3D editor, the models opened first if they are not yet.
    /// </summary>
    /// <param name="scene">The area or plot.</param>
    /// <param name="stillWanted">Asked once the models are open: false, nothing is shown.</param>
    /// <returns>Whether it was shown.</returns>
    internal async Task<bool> ShowEditor(WorldScene scene, Func<bool>? stillWanted = null)
    {
        if (_modelsLoading != null)
        {
            (_models, _modelsLoading) = (await _modelsLoading, null);
        }
        // Each map has its model folder: another one's models are opened when it is not the last map's.
        if (_models == null || _modelsFolder != ModelStore.MapModels)
        {
            _modelsFolder = ModelStore.MapModels;
            _models = await Task.Run(ModelStore.Open);
        }
        if (stillWanted?.Invoke() == false)
        {
            return false;
        }
        // The tape drawn on the last map is left behind.
        View.Tape.Clear();
        LoadedInfo.Text = scene.LoadInfo + (_models == null ? "\n" + Lang.T("world.msg.noModels") : "");
        View.Show(scene, _models);
        if (scene.Session is { } beds)
        {
            var models = _models;
            beds.MeshesOf = models == null ? null : prefab => ModelStore.ModelName(prefab) is { } name && models.LoadModel(name) is { } model
                ? (model.Parts.Select(p => (Mesh: models.LoadMesh(p.Mesh), Part: p)).Where(x => x.Mesh != null && x.Part.Sub < x.Mesh!.Submeshes.Length)
                    .Select(x => (x.Mesh!.Vertices, x.Mesh.Submeshes[x.Part.Sub], x.Part.Matrix)).ToList(), model.RootScale)
                : null;
            beds.BedAll();
        }
        // The last area's (or the plot's) point under the pointer is no point of this one: read again on the next move.
        ShowCursor(null);
        // The map's tools (and the running extensions').
        MapRailTools();
        if (scene.Session != null)
        {
            Edit(scene.Session);
        }
        if (scene.IsMap)
        {
            WatchInbox();
        }
        MapButton.IsVisible = true;
        TitleText.Text = scene.Name;
        ShowAreaInfo(scene);
        Title = $"{scene.Name} · Groundwork {BuildInfo.Version}";
        _pages.Content = _editorPage;
        EditorOpened();
        return true;
    }

    /// <summary>
    /// The line under the map's name: its size in metres and how many objects it holds now (made again after each
    /// change, as placing, deleting or undoing changes the count).
    /// </summary>
    /// <param name="scene">The map shown.</param>
    internal void ShowAreaInfo(WorldScene scene)
    {
        SubtitleText.Text = $"{_mapSize} · {scene.Things.Count(t => !t.Gone):N0} objects";
        // Read whole on hover where a narrow window ends it in "…".
        ToolTip.SetTip(SubtitleText, SubtitleText.Text);
    }
}
