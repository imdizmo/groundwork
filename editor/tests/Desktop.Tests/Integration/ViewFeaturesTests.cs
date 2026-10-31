using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Xunit;
using static Groundwork.Desktop.Tests.KeysAndMouseSetup;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The web editor's view features in the native app: the status bar's cursor readout (world x/z, the ground against the
/// original and the ±8 m limit, the zone), and the View panel's Look section (game look on/off, see-through buildings,
/// 3D resolution) and presets (Defaults, All, Ground).
/// </summary>
[Collection("World files")]
public class ViewFeaturesTests
{
    // ---- The cursor readout.

    [Fact]
    public void TheReadoutGivesMetresAndTheGround()
    {
        var s = TestScenes.Flat(2);
        // Grid (64, 64) of a map whose grid starts at -32: x = -32 + 64 = 32.
        Assert.Equal("x 32, z 32   ground 30.00 m (original 30.00, +0.00)", CursorReadout.Text(s.Scene, 64.4f, 63.6f));
        Assert.Equal("x -32, z -32   ground 30.00 m (original 30.00, +0.00)", CursorReadout.Text(s.Scene, 0, 0));
    }

    [Fact]
    public void PointsOutsideTheAreaReadTheNearestEdge()
    {
        var s = TestScenes.Flat(2);
        Assert.StartsWith("x -32, z 96", CursorReadout.Text(s.Scene, -20, 500));
    }

    [Fact]
    public void RaisedAndLoweredGroundIsShownAgainstTheOriginal()
    {
        var s = TestScenes.Flat(2);
        var g = s.Ground;
        int p = 64 * s.Scene.W + 64;
        g.Mod[p] = 1;
        g.Level[p] = 2.5f;
        Assert.Contains("ground 32.50 m (original 30.00, +2.50)", CursorReadout.Text(s.Scene, 64, 64));
        g.Level[p] = -1.25f;
        Assert.Contains("ground 28.75 m (original 30.00, -1.25)", CursorReadout.Text(s.Scene, 64, 64));
        // No limit: far above is far above.
        g.Level[p] = 90;
        Assert.Contains("ground 120.00 m (original 30.00, +90.00)", CursorReadout.Text(s.Scene, 64, 64));
    }

    [Fact]
    public void TheReadoutUsesDotsWhateverTheLanguage()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Contains("ground 30.00 m", CursorReadout.Text(TestScenes.Flat(1).Scene, 10, 10));
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    [Fact]
    public void AnAreaWithoutEditingShowsOnlyItsHeight()
    {
        var s = TestScenes.Flat(1).Scene;
        s.Session = null;
        Assert.Equal("x -22, z -12   ground 30.00 m", CursorReadout.Text(s, 10, 20));
    }

    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects, looking straight down; with the screen
    /// point of a grid point (on the ground at 30 m).
    /// </summary>
    [AvaloniaFact]
    public void TheStatusBarFollowsTheMouseOverTheGround()
    {
        var (w, s, screen) = Open();
        Assert.True(string.IsNullOrEmpty(w.CursorText.Text));
        w.MouseMove(screen(64, 64));
        Assert.StartsWith("x 32, z 32   ground 30.00 m", w.CursorText.Text);
        w.MouseMove(screen(10, 100));
        Assert.StartsWith("x -22, z 68", w.CursorText.Text);
        // The ground changes under a still mouse: shown again (after a stroke, for one).
        s.Ground.Mod[100 * s.Scene.W + 10] = 1;
        s.Ground.Level[100 * s.Scene.W + 10] = 1;
        w.ShowCursor(screen(10, 100));
        Assert.Contains("+1.00", w.CursorText.Text);
        // Off the view: nothing.
        w.ShowCursor(null);
        Assert.Equal("", w.CursorText.Text);
    }

    [AvaloniaFact]
    public void WithoutAnAreaTheReadoutStaysEmpty()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        w.ShowCursor(new Avalonia.Point(500, 500));
        Assert.Equal("", w.CursorText.Text);
    }

    // ---- View: Look.

    [AvaloniaFact]
    public void TheResolutionIsChosenAndSaysItsPixels()
    {
        var (w, _, _) = Open();
        Assert.Equal(GlView.Resolution.Sharp, w.View.Resolution3D);
        var full = w.View.RenderSize();
        w.ResolutionBox.SelectedIndex = 2;
        Assert.Equal(GlView.Resolution.Fast, w.View.Resolution3D);
        var (width, h) = w.View.RenderSize();
        Assert.Equal((int)(w.View.Bounds.Width * 0.75), width);
        Assert.Equal((int)(w.View.Bounds.Height * 0.75), h);
        Assert.True(width < full.W);
        Assert.Equal($"3D resolution: Fast, {width}×{h} pixels.", w.MessageText.Text);
        w.ResolutionBox.SelectedIndex = 1;
        Assert.Equal(GlView.Resolution.Balanced, w.View.Resolution3D);
        Assert.StartsWith("3D resolution: Balanced, ", w.MessageText.Text);
        w.ResolutionBox.SelectedIndex = 0;
        Assert.Equal(full, w.View.RenderSize());
    }

    [Theory]
    [InlineData(GlView.Resolution.Sharp, 1, 1)]
    [InlineData(GlView.Resolution.Sharp, 2, 2)]
    [InlineData(GlView.Resolution.Balanced, 2, 1)]
    [InlineData(GlView.Resolution.Balanced, 0.8, 0.8)]
    [InlineData(GlView.Resolution.Fast, 2, 0.75)]
    [InlineData(GlView.Resolution.Fast, 1, 0.75)]
    [InlineData(GlView.Resolution.Fast, 0.8, 0.6)]
    public void EachResolutionDrawsSoManyPixelsPerPoint(GlView.Resolution r, double screen, double expected) => Assert.Equal(expected, GlView.PixelScale(r, screen), 6);

    [AvaloniaFact]
    public void AViewOfNoSizeStillDrawsAPixel()
    {
        var v = new GlView
        {
            Resolution3D = GlView.Resolution.Fast
        };
        Assert.Equal((1, 1), v.RenderSize());
    }

    [AvaloniaFact]
    public void SwitchingModelsTwiceTheSameWayChangesNothing()
    {
        var v = new GlView
        {
            ModelsOn = true
        };
        Assert.True(v.ModelsOn);
        v.ModelsOn = false;
        v.ModelsOn = false;
        Assert.False(v.ModelsOn);
    }

    // ---- View: presets.

    [AvaloniaFact]
    public void PresetsLeaveTheLookAlone()
    {
        var (w, _, _) = Open();
        w.SeeThroughBox.IsChecked = true;
        w.ShowPreset(MainWindow.ViewPreset.Ground);
        w.ShowPreset(MainWindow.ViewPreset.Defaults);
        Assert.True(w.ModelsBox.IsChecked);
        Assert.True(w.SeeThroughBox.IsChecked);
    }

    [AvaloniaFact]
    public void TheLookSectionComesFirstInTheViewPanel()
    {
        var (w, _, _) = Open();
        var texts = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<TextBlock>().Select(t => t.Text).ToList();
        // The Place panel has an Objects section too: look for the View panel's headings after its LOOK.
        int look = texts.IndexOf("LOOK"), objects = texts.IndexOf("OBJECTS", look + 1);
        Assert.True(look >= 0 && objects > look, $"{look} {objects}");
        Assert.Contains("3D resolution", texts);
    }
}
