using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Groundwork.App;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The history panel: back to a change, redo to one, taking out one change only, and a long history shown a page at
/// a time.
/// </summary>
[Collection("World files")]
public class HistoryPanelTests
{
    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [Fact]
    public void BackToAndForwardTo()
    {
        var s = TestScenes.Flat(2);
        s.Shape(30, 30, Two, 3, 0, "one");
        s.Shape(60, 60, Two, 3, 0, "two");
        s.Shape(90, 90, Two, 3, 0, "three");
        var first = s.UndoList[0];
        s.BackTo(first);
        Assert.Single(s.UndoList);
        Assert.Equal(30, H(s, 60, 60), 3);
        Assert.Equal(2, s.RedoList.Count);
        s.ForwardTo(s.RedoList[0]);
        Assert.Equal(3, s.UndoList.Count);
        Assert.Equal(32, H(s, 90, 90), 3);
    }

    [Fact]
    public void RemovingOneChangeKeepsTheOnesAfter()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, AssetIds.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        s.Shape(60, 60, Two, 3, 0, "raise");
        s.Delete([0]);
        // Raised again on the same spot afterwards: that stays.
        s.Shape(60, 60, Two, 3, 0, "raise more");
        Assert.Equal(34, H(s, 60, 60), 3);
        Assert.Equal("Removed: raise", s.UndoList.Count is > 0 ? Remove(s, s.UndoList[0]) : "");
        Assert.Equal(32, H(s, 60, 60), 3);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.True(s.UndoList[0].Removed);
        // The delete taken out: the tree is back.
        Remove(s, s.UndoList[1]);
        Assert.False(s.Scene.Things[0].Gone);
        // A removal is undone like any change.
        s.Undo();
        Assert.True(s.Scene.Things[0].Gone);
        Assert.False(s.UndoList[1].Removed);
        Assert.Equal("That change cannot be removed.", s.RemoveChange(s.UndoList[0]).Shown);
    }

    /// <summary>
    /// Takes out one change; returns the undo label then.
    /// </summary>
    private static string Remove(EditSession s, EditSession.Change c)
    {
        s.RemoveChange(c);
        return s.UndoLabel!;
    }

    [AvaloniaFact]
    public void ThePanelListsTheChanges()
    {
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        s.Shape(30, 30, Two, 3, 0, "one");
        s.Shape(60, 60, Two, 3, 0, "two");
        s.Undo();
        w.HistoryButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.True(w.History.Card.IsVisible);
        Assert.Equal(2, w.History.Rows.Children.Count);
        Assert.Equal("1 change(s)", w.History.Count.Text);
        // The undone one is on top, greyed, with Redo to here.
        var top = (Avalonia.Controls.Border)w.History.Rows.Children[0];
        Assert.Equal(0.5, top.Opacity);
        var buttons = top.GetVisualDescendants().OfType<Avalonia.Controls.Button>().ToList();
        Assert.Equal("Redo to here", buttons.Single().Content);
        buttons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(2, s.UndoList.Count);
    }

    // A long history: the newest page of rows, then Show older with how many are left, which adds a page; shown
    // again, the newest page only.
    [AvaloniaFact]
    public void ALongHistoryShowsItsNewestPageFirst()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 0;
        var w = TestWindows.Editing(s);
        for (int i = 0; i < HistoryPanel.Page + 5; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        s.Undo();
        w.History.Toggle();
        var rows = w.History.Rows.Children;
        Assert.Equal(HistoryPanel.Page + 1, rows.Count);
        // The undone change on top, the oldest ones left out.
        Assert.Equal(0.5, rows[0].Opacity);
        var older = Assert.IsType<Avalonia.Controls.Button>(rows[^1]);
        Assert.Equal("Show older (5)", older.Content);
        older.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(HistoryPanel.Page + 5, rows.Count);
        Assert.DoesNotContain(rows, r => r is Avalonia.Controls.Button);
        w.History.Toggle();
        w.History.Toggle();
        Assert.Equal(HistoryPanel.Page + 1, w.History.Rows.Children.Count);
    }
}
