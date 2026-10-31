using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// A window closed and opened again remembers what the editor kept: the View switches, the brush, the Area action, Select's
/// options, the Shape preset and formula, the right-hand panel, the clipboard and the map's place as they were. The tests'
/// windows remember in memory only unless given a file.
/// </summary>
public sealed class PrefsWindowTests : PrefsFolder
{
    /// <summary>
    /// A shown main window remembering in the test's prefs file.
    /// </summary>
    private MainWindow Window()
    {
        var w = new MainWindow(load: false, prefs: Prefs.Load(File1)) { Width = 1200, Height = 900 };
        w.Show();
        return w;
    }

    /// <summary>
    /// A small 2 × 2 clipboard with a missing height, mixed weights and paint (four values a point), to see it
    /// remembered as it was.
    /// </summary>
    private static CopyData Clip() => new()
    {
        W = 2,
        H = 2,
        Rel = [0f, 1f, float.NaN, 2f],
        Wt = [1f, 1f, 0f, 0.5f],
        Pnt = [-1f, -1, -1, -1, 0f, 0.5f, 1f, 0, 0.5f, 0.5f, 0, 0, 1f, 0, 0, 0],
        Objects = [new(Groundwork.App.AssetIds.Of("Beech1"), "Beech1", 1, 1, 0, new Vector3(0, 90, 0), 1)],
        Poly = [new(0, 0), new(2, 0), new(2, 2)],
        Name = "kept",
    };

    [AvaloniaFact]
    public void TheClipboardComesBack()
    {
        var w = Window();
        var c = Clip();
        w.View.Paste.Clip = c;
        w.View.Paste.Notify();
        // Notified again with the same copy (an offset changed): written once.
        w.View.Paste.Notify();
        w.Prefs.Flush();
        var v = Window();
        var back = v.View.Paste.Clip;
        Assert.NotNull(back);
        Assert.Equal(2, back!.W);
        Assert.True(float.IsNaN(back.Rel[2]));
        Assert.Equal("Beech1", Assert.Single(back.Objects).Name);
        Assert.Equal(90, back.Objects[0].Rotation.Y, 3);
        Assert.Equal(3, back.Poly.Count);
        Assert.Equal("kept", back.Name);
        // Pasting works from it at once (Ctrl+V needs no copy first).
        Assert.NotNull(v.View.Paste.Clip);
    }

    [AvaloniaFact]
    public void ABrokenClipboardIsForgotten()
    {
        var p = Prefs.Load(File1);
        p.Set("clipboard", new JsonObject { ["w"] = 2, ["h"] = 2 });
        p.Flush();
        var v = Window();
        Assert.Null(v.View.Paste.Clip);
        Assert.False(v.Prefs.Has("clipboard"));
        // Not even an object: ignored.
        p = Prefs.Load(File1);
        p.Set("clipboard", "text");
        p.Flush();
        Assert.Null(Window().View.Paste.Clip);
    }

    [AvaloniaFact]
    public void TestWindowsRememberNothingOnDisk()
    {
        var w = new MainWindow(load: false);
        w.SeeThroughBox.IsChecked = true;
        w.Prefs.Flush();
        Assert.False(new MainWindow(load: false).SeeThroughBox.IsChecked);
    }
}
