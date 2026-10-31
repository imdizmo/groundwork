using System.Collections.Generic;

namespace Groundwork.Sdk;

/// <summary>A control of a tool's card. Its value is read through <see cref="IToolContext"/> by its id.</summary>
public abstract class ToolControl
{
    /// <summary>A control.</summary>
    /// <param name="id">Its id, unique within the tool.</param>
    /// <param name="label">What it is called on the card.</param>
    protected ToolControl(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>Its id.</summary>
    public string Id { get; }

    /// <summary>What it is called on the card.</summary>
    public string Label { get; }

    /// <summary>What it does, shown when pointing at it (null: nothing).</summary>
    public string? Help { get; set; }
}

/// <summary>A number chosen on a slider (<see cref="IToolContext.Number"/>).</summary>
public sealed class SliderControl : ToolControl
{
    /// <summary>A slider.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value.</param>
    /// <param name="value">Its first value.</param>
    public SliderControl(string id, string label, float min, float max, float value)
        : base(id, label)
    {
        Min = min;
        Max = max;
        Value = value;
    }

    /// <summary>The lowest value.</summary>
    public float Min { get; }

    /// <summary>The highest value.</summary>
    public float Max { get; }

    /// <summary>Its first value.</summary>
    public float Value { get; }

    /// <summary>The steps it moves by (0: any value).</summary>
    public float Step { get; set; }

    /// <summary>The unit shown after the value ("m"), or null.</summary>
    public string? Unit { get; set; }
}

/// <summary>One of a few choices (<see cref="IToolContext.Choice"/>).</summary>
public sealed class ChoiceControl : ToolControl
{
    /// <summary>A choice.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="options">The choices, as shown; the first is chosen first.</param>
    public ChoiceControl(string id, string label, IReadOnlyList<string> options)
        : base(id, label) => Options = options;

    /// <summary>The choices, as shown.</summary>
    public IReadOnlyList<string> Options { get; }
}

/// <summary>A yes or no (<see cref="IToolContext.IsOn"/>).</summary>
public sealed class ToggleControl : ToolControl
{
    /// <summary>A toggle.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="value">Its first value.</param>
    public ToggleControl(string id, string label, bool value)
        : base(id, label) => Value = value;

    /// <summary>Its first value.</summary>
    public bool Value { get; }
}

/// <summary>A button: <see cref="IEditorTool.OnControl"/> is called when it is pressed.</summary>
public sealed class ButtonControl : ToolControl
{
    /// <summary>A button.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What is written on it.</param>
    public ButtonControl(string id, string label)
        : base(id, label)
    {
    }
}

/// <summary>
/// A menu of items shown as pictures under group headings, with a search box :
/// one item picked (<see cref="IToolContext.Choice"/>) or, with <see cref="Multi"/>, several (<see cref="IToolContext.Picked"/>).
/// </summary>
public sealed class PaletteControl : ToolControl
{
    /// <summary>A palette.</summary>
    /// <param name="id">The control's id.</param>
    /// <param name="label">Its heading.</param>
    /// <param name="items">The items: group heading, name, picture file (PNG; null: none).</param>
    public PaletteControl(string id, string label, IReadOnlyList<(string Group, string Name, string? Picture)> items)
        : base(id, label)
    {
        Items = items;
    }

    /// <summary>The items: group heading, name, picture file.</summary>
    public IReadOnlyList<(string Group, string Name, string? Picture)> Items { get; }

    /// <summary>Several can be picked.</summary>
    public bool Multi { get; set; }

    /// <summary>The items picked at first (indices), until the user picks others.</summary>
    public IReadOnlyList<int> Picked { get; set; } = [];
}

/// <summary>
/// A list of cards (a title, a line under it, a tag) that the tool fills and refills (<see cref="IToolContext.SetList"/>),
/// one picked (<see cref="IToolContext.Choice"/>).
/// </summary>
public sealed class ListControl : ToolControl
{
    /// <summary>An empty list.</summary>
    /// <param name="id">The control's id.</param>
    /// <param name="label">Its heading.</param>
    public ListControl(string id, string label)
        : base(id, label)
    {
    }
}

/// <summary>
/// A heading that folds the controls after it, up to the next heading: a long card in groups, the
/// rarely used ones folded.
/// </summary>
public sealed class SectionControl : ToolControl
{
    /// <summary>A heading.</summary>
    /// <param name="id">Its id (its folded state is remembered by it).</param>
    /// <param name="label">Its words.</param>
    public SectionControl(string id, string label)
        : base(id, label)
    {
    }

    /// <summary>Folded at first, until the user opens it.</summary>
    public bool Folded { get; set; }
}

/// <summary>A line of text on the card, which the tool changes (<see cref="IToolContext.SetText"/>).</summary>
public sealed class TextControl : ToolControl
{
    /// <summary>A line of text.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="text">What it says first.</param>
    public TextControl(string id, string text)
        : base(id, text)
    {
    }
}
