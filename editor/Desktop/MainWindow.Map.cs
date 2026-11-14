using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Groundwork.App;

namespace Groundwork.Desktop;

// A map in the window: opened from the start page, every ground tool on it, Save writes the heightmap, Start goes
// back to the start page.
public partial class MainWindow
{
    /// <summary>Whether the map is open.</summary>
    internal bool InMap { get; private set; }

    /// <summary>How many objects the last save wrote.</summary>
    private int _mapObjects;

    /// <summary>The ground's heights when the objects last stood on it (opened, or settled): Settle moves them by the change since.</summary>
    private float[]? _mapGround;

    /// <summary>Send to Unreal and Settle objects, on the top bar while the map is open.</summary>
    private Button? _moreButton, _layersButton, _checksButton, _objectsButton;

    private Avalonia.Controls.Shapes.Ellipse? _liveDot;

    private TextBlock? _liveStatus;

    private ModToolPanel? _ownRight;

    private Border? _selectionBar;

    /// <summary>The objects drawer along the bottom (ObjectsDrawer), made when the map is first opened.</summary>
    private ObjectsDrawer? _drawer;

    /// <summary>The rail and the tool cards (they end above the drawer).</summary>
    private readonly Panel _toolColumn = null!;

    private TextBlock? _selectionCount;

    private ModToolContext? _ownRightContext;

    private ModExtensions.Tool? _ownRightTool;

    private List<ModExtensions.Tool> _ownRightTools = [];

    /// <summary>The Checks button says how many problems the last check found.</summary>
    internal void ChecksFound(int n) => _checksButton?.Content = n > 0 ? $"Checks · {n}" : "Checks";

    /// <summary>Grow, chosen from the ⋯ menu.</summary>
    private ModExtensions.Tool? _growTool;

    /// <summary>Live in Unreal (UnrealLive): on while set.</summary>
    private UnrealLive? _live;

    private Avalonia.Controls.Primitives.ToggleButton? _liveButton;
    private Button? _playButton;

    /// <summary>While live: looks every 1.5 s for changes made in Unreal.</summary>
    private Avalonia.Threading.DispatcherTimer? _pullTimer;

    /// <summary>The map changed since it was last saved.</summary>
    internal bool MapDirty => InMap && Session != null && (UnsavedSteps > 0 || MapWater.Snapshot() != _savedWater || MapAreas.Snapshot() != _savedAreas || MapCaves.Snapshot() != _savedCaves);

    /// <summary>The history's top step and length at the last save (or open), and the water and areas then.</summary>
    private (EditSession.Change? Top, int Count) _saved;
    private string _savedWater = "", _savedAreas = "", _savedCaves = "";

    /// <summary>How many steps the history is from the saved state (undoing back to it: none).</summary>
    private int UnsavedSteps => Session is { } s ? EditSession.UnsavedSteps(s.UndoList, _saved.Top, _saved.Count) : 0;

    /// <summary>The map as it is now is what its files hold.</summary>
    private void MarkSaved(EditSession s)
    {
        _saved = (s.TopStep, s.UndoList.Count);
        (_savedWater, _savedAreas, _savedCaves) = (MapWater.Snapshot(), MapAreas.Snapshot(), MapCaves.Snapshot());
    }

    /// <summary>Opens a map (null: the current one again); it is the current map once it is open.</summary>
    internal async Task ShowMap(MapProject? chosen = null)
    {
        // Another map open with changes not saved: asked first.
        if (InMap && MapDirty && !await Ask("Open another map?", "The open map has changes not saved.", Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        WorldScene scene;
        MapProject map;
        var was = MapProject.Current;
        try
        {
            map = chosen ?? MapProject.Current ?? throw new IOException("no map chosen");
            // Read on a worker, the window drawing meanwhile: each step under the veil and in the status bar.
            _busyText.Text = Lang.T("map.read.start", map.Name);
            _busy.IsVisible = true;
            var step = new Progress<string>(said => { _busyText.Text = said; Msg(Said.Raw(said)); });
            var clock = Stopwatch.StartNew();
            scene = await Task.Run(() => map.Read(step, extras: false));
            _busyText.Text = Lang.T("map.read.water");
            map.LoadExtras();
            Options.Say($"{map.Name} read in {clock.ElapsedMilliseconds} ms");
            var raster = map.ReadRaster();
            _mapSize = $"{raster.Cols - 1} × {raster.Rows - 1} m";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MapProject.Current = was;
            await Tell("The map could not be read: " + ex.Message);
            return;
        }
        finally
        {
            _busy.IsVisible = false;
        }
        MapProject.Current = map;
        RememberMap(map.Folder);
        if (!map.HasUnrealLevel && _liveButton != null)
        {
            _liveButton.IsChecked = false;
        }
        MapWater.Changed -= ShowWater;
        MapWater.Changed += ShowWater;
        await ShowEditor(scene);
        ShowWater();
        TitleText.Text = map.HasUnrealLevel ? map.Name : map.Name + " (no Unreal level)";
        InMap = true;
        MarkSaved(scene.Session!);
        MapButton.Content = Icons.With("back", Lang.T("top.label.start"));
        // The map's objects have ids of their own (negative, as new ones): no "not saved yet" marks on them.
        View.ShowNewMarkers = false;
        Brush.MapOpen = true;
        Tools.RelabelPaints();
        LayerMaskNames(true);
        Tools.ChooseSelect();
        _mapGround = (float[])scene.Heights.Clone();
        MapButtons(true);
        Tools.SetMap(true);
        MapRailTools();
        UpdateSaveBar();
        // The map first: no right-hand panel open (the choice kept for the game's worlds).
        string right = Prefs.Get("panel.right", "view");
        ShowRight(null);
        Prefs.Set("panel.right", right);
        Msg(new Said("", ""));
    }

    /// <summary>New map: its name, size and ground in the generator's window, made in the maps folder (Settings) and opened.</summary>
    private async Task NewMap(string? heightmap = null)
    {
        if (await GeneratorWindow.NewMap(this, heightmap) is not { } asked)
        {
            return;
        }
        MapProject made;
        try
        {
            made = asked.Spec is { } spec
                ? MapProject.CreateZone(asked.Name, asked.Chunks, spec, ZoneGenerator.Planting.From(MapTools.Catalog().Kinds), NewMapsFolder(), MapProject.Current, asked.Ground, asked.ChunksZ)
                : MapProject.Create(asked.Name, asked.Chunks, asked.Rolling, asked.Rolling ? 3f : 1f, asked.Seed, NewMapsFolder(), MapProject.Current);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            await Tell(ex.Message);
            return;
        }
        await ShowMap(made);
    }

    /// <summary>
    /// Make it again: a generated map not yet worked on, made again from the numbers asked in the generator's window
    /// (its last numbers kept for Undo make it again) and opened again.
    /// </summary>
    private async Task MakeAgain(ZoneGenerator.Spec? spec = null)
    {
        if (MapProject.Current is not { Generated: { } g } m || !m.Untouched || MapDirty)
        {
            await Tell("Only a generated map not yet worked on can be made again: this one has been changed by hand (or was not generated).");
            return;
        }
        if ((spec ?? await GeneratorWindow.Again(this, m, g.Spec)) is not { } asked)
        {
            return;
        }
        try
        {
            Msg(new Said("Making the map again...", ""));
            m.Generate(asked, ZoneGenerator.Planting.From(MapTools.Catalog().Kinds));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            await Tell(ex.Message);
            return;
        }
        _madeFrom = (m.Folder, g.Spec);
        await ShowMap(MapProject.Open(m.Folder));
    }

    /// <summary>The map made again last and the numbers it was made from before (Undo make it again).</summary>
    private (string Folder, ZoneGenerator.Spec Spec)? _madeFrom;

    /// <summary>The map's rivers and lakes drawn again (and the save bar: they count as changes).</summary>
    private void ShowWater()
    {
        if (Session?.Scene is { IsMap: true } s)
        {
            View.SetWater(s, MapWater.Shapes());
            UpdateSaveBar();
        }
    }

    /// <summary>Grow map: saves, makes the map's files bigger, opens it again.</summary>
    internal async Task GrowMap(MapProject.Sides sides, int metres, bool generated, float flatHeight, int seed)
    {
        if (!InMap || Session is not { } s || MapProject.Current is not { } map)
        {
            return;
        }
        try
        {
            _ = map.Save(s.Scene);
            map.Grow(sides, metres, generated, flatHeight, seed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await Tell("The map could not grow: " + ex.Message);
            return;
        }
        MarkSaved(s);
        InMap = false;
        Session = null;
        await ShowMap();
        var m = map.ReadRaster();
        Msg(new Said($"{map.Name} is now {m.Cols} x {m.Rows} m (saved; the old files are kept as .bak).", $"{map.Name} is now {m.Cols} x {m.Rows} m."));
    }

    /// <summary>The Mask panel's paint rules named after the open map's layers (or numbered again).</summary>
    private void LayerMaskNames(bool map)
    {
        int keep = MaskPanel.PaintBox.SelectedIndex;
        string[] painted = map && MapProject.Current is { } open
            ? [.. Enumerable.Range(1, Math.Max(3, open.Layers.Count - 1)).Select(k => open.Layers.ElementAtOrDefault(k)?.Name ?? Lang.T("mask.paint.layer", k + 1))]
            : [.. Enumerable.Range(1, 3).Select(k => Lang.T("mask.paint.layer", k + 1))];
        MaskPanel.PaintBox.ItemsSource = (string[])[Lang.T("mask.paint.any"), Lang.T("mask.paint.unpainted"), Lang.T("mask.paint.painted"), .. painted];
        MaskPanel.PaintBox.SelectedIndex = keep < 3 + painted.Length ? keep : 0;
    }

    /// <summary>
    /// The map's landscape made in the open Unreal level from its heights as they are now; a level that has one already is
    /// asked about first (rebuilt at the map's size, its material, edit layers and water kept).
    /// </summary>
    /// <summary>
    /// The open map linked to an Unreal project the user picks (its .uproject): map.json's unreal.project written in
    /// place, nothing else in the file touched. True when the map has a project after it.
    /// </summary>
    /// <param name="change">Pick again even when the map has one.</param>
    private async Task<bool> LinkUnreal(bool change = false)
    {
        if (MapProject.Current is not { } map)
        {
            return false;
        }
        if (map.HasUnrealLevel && !change)
        {
            return true;
        }
        string? file = await Pickers.File(StorageProvider, "The Unreal project for this map (its .uproject)", new("Unreal project") { Patterns = ["*.uproject"] });
        if (file == null)
        {
            return map.HasUnrealLevel;
        }
        map.LinkUnreal(Path.GetDirectoryName(file)!);
        TitleText.Text = map.Name;
        _playButton?.IsVisible = true;
        Msg(new Said($"This map now goes to the Unreal project {Path.GetFileName(file)}. Open the level you want it in, in Unreal.", ""));
        return true;
    }

    private async Task MakeLandscape()
    {
        if (Session is not { Scene: var scene } || MapProject.Current is not { } map || !await LinkUnreal())
        {
            return;
        }
        string said = await UnrealLive.MakeLandscape(map, scene, replace: false);
        if (said.Contains("has one already", StringComparison.Ordinal)
            && await Ask("Unreal landscape", "The open level has a landscape already. Rebuild it from this map, at the map's size? Its material, edit layers and water stay; its heights and paint are the map's. Nothing is saved in Unreal until you save the level.", "Rebuild it", "Leave it"))
        {
            said = await UnrealLive.MakeLandscape(map, scene, replace: true);
        }
        Msg(new Said(said, ""));
    }

    /// <summary>The map's heights as they are now into a 16-bit PNG or raw .r16 (Unreal's import formats), where the user picks.</summary>
    private async Task ExportHeightmap()
    {
        if (Session is not { Scene: var scene } || MapProject.Current is not { } map)
        {
            return;
        }
        string? file = await Pickers.SaveFile(StorageProvider, "Export the heightmap", map.Name + "_height.png",
            [new("16-bit PNG") { Patterns = ["*.png"] }, new("Raw 16-bit (.r16)") { Patterns = ["*.r16", "*.raw"] }]);
        if (file == null)
        {
            return;
        }
        var (heights, cols, rows) = map.HeightsNow(scene);
        HeightmapExport.Write(file, heights, cols, rows);
        Msg(new Said($"Heightmap written: {Path.GetFileName(file)}, {cols} x {rows}, 0 m = 32768, 128 steps a metre (Unreal at Z scale 100).", ""));
    }

    /// <summary>The map's terrain maps (TerrainMaps: flow, wetness, slope and the rest) as 8-bit grey PNGs into a folder the user picks.</summary>
    private async Task ExportTerrainMaps()
    {
        if (Session is not { Scene: var scene } || MapProject.Current is not { } map)
        {
            return;
        }
        string? folder = await Pickers.Folder(StorageProvider, "Export the terrain maps into");
        if (folder == null)
        {
            return;
        }
        var (heights, cols, rows) = map.HeightsNow(scene);
        float[]? worn = map.ReadWorn(cols, rows);
        var names = new List<string>();
        // names from map.json (the map's, its layers') as file names: no folders, nothing the system refuses
        static string Safe(string name, string otherwise) =>
            string.Concat(Path.GetFileName(name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim('.', ' ') is { Length: > 0 } s ? s : otherwise;
        string stem = Safe(map.Name, "map");
        var (weights, _, _) = map.LayersNow(scene);
        await Task.Run(() =>
        {
            foreach (var (name, values) in TerrainMaps.Of(heights, cols, rows, map.SeaLevel, worn, map.Generated?.Spec.SnowLine is > 0 and var line ? line : null))
            {
                File.WriteAllBytes(Path.Combine(folder, $"{stem}_{name}.png"), HeightmapExport.Png8(values, cols, rows));
                names.Add(name);
            }
            // the paint: each layer's weight, every point's adding up to white (as Unreal's landscape layers take them)
            for (int k = 0; k < weights.Length; k++)
            {
                byte[] w = weights[k];
                string layer = $"paint_{Safe(map.Layers[k].Name, $"layer{k + 1}")}";
                File.WriteAllBytes(Path.Combine(folder, $"{stem}_{layer}.png"), HeightmapExport.Png8([.. w.Select(b => b / 255f)], cols, rows));
                names.Add(layer);
            }
        });
        Msg(new Said($"Terrain maps written to {folder}: {string.Join(", ", names)} ({cols} x {rows}).", ""));
    }

    /// <summary>The map the drawer holds the kinds of.</summary>
    private string? _drawerFor;

    /// <summary>A middle click on an object: its kind picked in the drawer, to place more of it.</summary>
    private void PickKindOfObject(int thing)
    {
        if (Session?.Scene is { } scene && thing < scene.Things.Count && _drawer != null)
        {
            var t = scene.Things[thing];
            Msg(new Said(_drawer.PickKindOf(t.Prefab) is string kind ? $"Picked {kind}: click to place more" : $"{ThingName(t)} is not a kind of the catalog", ""));
        }
    }

    /// <summary>A Shift + right click on an object: it is deleted (one step in History).</summary>
    private void DeleteObject(int thing)
    {
        if (Session is { } s && thing < s.Scene.Things.Count)
        {
            string name = ThingName(s.Scene.Things[thing]);
            _ = s.Commit("Delete " + name, null, [thing], []);
            Msg(new Said($"Deleted {name}", ""));
            UpdateSaveBar();
        }
    }

    /// <summary>The objects drawer for the map open, the panels above it moving with it.</summary>
    private ObjectsDrawer NewDrawer()
    {
        var d = new ObjectsDrawer(this);
        _drawerFor = MapProject.Current?.Folder;
        d.Card.SizeChanged += (_, _) => AboveDrawer();
        d.Card.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                AboveDrawer();
            }
        };
        return d;
    }

    /// <summary>Play here: the game started in Unreal at the ground the camera looks at (Walk and Fly: below the camera).</summary>
    private async Task PlayHere()
    {
        if (Session is not { Scene: var scene })
        {
            return;
        }
        var (yaw, _, _, target) = View.Camera;
        var at = View.Eye == GlView.EyeMode.Orbit ? target : View.EyePosition;
        float x = at.X + scene.Cx, z = -at.Z + scene.Cz;
        string said = await UnrealLive.Play(new System.Numerics.Vector3(x, new SceneLayers.AreaView(scene).GroundHeight(x, z), z), yaw);
        Msg(new Said(said, ""));
    }

    /// <summary>Send to Unreal and Settle objects on the top bar (made once, beside Save), shown or hidden.</summary>
    private void MapButtons(bool show)
    {
        if (_liveButton == null && SaveButton.Parent is Panel bar)
        {
            // Live: a pill with its state (UnrealLive)
            _liveDot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = Brushes.Gray, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _liveStatus = new TextBlock { Text = "off", Foreground = Ui.Muted, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _liveButton = new Avalonia.Controls.Primitives.ToggleButton
            {
                Content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 7, Children = { _liveDot, new TextBlock { Text = "Live", FontWeight = FontWeight.SemiBold, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, _liveStatus } },
                CornerRadius = new Avalonia.CornerRadius(14),
                Padding = new Avalonia.Thickness(10, 4),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            ToolTip.SetTip(_liveButton, "Live with Unreal, both ways: what you change here goes into the open Unreal level as you make it, and what is changed there comes here. Turning it on sends what Unreal does not have yet first.");
            _liveButton.IsCheckedChanged += async (_, _) => await SetLive(_liveButton.IsChecked == true);
            int at = bar.Children.IndexOf(SaveButton);
            bar.Children.Insert(at, _liveButton);
            // Play here: the game started in Unreal where the camera looks
            _playButton = new Button { Content = "▶ Play here", FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(_playButton, "Starts the game in the open Unreal level (Play In Editor) at the ground the camera looks at (Walk: where you stand), facing the camera's way. Needs the Groundwork Bridge plugin; stop it in Unreal (Esc).");
            _playButton.Click += async (_, _) => await PlayHere();
            bar.Children.Insert(at + 1, _playButton);
            // ⋯: what is not used often
            _moreButton = new Button { Content = "⋯", FontSize = 15, Padding = new Avalonia.Thickness(10, 2), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(_moreButton, "More: send everything to Unreal, settle objects on changed ground");
            var send = new MenuItem { Header = "Send everything to Unreal (full rebuild, about a minute)" };
            send.Click += async (_, _) => await SendToUnreal();
            var settle = new MenuItem { Header = "Settle objects on ground that changed" };
            settle.Click += (_, _) => SettleObjects();
            var grow = new MenuItem { Header = "Grow the map…" };
            grow.Click += (_, _) =>
            {
                if (_growTool != null)
                {
                    Tools.ChooseMod(_growTool);
                }
            };
            var export = new MenuItem { Header = "Export the heightmap (16-bit PNG or .r16)…" };
            export.Click += async (_, _) => await ExportHeightmap();
            var terrainMaps = new MenuItem { Header = "Export the terrain maps and paint weights…" };
            terrainMaps.Click += async (_, _) => await ExportTerrainMaps();
            var landscape = new MenuItem { Header = "Make the Unreal landscape from this map…" };
            landscape.Click += async (_, _) => await MakeLandscape();
            var models = new MenuItem { Header = "Bring the models into Unreal" };
            models.Click += async (_, _) =>
            {
                if (!await LinkUnreal())
                {
                    return;
                }
                Msg(new Said("Bringing the models into Unreal...", ""));
                Msg(new Said(await UnrealLive.ImportModels(ModelStore.MapModels), ""));
            };
            // a generated map not worked on yet: made again from new numbers, or back to the last ones
            var again = new MenuItem { Header = "Make it again with other numbers…" };
            again.Click += async (_, _) => await MakeAgain();
            var undoAgain = new MenuItem { Header = "Undo make it again" };
            undoAgain.Click += async (_, _) => await MakeAgain(_madeFrom?.Spec);
            var link = new MenuItem();
            link.Click += async (_, _) => await LinkUnreal(change: true);
            var more = new MenuFlyout { Items = { again, undoAgain, send, settle, grow, export, terrainMaps, link, landscape, models } };
            more.Opening += (_, _) =>
            {
                link.Header = MapProject.Current?.UnrealProject is { } up
                    ? $"Unreal project: {Path.GetFileName(up)} (change…)"
                    : "Link to an Unreal project…";
                again.IsVisible = MapProject.Current is { Untouched: true } && !MapDirty;
                undoAgain.IsVisible = again.IsVisible && _madeFrom?.Folder == MapProject.Current?.Folder;
            };
            _moreButton.Flyout = more;
            bar.Children.Insert(bar.Children.IndexOf(SaveButton) + 1, _moreButton);
            // Layers and Checks: right-hand panels like History
            _layersButton = new Button { Content = "Layers", FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _layersButton.Click += (_, _) => ShowOwnRight("layers");
            _checksButton = new Button { Content = "Checks", FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _checksButton.Click += (_, _) => ShowOwnRight("checks");
            _objectsButton = new Button { Content = "Objects", FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _objectsButton.Click += (_, _) => ShowOwnRight("objects");
            int h = bar.Children.IndexOf(HistoryButton);
            bar.Children.Insert(h, _objectsButton);
            bar.Children.Insert(h, _checksButton);
            bar.Children.Insert(h, _layersButton);
            foreach (var other in new[] { HistoryButton, ViewButton, HelpButton })
            {
                other.Click += (_, _) => ShowOwnRight(null);
            }
            _ownRight = new ModToolPanel(Prefs);
            _ownRightContext = new ModToolContext(View, _ownRight, Msg);
            _ownRight.Changed += id =>
            {
                if (_ownRightTool is { } t)
                {
                    t.Control(_ownRightContext, id);
                    UpdateSaveBar();
                }
            };
            _ownRight.Card.Margin = new Avalonia.Thickness(10, 70, 10, 58);
            _ownRight.Card.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            _ownRight.Card.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            _ownRight.Card.IsVisible = false;
            var page = (Grid)_editorPage;
            page.Children.Insert(page.Children.IndexOf(HelpCard) + 1, _ownRight.Card);
            // The selection's actions, floating over the view while anything is selected
            Button Act(string text, string tip, Action click, bool danger = false)
            {
                var b = new Button { Content = text, FontSize = 12, Padding = new Avalonia.Thickness(10, 4) };
                if (danger)
                {
                    b.Foreground = new SolidColorBrush(Color.FromRgb(240, 128, 112));
                }
                ToolTip.SetTip(b, tip);
                b.Click += (_, _) => click();
                return b;
            }
            _selectionCount = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Avalonia.Thickness(4, 0, 6, 0) };
            _selectionBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 27, 30, 36)),
                BorderBrush = Ui.Line,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(10),
                Padding = new Avalonia.Thickness(8, 6),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Avalonia.Thickness(0, 0, 0, 52),
                IsVisible = false,
                Child = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        _selectionCount,
                        Act("Drop to ground", "Each selected object down onto the ground under it (End)", () => { View.SelectTool.Drop(); UpdateSaveBar(); }),
                        Act("Settle", "The selected objects onto ground that changed since the map was opened", SettleObjects),
                        Act("Save as stamp", "The selection as a named stamp: the Stamp tool then offers it", () => _ = SaveStampAsked()),
                        Act("Duplicate", "Copy the selection and place the copy (Ctrl+C, Ctrl+V)", () => { Copy(); StartPaste(); }),
                        Act("Delete", "Delete the selection (Del)", () => { View.SelectTool.Delete(); UpdateSaveBar(); }, danger: true),
                    },
                },
            };
            page.Children.Insert(page.Children.IndexOf(_ownRight.Card), _selectionBar);
            _cameraBar = CameraBar();
            page.Children.Insert(page.Children.IndexOf(_selectionBar), _cameraBar);
            _drawer = NewDrawer();
            page.Children.Insert(page.Children.IndexOf(_selectionBar), _drawer.Card);
            Minimap = new Minimap(this);
            page.Children.Insert(page.Children.IndexOf(_selectionBar), Minimap.Card);
            Minimap.Card.SizeChanged += (_, _) => AboveDrawer();
            View.SelectionChanged += things =>
            {
                _selectionBar.IsVisible = Session is { Scene.IsMap: true } && things.Count > 0;
                // How many, and of what (the catalog's names, the most first).
                var kinds = things.GroupBy(t => MapTools.GroupOf(t.Prefab).Label).OrderByDescending(g => g.Count()).ToList();
                _selectionCount.Text = $"{things.Count} selected" + (kinds.Count switch
                {
                    0 => "",
                    1 => $" · {kinds[0].Key}",
                    _ => " · " + string.Join(", ", kinds.Take(2).Select(g => $"{g.Key} ×{g.Count()}")) + (kinds.Count > 2 ? $" +{kinds.Count - 2} more" : ""),
                });
            };
        }
        // another map: its own kinds in the drawer
        if (show && _drawer is { Card.Parent: Panel holder } old && _drawerFor != MapProject.Current?.Folder)
        {
            int at = holder.Children.IndexOf(old.Card);
            holder.Children.RemoveAt(at);
            _drawer = NewDrawer();
            holder.Children.Insert(at, _drawer.Card);
        }
        _drawer?.Card.IsVisible = show;
        _cameraBar?.IsVisible = show;
        Minimap?.Card.IsVisible = show;
        ModToolPanel.PalettesElsewhere = show;
        _liveButton?.IsVisible = show;
        _playButton?.IsVisible = show && MapProject.Current?.HasUnrealLevel == true;
        _moreButton?.IsVisible = show;
        _layersButton?.IsVisible = show;
        _checksButton?.IsVisible = show;
        _objectsButton?.IsVisible = show;
        if (!show)
        {
            _selectionBar?.IsVisible = false;
        }
        SelectPanel.SetMap(show);
        if (!show)
        {
            ShowOwnRight(null);
            _liveButton?.IsChecked = false;
        }
    }

    /// <summary>The open map's size, "505 × 505 m" (the line under its name).</summary>
    private string _mapSize = "";

    /// <summary>The map in the view's corner (Minimap), made once.</summary>
    private Minimap? Minimap { get; set; }

    /// <summary>Orbit / Top / Walk over the view (the map), made once.</summary>
    private Border? _cameraBar;

    /// <summary>
    /// The camera's three ways over the view: Orbit (round a point), Top (straight down, the map as a plan) and Walk
    /// (at a player's eye height; F cycles on to flying as before).
    /// </summary>
    private Border CameraBar()
    {
        var buttons = new List<(string Name, Avalonia.Controls.Primitives.ToggleButton Button)>();
        void Eye(GlView.EyeMode want)
        {
            for (int i = 0; i < 3 && View.Eye != want; i++)
            {
                View.CycleEye();
            }
        }
        void Mark()
        {
            bool top = View.Eye == GlView.EyeMode.Orbit && View.WorldCamera is { Pitch: > 1.45f };
            foreach (var (name, b) in buttons)
            {
                b.IsChecked = name switch { "Top" => top, "Orbit" => View.Eye == GlView.EyeMode.Orbit && !top, _ => View.Eye != GlView.EyeMode.Orbit };
            }
        }
        foreach (var (name, tip, act) in new (string, string, Action)[]
        {
            ("Orbit", "Turn round a point: right drag turns, wheel zooms", () =>
            {
                Eye(GlView.EyeMode.Orbit);
                if (View.WorldCamera is { Pitch: > 1.45f } c)
                {
                    View.WorldCamera = (c.Yaw, 0.9f, c.Distance, c.World);
                }
            }),
            ("Top", "Straight down: the map as a plan", () =>
            {
                Eye(GlView.EyeMode.Orbit);
                if (View.WorldCamera is { } c)
                {
                    View.WorldCamera = (0, 1.55f, Math.Max(c.Distance, 160), c.World);
                }
            }),
            ("Walk", "At a player's eye height: WASD walks, F flies", () => Eye(GlView.EyeMode.Walk)),
        })
        {
            var b = new Avalonia.Controls.Primitives.ToggleButton { Content = name, FontSize = 12, Padding = new Avalonia.Thickness(12, 4) };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) =>
            {
                act();
                Mark();
            };
            buttons.Add((name, b));
        }
        View.EyeChanged += _ => Mark();
        Mark();
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 27, 30, 36)),
            BorderBrush = Ui.Line,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, 70, 0, 0),
            Child = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { buttons[0].Button, buttons[1].Button, buttons[2].Button } },
        };
    }

    /// <summary>The tool column, the right-hand panels and the selection bar end above the drawer while it shows.</summary>
    private void AboveDrawer()
    {
        double bottom = 58 + (_drawer is { Card.IsVisible: true } d ? d.Card.Bounds.Height + 8 : 0);
        _toolColumn.Margin = new Avalonia.Thickness(10, 70, 10, bottom);
        Minimap?.Card.Margin = new Avalonia.Thickness(0, 0, 10, bottom);
        double map = Minimap is { Card.IsVisible: true } m ? m.Card.Bounds.Height + 8 : 0;
        foreach (var right in new Control?[] { ViewPanelCard, History.Card, HelpCard, _ownRight?.Card })
        {
            right?.Margin = new Avalonia.Thickness(10, 70, 10, bottom + map);
        }
        _selectionBar?.Margin = new Avalonia.Thickness(0, 0, 0, bottom - 6);
    }

    /// <summary>The editor's own tools on the rail (Layers and Checks go to the right-hand panels).</summary>
    private void MapRailTools()
    {
        var own = MapTools.For(this);
        _ownRightTools = [.. own.Where(t => t.Info.Id is "layers" or "checks" or "objects")];
        // Grow is rare (it changes the map's size): in the ⋯ menu, not on the rail.
        _growTool = own.FirstOrDefault(t => t.Info.Id == "grow");
        Tools.SetModTools([.. ModExtensions.Tools, .. own.Where(t => t.Info.Id is not ("layers" or "checks" or "objects" or "grow"))]);
    }

    /// <summary>Layers or Checks in the right-hand panel (null: hidden).</summary>
    private void ShowOwnRight(string? id)
    {
        if (_ownRight == null || _ownRightContext == null)
        {
            return;
        }
        var tool = id == null ? null : _ownRightTools.FirstOrDefault(t => t.Info.Id == id);
        bool same = tool != null && ReferenceEquals(tool, _ownRightTool) && _ownRight.Card.IsVisible;
        if (tool == null || same)
        {
            _ownRight.Card.IsVisible = false;
            _ownRightTool = null;
            _layersButton?.Classes.Set("on", false);
            _checksButton?.Classes.Set("on", false);
            _objectsButton?.Classes.Set("on", false);
            return;
        }
        ShowRight(null);
        _ownRight.Show(tool);
        _ownRight.Card.IsVisible = true;
        _ownRightTool = tool;
        tool.Chosen(_ownRightContext);
        _layersButton?.Classes.Set("on", id == "layers");
        _checksButton?.Classes.Set("on", id == "checks");
        _objectsButton?.Classes.Set("on", id == "objects");
    }

    /// <summary>The Live pill's state: on (green) with what happened last, or off.</summary>
    private void LiveState(string? text, bool error = false)
    {
        if (_liveDot == null || _liveStatus == null)
        {
            return;
        }
        // Off grey, sending amber, synced green, lost red.
        var (dot, words) = error ? (Color.FromRgb(232, 98, 82), text ?? "lost Unreal")
            : _live == null ? (Colors.Gray, "off")
            : text is { } t && t.StartsWith("sending", StringComparison.Ordinal) ? (Color.FromRgb(240, 168, 58), t)
            : (Color.FromRgb(76, 195, 138), text ?? $"synced {DateTime.Now:HH:mm:ss}");
        _liveDot.Fill = new SolidColorBrush(dot);
        _liveStatus.Text = words;
        _liveStatus.Foreground = error ? new SolidColorBrush(dot) : Ui.Muted;
    }

    /// <summary>Save stamp: the selection as a named stamp; the Stamp tool then offers it.</summary>
    private async Task SaveStampAsked()
    {
        if (Session is not { Scene.IsMap: true } s)
        {
            return;
        }
        var selected = View.Selected;
        if (selected.Count == 0)
        {
            Msg(new Said("Select the objects for the stamp first (Select, E).", ""));
            return;
        }
        if (await Dialogs.AskText(this, "Save stamp", $"A name for the stamp of {selected.Count} objects:", "My stamp") is not { Length: > 0 } name)
        {
            return;
        }
        int n = MapTools.SaveStamp(s.Scene, selected, name);
        MapRailTools();
        Msg(new Said($"Saved the stamp \"{name}\" ({n} objects): the Stamp tool has it.", ""));
    }

    /// <summary>Save as biome: the Brush's picked kinds as a named biome; the Biome tool then offers it.</summary>
    internal async Task SaveBiomeAsked(IReadOnlyList<string> models)
    {
        if (models.Count == 0)
        {
            Msg(new Said("Pick the kinds for the biome first.", ""));
            return;
        }
        if (await Dialogs.AskText(this, "Save as biome", $"A name for the biome of {models.Count} kinds (its ground is the first layer; the map's catalog changes it):", "My biome") is not { Length: > 0 } name)
        {
            return;
        }
        if (MapTools.SaveBiome(name, MapProject.Current?.Layers.FirstOrDefault()?.Name ?? "grass", models) is string why)
        {
            Msg(new Said(why, ""));
            return;
        }
        MapRailTools();
        Msg(new Said($"Saved the biome \"{name}\" ({models.Count} kinds): the Biome tool has it.", ""));
    }

    /// <summary>Live on (after sending what Unreal does not have yet, when anything is unsaved) or off.</summary>
    private async Task SetLive(bool on)
    {
        if (!on)
        {
            _pullTimer?.Stop();
            if (_live != null)
            {
                _live = null;
                LiveState(null);
                Msg(new Said("Live off: changes stay in the editor until Send to Unreal.", ""));
            }
            return;
        }
        if (Session is not { Scene.IsMap: true } s)
        {
            return;
        }
        if (!await LinkUnreal())
        {
            _liveButton!.IsChecked = false;
            LiveState($"lost Unreal {DateTime.Now:HH:mm}", error: true);
            return;
        }
        if (MapDirty)
        {
            await SendToUnreal();
        }
        var (live, why) = await UnrealLive.Connect(s.Scene);
        if (live == null)
        {
            _liveButton!.IsChecked = false;
            LiveState($"lost Unreal {DateTime.Now:HH:mm}", error: true);
            await Tell(why ?? "Live could not start.");
            return;
        }
        _live = live;
        _pullTimer ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _pullTimer.Tick -= PullTick;
        _pullTimer.Tick += PullTick;
        _pullTimer.Start();
        LiveState(null);
        Msg(new Said("LIVE both ways: what you change here goes into the open Unreal level, and what is changed there comes here.", ""));
    }

    private bool _pullingNow;

    /// <summary>While live: brings in what was changed in Unreal.</summary>
    private async void PullTick(object? sender, EventArgs e)
    {
        if (_live is not { } live || _pullingNow)
        {
            return;
        }
        _pullingNow = true;
        try
        {
            if (await live.Pull() is { } said)
            {
                LiveState($"from Unreal {DateTime.Now:HH:mm:ss}");
                Msg(new Said(said, ""));
                UpdateSaveBar();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException)
        {
            _pullTimer?.Stop();
            _live = null;
            _liveButton!.IsChecked = false;
            LiveState($"lost Unreal {DateTime.Now:HH:mm}", error: true);
            await Tell("Live stopped, Unreal did not answer: " + ex.Message);
        }
        finally
        {
            _pullingNow = false;
        }
    }

    /// <summary>After a change while live: send it.</summary>
    internal async Task LiveChanged()
    {
        if (_live is not { } live)
        {
            return;
        }
        try
        {
            LiveState("sending…");
            if (await live.Sync() is { Length: > 0 } said)
            {
                Msg(new Said(said, ""));
            }
            LiveState(null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            _live = null;
            _liveButton!.IsChecked = false;
            LiveState($"lost Unreal {DateTime.Now:HH:mm}", error: true);
            await Tell("Live stopped, Unreal did not answer: " + e.Message);
        }
    }

    /// <summary>Settle objects: the selection onto the ground, or every object by the ground's change under it.</summary>
    internal void SettleObjects()
    {
        if (Session is not { Scene.IsMap: true } s || _mapGround is not { } before)
        {
            return;
        }
        var area = new SceneLayers.AreaView(s.Scene);
        var selected = View.Selected;
        var remove = new List<int>();
        var add = new List<(NewObject, bool)>();
        lock (s.Scene.Things)
        {
            var which = selected.Count > 0 ? selected : Enumerable.Range(0, s.Scene.Things.Count);
            foreach (int i in which)
            {
                var t = s.Scene.Things[i];
                if (t.Gone)
                {
                    continue;
                }
                float y;
                if (selected.Count > 0)
                {
                    y = area.GroundHeight(t.Position.X, t.Position.Z);
                }
                else
                {
                    int gx = Math.Clamp((int)MathF.Round(t.Position.X - s.Scene.OriginX), 0, s.Scene.W - 1), gz = Math.Clamp((int)MathF.Round(t.Position.Z - s.Scene.OriginZ), 0, s.Scene.H - 1);
                    float change = s.Scene.Heights[(gz * s.Scene.W) + gx] - before[(gz * s.Scene.W) + gx];
                    if (MathF.Abs(change) < 0.02f)
                    {
                        continue;
                    }
                    y = t.Position.Y + change;
                }
                if (MathF.Abs(y - t.Position.Y) < 0.005f)
                {
                    continue;
                }
                remove.Add(i);
                add.Add((new NewObject(0, t.Prefab, t.Position with { Y = y }, t.Rotation, t.Scale), t.Piece));
            }
        }
        if (remove.Count > 0)
        {
            _ = s.Commit($"Settled {remove.Count} objects", null, remove, add);
        }
        if (selected.Count == 0)
        {
            _mapGround = (float[])s.Scene.Heights.Clone();
        }
        Msg(new Said(remove.Count == 0 ? "Nothing to settle." : $"Settled {remove.Count} objects (Ctrl+Z takes it back).", ""));
        UpdateSaveBar();
    }

    /// <summary>Send to Unreal: saves, then runs the map's apply script (map.json's unreal.apply), its last line on the status bar.</summary>
    internal async Task SendToUnreal()
    {
        if (Session is not { Scene.IsMap: true } || _moreButton is not { IsEnabled: true } button)
        {
            return;
        }
        if (MapProject.Current is not { UnrealApply: { } script } map)
        {
            await Tell("This map has no Unreal level (map.json's unreal.apply): Save keeps it in its own folder.");
            return;
        }
        // Trusted as it is now: a changed script is asked about again, and what runs is the text read here (a change made
        // after the check does not run), as bash -c with the script's path as $0.
        string trust, text;
        try
        {
            byte[] bytes = File.ReadAllBytes(script);
            if (bytes.Length > 100_000 || bytes.Contains((byte)0))
            {
                await Tell($"{script} is not a script Send to Unreal runs (over 100 kB, or not text).");
                return;
            }
            (text, trust) = (System.Text.Encoding.UTF8.GetString(bytes), map.Folder + "|" + script + "|" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Tell($"{script} could not be read: {ex.Message}");
            return;
        }
        if (!Settings.TrustedScripts.Contains(trust))
        {
            if (!await Ask("Run this map's script?", $"Send to Unreal runs {script}, named in {map.Name}'s map.json. Run it only when you know where the map came from.", "Run it", Lang.T("common.cancel")))
            {
                return;
            }
            Settings.TrustedScripts.Add(trust);
            SaveSettings(Settings);
        }
        if (!await SaveMap(quiet: true))
        {
            return;
        }
        button.IsEnabled = false;
        LiveState("sending everything…");
        var lines = new List<string>();
        int code;
        try
        {
            using var p = Process.Start(new ProcessStartInfo("bash", ["-c", text, script]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
            void Line(string? l)
            {
                if (!string.IsNullOrWhiteSpace(l))
                {
                    lock (lines)
                    {
                        lines.Add(l);
                    }
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Msg(new Said("Unreal: " + l, "")));
                }
            }
            p.OutputDataReceived += (_, e) => Line(e.Data);
            p.ErrorDataReceived += (_, e) => Line(e.Data);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            code = p.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            lines.Add(ex.Message);
            code = -1;
        }
        button.IsEnabled = true;
        LiveState(null);
        string tail = string.Join("\n", lines.TakeLast(6));
        if (code == 0)
        {
            Msg(new Said("Sent to Unreal.", ""));
        }
        else
        {
            await Tell(lines.Any(l => l.Contains("no Unreal editor answered"))
                ? "Unreal is not running (or not answering). Start it, wait until it has loaded the map's level, then send again."
                : "Sending to Unreal failed:\n" + tail);
        }
    }

    /// <summary>Save: the map's ground, objects and water into its files.</summary>
    internal async Task<bool> SaveMap(bool quiet = false)
    {
        if (Session is not { } s || MapProject.Current is not { } map)
        {
            return false;
        }
        try
        {
            int objects = map.Save(s.Scene);
            _mapObjects = objects;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await Tell("The map could not be saved: " + ex.Message);
            return false;
        }
        MarkSaved(s);
        UpdateSaveBar();
        if (!quiet)
        {
            Msg(new Said($"Saved the ground, its colours, the rivers and {_mapObjects} objects (old files kept as .bak). Send to Unreal puts them in the game.", ""));
        }
        return true;
    }

    /// <summary>Start: back to the start page, asking first when the map is not saved.</summary>
    private async Task LeaveMap()
    {
        if (MapDirty && !await Ask("Leave the map?", "Your changes to the map are not saved.", Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        InMap = false;
        View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        if (View.Scene is { } shown)
        {
            View.SetWater(shown, []);
        }
        Brush.MapOpen = false;
        Tools.RelabelPaints();
        LayerMaskNames(false);
        MapButtons(false);
        Tools.SetMap(false);
        _pendingPill.IsVisible = true;
        SaveButton.Classes.Set("primary", true);
        MapTools.Hidden.Clear();
        View.HideOnly([]);
        MapButton.Content = Icons.With("back", Lang.T("top.label.map"));
        Session = null;
        ShowStart();
    }

    /// <summary>The save bar while the map is open.</summary>
    private void MapSaveBar()
    {
        bool dirty = MapDirty;
        int steps = UnsavedSteps;
        PendingText.Text = !dirty ? "" : steps > 0 ? $"{steps} unsaved change{(steps == 1 ? "" : "s")}" : "Unsaved changes";
        _pendingPill.IsVisible = dirty;
        // Nothing to save reads as done (grey "Saved"), not as a dimmed call to action.
        SaveButton.IsEnabled = dirty;
        SaveButton.Content = dirty ? "Save" : "Saved";
        SaveButton.Classes.Set("primary", dirty);
        SetPendingPill(dirty);
    }

    /// <summary>Opens the map in a folder (one without a map says so).</summary>
    internal async Task OpenMap(string folder)
    {
        MapProject map;
        try
        {
            map = MapProject.Open(folder);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            await Tell($"{folder}: {ex.Message}");
            return;
        }
        await ShowMap(map);
    }

    /// <summary>The last map opened that is still there (Settings' maps), or null.</summary>
    internal MapProject? LastMap() => LastMapOf(Settings.Maps);

    /// <summary>The first of the maps opened before that still opens (the list has the last first), or null.</summary>
    internal static MapProject? LastMapOf(IEnumerable<string> maps)
    {
        foreach (string f in maps.Where(MapProject.IsMap))
        {
            try
            {
                return MapProject.Open(f);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Log.Caught(ex, "reading the last map's map.json");
            }
        }
        return null;
    }

    /// <summary>A map opened goes first in Settings' maps (the start page's list).</summary>
    private void RememberMap(string folder)
    {
        if (Settings.Maps.FirstOrDefault() == folder)
        {
            return;
        }
        _ = Settings.Maps.Remove(folder);
        Settings.Maps.Insert(0, folder);
        SaveSettings(Settings);   // a failure is logged; the list stays in memory
        StartPage?.ShowMaps();
    }

    /// <summary>Where new maps go: Settings' maps folder, else Documents/Groundwork.</summary>
    internal string NewMapsFolder() => Settings.MapsFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify) is { Length: > 0 } docs
        ? docs : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Groundwork");   // asked for unchecked: on a fresh home it is not made yet and came back "" (a folder relative to wherever the app was started)
}
