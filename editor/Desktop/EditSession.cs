
namespace Groundwork.Desktop;

/// <summary>
/// Editing a scene: brush strokes on the ground, deleting and moving objects, undo and redo, what is waiting to be
/// saved. Strokes run on the drawing thread, the buttons on the window's: everything here takes the lock. Objects are
/// changed on the window's thread only.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The open area being edited.
    /// </summary>
    public WorldScene Scene { get; }

    /// <summary>
    /// The area's ground grid the brushes and tools change.
    /// </summary>
    public Ground Ground { get; }

    /// <summary>
    /// What changed since the map was opened: the counters, and the objects placed with their data.
    /// </summary>
    public MapEdits Edits { get; } = new();

    /// <summary>
    /// The window shares its brush settings with the session.
    /// </summary>
    public Brush Brush { get; set; } = new();

    /// <summary>
    /// The window's Mask too: which ground the brushes and tools may change.
    /// </summary>
    public Mask Mask { get; set; } = new();

    /// <summary>
    /// The mask of the stroke going on, judged as the ground was when it started (null when off or between strokes).
    /// </summary>
    private Func<int, float>? _strokeMask;

    /// <summary>
    /// Guards the ground, the history and the stroke: strokes run on the drawing thread, the buttons on the window's.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The undo list, oldest first (the next undo last).
    /// </summary>
    public IReadOnlyList<Change> UndoList { get { lock (_lock) { return [.. _undo]; } } }

    /// <summary>
    /// What was undone, the next redo last.
    /// </summary>
    public IReadOnlyList<Change> RedoList { get { lock (_lock) { return [.. _redo]; } } }

    /// <summary>
    /// The ground state of a change that touched no ground points.
    /// </summary>
    private static readonly Ground.State NoPoints = new([], [], [], [], []);

    /// <summary>
    /// The history: what can be undone (the next undo last) and what can be redone (the next redo last).
    /// </summary>
    private readonly List<Change> _undo = [], _redo = [];

    /// <summary>
    /// A step that changes neither ground nor objects, only something of its own (the map's rivers; local
    /// addition): apply(true) does it again, apply(false) takes it back. It is already done.
    /// </summary>
    /// <returns>The step as kept.</returns>
    internal Change RecordExtra(string label, Action<bool> apply)
    {
        AddChange(new Change(label, [], NoPoints, NoPoints) { Extra = apply });
        Change kept;
        lock (_lock)
        {
            kept = _undo[^1];
        }
        Edited();
        return kept;
    }

    /// <summary>Forgets every step (a map after loading its objects).</summary>
    internal void ForgetHistory()
    {
        lock (_lock)
        {
            _undo.Clear();
            _redo.Clear();
        }
    }

    /// <summary>
    /// How many steps the undo lists keep, from their next change on (Settings,
    /// <see cref="Groundwork.App.AppSettings.HistorySteps"/>); 0 or less: no limit.
    /// </summary>
    public static int StepLimit { get; set; }

    /// <summary>
    /// How much memory an undo list may take (about; <see cref="Change.Bytes"/>), whatever the steps' limit: 2 GB.
    /// </summary>
    public const long HistoryBytes = 2L << 30;

    /// <summary>
    /// How many steps this session's undo list keeps: <see cref="StepLimit"/> as it is now, unless set (tests); 0 or
    /// less: no limit. The oldest go first, at the next change.
    /// </summary>
    internal int KeepSteps
    {
        get => _keepSteps ?? StepLimit;
        set => _keepSteps = value;
    }

    /// <summary>The steps this session keeps whatever Settings say; null: Settings' (<see cref="KeepSteps"/>).</summary>
    private int? _keepSteps;

    /// <summary>
    /// How much memory this session's undo list may take (<see cref="HistoryBytes"/>; tests set less): past it, the
    /// oldest steps go first, at the next change. The newest step always stays.
    /// </summary>
    internal long KeepBytes { get; set; } = HistoryBytes;

    /// <summary>
    /// The brush stroke going on; null between strokes (during one, undo and redo do nothing and saving throws).
    /// </summary>
    private Stroke? _stroke;

    /// <summary>
    /// The grid rectangle changed since the view last took it (to send to the graphics card).
    /// </summary>
    private (int X0, int Z0, int X1, int Z1)? _dirty;

    /// <summary>
    /// Raised after anything here changed (an edit, a save, an apply): what is pending and the undo buttons may differ.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// A change made by the user (a stroke, a tool, undo, redo, a change taken out): not a save.
    /// </summary>
    public event Action? EditMade;

    /// <summary>
    /// Raises Changed and EditMade after an edit by the user.
    /// </summary>
    private void Edited()
    {
        Edits.Changed();
        Changed?.Invoke();
        EditMade?.Invoke();
    }

    /// <summary>
    /// Things that appeared, went or came back (indices into the scene's things).
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsChanged;

    /// <summary>
    /// New objects were added (placed, pasted, copied, restored...): their indices in Scene.Things.
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsAdded;

    /// <summary>
    /// Ids for objects added in this session: negative, like the web editor's.
    /// </summary>
    private int _nextId = -1;

    /// <summary>
    /// A session over an open map's scene and its ground.
    /// </summary>
    public EditSession(WorldScene scene, Ground ground)
    {
        Scene = scene;
        Ground = ground;
    }

    /// <summary>The list's last change (null: none), under the lock.</summary>
    private Change? Last(List<Change> list)
    {
        lock (_lock)
        {
            return list.Count > 0 ? list[^1] : null;
        }
    }

    /// <summary>Whether the list holds this very change (not an equal one), under the lock.</summary>
    private bool Holds(List<Change> list, Change c)
    {
        lock (_lock)
        {
            return list.Exists(l => ReferenceEquals(l, c));
        }
    }

    /// <summary>
    /// Counts every change to the area (a step made, undone or redone, the area read again): what was worked out from
    /// it earlier (a script's run) is out of date once it moved.
    /// </summary>
    public int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Backs Generation; changed with Interlocked.
    /// </summary>
    private int _generation;

    /// <summary>Raises ThingsChanged for these things.</summary>
    private void Notify(IEnumerable<int> indices) => ThingsChanged?.Invoke([.. indices]);
}
