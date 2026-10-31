using System.Collections.Generic;
using System.Numerics;

namespace Groundwork.Sdk;

/// <summary>What a tool can ask of the editor while it is chosen (<see cref="IEditorTool"/>).</summary>
public interface IToolContext
{
    /// <summary>The area open, as it is now (its objects with the changes not saved yet, its ground).</summary>
    IAreaView Area { get; }

    /// <summary>A slider's value.</summary>
    /// <param name="id">The slider's id.</param>
    float Number(string id);

    /// <summary>A choice's index (0 for the first).</summary>
    /// <param name="id">The choice's id.</param>
    int Choice(string id);

    /// <summary>A toggle's value.</summary>
    /// <param name="id">The toggle's id.</param>
    bool IsOn(string id);

    /// <summary>Changes a text line of the card.</summary>
    /// <param name="id">The text's id.</param>
    /// <param name="text">What it says now.</param>
    void SetText(string id, string text);

    /// <summary>Moves a slider to a value without it counting as a change.</summary>
    /// <param name="id">The slider's id.</param>
    /// <param name="value">Its value now.</param>
    void SetNumber(string id, float value);

    /// <summary>Fills a <see cref="ListControl"/> and picks one of its cards.</summary>
    /// <param name="id">The list's id.</param>
    /// <param name="items">The cards: title, the line under it, a tag on the right.</param>
    /// <param name="picked">The card picked (index).</param>
    void SetList(string id, IReadOnlyList<(string Title, string Sub, string Tag)> items, int picked);

    /// <summary>
    /// What to draw over the area until the next call (empty: nothing): shapes in the world's frame
    /// (<see cref="Shape.WorldSpace"/> is taken as true), drawn see-through over everything.
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    void Preview(IReadOnlyList<Shape> shapes);

    /// <summary>A line in the editor's status bar.</summary>
    /// <param name="text">The line.</param>
    void Status(string text);

    /// <summary>
    /// Makes the edits one step of the editor's history (undone and redone as one, saved and applied live like the
    /// editor's own); nothing when they change nothing.
    /// </summary>
    /// <param name="label">The step's name in the history.</param>
    /// <param name="edit">The objects added, changed and removed.</param>
    void Commit(string label, ToolEdit edit);

    /// <summary>The items picked in a <see cref="PaletteControl"/> (indices into its items).</summary>
    /// <param name="id">The palette's id.</param>
    IReadOnlyList<int> Picked(string id);
}

/// <summary>
/// What a tool's commit does, as one step of the history: objects added (with their data), objects whose data changes,
/// objects removed.
/// </summary>
public sealed class ToolEdit
{
    private readonly List<(string Asset, Vector3 Position, Vector3 Rotation, IReadOnlyDictionary<string, string>? Data)> _added = [];
    private readonly List<(IObjectView Object, IReadOnlyDictionary<string, string?> Change)> _changed = [];
    private readonly List<IObjectView> _removed = [];

    /// <summary>The objects added.</summary>
    public IReadOnlyList<(string Asset, Vector3 Position, Vector3 Rotation, IReadOnlyDictionary<string, string>? Data)> Added => _added;

    /// <summary>The objects whose data changes: the values set (null: the key taken away).</summary>
    public IReadOnlyList<(IObjectView Object, IReadOnlyDictionary<string, string?> Change)> Changed => _changed;

    /// <summary>The objects removed.</summary>
    public IReadOnlyList<IObjectView> Removed => _removed;

    /// <summary>Adds an object.</summary>
    /// <param name="asset">Its asset's name.</param>
    /// <param name="position">Where, in metres.</param>
    /// <param name="rotation">Its turn, Euler degrees.</param>
    /// <param name="data">Its data (null: none).</param>
    public ToolEdit Add(string asset, Vector3 position, Vector3 rotation, IReadOnlyDictionary<string, string>? data = null)
    {
        _added.Add((asset, position, rotation, data));
        return this;
    }

    /// <summary>Changes an object's data, keeping the object.</summary>
    /// <param name="obj">The object, as the area or the pointer gave it.</param>
    /// <param name="change">The values set; a null value takes its key away.</param>
    public ToolEdit Change(IObjectView obj, IReadOnlyDictionary<string, string?> change)
    {
        _changed.Add((obj, change));
        return this;
    }

    /// <summary>Removes an object.</summary>
    /// <param name="obj">The object, as the area or the pointer gave it.</param>
    public ToolEdit Remove(IObjectView obj)
    {
        _removed.Add(obj);
        return this;
    }
}
