using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>The objects drawer: along the bottom or on the left, as big as it was dragged to (within limits), remembered.</summary>
[Collection("DataDir")]
public sealed class ObjectsDrawerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-drawer-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    public void Dispose()
    {
        MapProject.Current = _was;
        Brush.MapOpen = false;
        Inbox.Announce(null);
        Directory.Delete(_dir, true);
    }

    [AvaloniaFact]
    public async Task ItDocksOnTheLeftAndKeepsItsSize()
    {
        var made = MapProject.Create("Drawer", 1, generated: false, height: 2, seed: 1, under: _dir);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenMap(made.Folder);
        var d = w.Drawer!;
        Assert.False(d.Left);
        Assert.Equal(VerticalAlignment.Bottom, d.Card.VerticalAlignment);
        d.Size = 9999;
        Assert.Equal(600, d.Size);
        d.Size = 10;
        Assert.Equal(110, d.Size);

        d.DockOn(true);
        Assert.True(d.Left);
        Assert.Equal("left", w.Prefs.Get("map.drawer.side", ""));
        Assert.Equal(HorizontalAlignment.Left, d.Card.HorizontalAlignment);
        d.Size = 9999;
        Assert.Equal(560, d.Size);
        Assert.Equal(560, d.Card.Width);
        // Each side keeps its own size.
        d.DockOn(false);
        Assert.Equal(110, d.Size);
        Assert.True(double.IsNaN(d.Card.Width));
        w.Close();
    }
}
