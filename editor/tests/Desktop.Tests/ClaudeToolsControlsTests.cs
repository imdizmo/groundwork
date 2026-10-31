using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Claude's tools for the editor's own controls (ClaudeTools.Controls): controls known by kind, labelled by what
/// the user reads, given ids and values, set as the user would, and the blocked ones refused.
/// </summary>
[Collection("World files")]
public class ClaudeToolsControlsTests
{
    private static readonly string[] Expected1 = ["Fast", "Sharp"];
    // KindOf: each kind of control by its most specific type; others are not controls Claude sets.
    [AvaloniaFact]
    public void ControlsAreKnownByKind()
    {
        Assert.Equal("switch", ClaudeTools.KindOf(new CheckBox()));
        Assert.Equal("radio", ClaudeTools.KindOf(new RadioButton()));
        Assert.Equal("toggle", ClaudeTools.KindOf(new Avalonia.Controls.Primitives.ToggleButton()));
        Assert.Equal("button", ClaudeTools.KindOf(new Button()));
        Assert.Equal("dropdown", ClaudeTools.KindOf(new ComboBox()));
        Assert.Equal("slider", ClaudeTools.KindOf(new Slider()));
        Assert.Equal("number", ClaudeTools.KindOf(new NumericUpDown()));
        Assert.Equal("text", ClaudeTools.KindOf(new TextBox()));
        Assert.Null(ClaudeTools.KindOf(new TextBlock()));
    }

    // LabelOf: the content's text, a text box's placeholder, the text block just before it in its panel, else the
    // first sentence of its tip.
    [AvaloniaFact]
    public void ControlsAreLabelledByWhatTheUserReads()
    {
        Assert.Equal("Snap", ClaudeTools.LabelOf(new CheckBox { Content = "Snap" }));
        Assert.Equal("Find…", ClaudeTools.LabelOf(new TextBox { PlaceholderText = "Find…" }));
        var slider = new Slider();
        _ = new StackPanel { Children = { new TextBlock { Text = "Strength" }, slider } };
        Assert.Equal("Strength", ClaudeTools.LabelOf(slider));
        var tipped = new NumericUpDown().Tip("select.x");
        Assert.Equal(((string)ToolTip.GetTip(tipped)!).Split(". ")[0], ClaudeTools.LabelOf(tipped));
    }

    // ControlsOf: tip keys as ids, labels otherwise, a number after an id already taken; ValueOf reads each kind.
    [AvaloniaFact]
    public void ControlsHaveIdsAndValues()
    {
        var root = new StackPanel
        {
            Children =
            {
                new CheckBox { Content = "Show water", IsChecked = true }.Tip("view.water"),
                new Button { Content = "Go" }, new Button { Content = "Go" },
                new ComboBox { ItemsSource = Expected1, SelectedIndex = 1 },
                new Slider { Minimum = 0, Maximum = 40, Value = 7 },
            },
        };
        var list = ClaudeTools.ControlsOf(root);
        Assert.Equal(["view.water", "~go", "~go#2", "~dropdown", "~slider"], list.Select(c => c.Id));
        Assert.Equal([true, null, null, "Sharp", 7.0], list.Select(c => ClaudeTools.ValueOf(c.Control)));
    }

    // Set: a switch on and off, a dropdown by text or number, a slider clamped, a text, a button clicked (its Click and
    // its Command), a switch clicked; wrong values are said. Blocked: the window's Save and Discard, blocked keys, and
    // blocked labels of untipped buttons.
    [AvaloniaFact]
    public void ControlsAreSetAndBlockedOnesRefused()
    {
        var w = new MainWindow(load: false);
        var t = new ClaudeTools(w);
        static ClaudeTools.Ctl C(string id, Control c) => new(id, ClaudeTools.KindOf(c)!, ClaudeTools.LabelOf(c), c);
        var box = new CheckBox();
        Assert.Null(t.Set(C("a", box), "on", false));
        Assert.True(box.IsChecked);
        Assert.StartsWith("a is a switch: give true or false", t.Set(C("a", box), "maybe", false));
        var combo = new ComboBox { ItemsSource = new[] { "Fast", "Sharp" } };
        Assert.Null(t.Set(C("b", combo), "sharp", false));
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Null(t.Set(C("b", combo), "0", false));
        Assert.Equal(0, combo.SelectedIndex);
        Assert.Equal("b has no option \"Slow\": Fast, Sharp.", t.Set(C("b", combo), "Slow", false));
        var slider = new Slider { Minimum = 0, Maximum = 40 };
        Assert.Null(t.Set(C("c", slider), "99", false));
        Assert.Equal(40, slider.Value);
        // Not a number to set (NaN went through Math.Clamp), and a number too big for a decimal (it threw).
        Assert.StartsWith("c is a slider from 0 to 40: give a number", t.Set(C("c", slider), "NaN", false));
        Assert.StartsWith("c is a slider", t.Set(C("c", slider), "-Infinity", false));
        Assert.Equal(40, slider.Value);
        var number = new NumericUpDown { Minimum = 1, Maximum = 50 };
        Assert.Null(t.Set(C("g", number), "1e30", false));
        Assert.Equal(50m, number.Value);
        Assert.Null(t.Set(C("g", number), "-1e30", false));
        Assert.Equal(1m, number.Value);
        Assert.StartsWith("g takes a number", t.Set(C("g", number), "Infinity", false));
        var text = new TextBox();
        Assert.Null(t.Set(C("d", text), "Beech", true));
        Assert.Equal("Beech", text.Text);
        int clicks = 0;
        var command = new CountingCommand();
        var button = new Button { Content = "Go", Command = command };
        button.Click += (_, _) => clicks++;
        Assert.Null(t.Set(C("e", button), "click", false));
        Assert.Equal(1, clicks);
        Assert.Equal(1, command.Runs);
        Assert.Equal("e is a button: give click.", t.Set(C("e", button), "true", false));
        // A click on a switch changes it before its handlers run, as a pointer's does.
        var toggle = new CheckBox();
        bool? seen = null;
        toggle.Click += (_, _) => seen = toggle.IsChecked;
        int changes = 0;
        toggle.IsCheckedChanged += (_, _) => changes++;
        Assert.Null(t.Set(C("f", toggle), "click", false));
        Assert.True(toggle.IsChecked);
        Assert.True(seen);
        Assert.Equal(1, changes);

        Assert.NotNull(t.Blocked(C("save", w.SaveButton)));
        Assert.NotNull(t.Blocked(C("~delete", new Button { Content = "Delete" })));
        Assert.NotNull(t.Blocked(C("~live", new Button { Content = "Live" })));
        // Buttons that write or delete files and Select's Delete, by their key whatever their label's language.
        string[] writing = ["map.save", "select.delete", "select.forget", "brush.forgetStamp"];
        Assert.All(writing, k => Assert.NotNull(t.Blocked(C(k, new Button { Content = "Supprimer" }.Tip(k)))));
        Assert.Null(t.Blocked(C("select.all", new Button { Content = "Tout" }.Tip("select.all"))));
        Assert.Null(t.Blocked(C("~go", button)));
        Assert.StartsWith("save is not Claude's to use", t.Set(C("save", w.SaveButton), "click", false));
        w.CloseWithoutAsking();
    }

    /// <summary>A command that can always run and counts its runs.</summary>
    private sealed class CountingCommand : ICommand
    {
        /// <summary>Not kept, as it is never raised: the command can always run.</summary>
#pragma warning disable S3237 // a listener need not be kept for an event that is never raised
        event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
#pragma warning restore S3237

        /// <summary>How many times the command ran.</summary>
        public int Runs { get; private set; }

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => true;

        /// <inheritdoc/>
        public void Execute(object? parameter) => Runs++;
    }
}
