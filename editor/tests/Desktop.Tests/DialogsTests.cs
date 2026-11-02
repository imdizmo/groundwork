using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The small modal windows (Dialogs): a question answered with either button, a message with only OK, and a line of
/// text typed, kept or cancelled. They are real windows owned by the main one: the tests find them there and press
/// their buttons.
/// </summary>
public class DialogsTests
{

    /// <summary>
    /// Presses the dialog's button with this label.
    /// </summary>
    internal static void Press(Window dialog, string label)
    {
        var b = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == label);
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A shown 800 × 600 window to own the dialogs.
    /// </summary>
    private static Window Owner()
    {
        var w = new Window { Width = 800, Height = 600 };
        w.Show();
        return w;
    }

    [AvaloniaFact]
    public async Task AQuestionIsYesWithTheFirstButtonAndNoWithTheOther()
    {
        var owner = Owner();
        var asked = Dialogs.Ask(owner, "Discard", "Discard the changes?", "Discard", "Keep them");
        var d = TestWindows.Shown(owner);
        Assert.Equal("Discard", d.Title);
        Assert.Contains("Discard the changes?", d.GetLogicalDescendants().OfType<SelectableTextBlock>().Single().Text);
        Press(d, "Discard");
        Assert.True(await asked);
        Assert.Empty(owner.OwnedWindows);

        asked = Dialogs.Ask(owner, "Discard", "Again?", "Discard", "Keep them");
        Press(TestWindows.Shown(owner), "Keep them");
        Assert.False(await asked);
    }

    [AvaloniaFact]
    public async Task AMessageHasOnlyOk()
    {
        var owner = Owner();
        var told = Dialogs.Tell(owner, "Save", "Saved 3 zone(s).");
        var d = TestWindows.Shown(owner);
        var buttons = d.GetLogicalDescendants().OfType<Button>().ToList();
        Assert.Equal("OK", Assert.Single(buttons).Content);
        // Enter and Esc both close it.
        Assert.True(buttons[0].IsDefault && buttons[0].IsCancel);
        Press(d, "OK");
        await told;
        Assert.Empty(owner.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task ClosingAQuestionWithoutAButtonIsNo()
    {
        var owner = Owner();
        var asked = Dialogs.Ask(owner, "Q", "Sure?", "Yes");
        TestWindows.Shown(owner).Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(await asked);
    }

    [AvaloniaFact]
    public async Task ALineOfTextIsTypedKeptOrCancelled()
    {
        var owner = Owner();
        var typed = Dialogs.AskText(owner, "Save stamp", "Name of the stamp:", "My house");
        var d = TestWindows.Shown(owner);
        var box = d.GetLogicalDescendants().OfType<TextBox>().Single();
        Assert.Equal("My house", box.Text);
        box.Text = "Longhouse";
        Press(d, "OK");
        Assert.Equal("Longhouse", await typed);

        typed = Dialogs.AskText(owner, "Save stamp", "Name:");
        d = TestWindows.Shown(owner);
        Assert.Equal("", d.GetLogicalDescendants().OfType<TextBox>().Single().Text);
        Press(d, "Cancel");
        Assert.Null(await typed);
    }

    [AvaloniaFact]
    public async Task TheZoneFormGivesItsNumbersOrNothing()
    {
        var owner = Owner();
        var asked = Dialogs.AskZone(owner, 1000);
        var d = TestWindows.Shown(owner);
        d.GetLogicalDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "none").Text = "12, 5";
        Press(d, "Make it");
        var spec = await asked;
        Assert.NotNull(spec);
        Assert.Equal([12f, 5f], spec.LakeHectares!);
        Assert.Equal((20f, 70f, 0.35f), (spec.Low, spec.High, spec.Woods));
        Assert.Equal(120f, spec.RimWidth);   // scaled to the map's size

        asked = Dialogs.AskZone(owner, 1000);
        Press(TestWindows.Shown(owner), "Cancel");
        Assert.Null(await asked);
    }

    [AvaloniaFact]
    public async Task AZoneStyleFillsTheNumbersAndKeepsItsShapes()
    {
        var owner = Owner();
        var asked = Dialogs.AskZone(owner, 2000);
        var d = TestWindows.Shown(owner);
        var style = d.GetLogicalDescendants().OfType<ComboBox>().Single();
        style.SelectedIndex = ZoneGenerator.Styles.ToList().FindIndex(s => s.Name == "Mesas and canyons");
        Press(d, "Make it");
        var spec = await asked;
        Assert.NotNull(spec);
        var mesas = ZoneGenerator.Styles.Single(s => s.Name == "Mesas and canyons").Spec;
        Assert.True(spec.Terraces > 0 && spec.Canyons > 0);
        Assert.Equal((mesas.Low, mesas.High, mesas.Terraces), (spec.Low, spec.High, spec.Terraces));
    }
}
