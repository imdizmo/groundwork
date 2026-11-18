using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;
using static Groundwork.Desktop.Tests.BareGlView;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Picking with a camera kept from a picture (GlView.ProbeAt, FractionOf; Claude's pick and where_drawn): a ground point
/// is found where the view's own picking finds it, and back at its place on the picture.
/// </summary>
public class GlViewProbeTests
{
    [AvaloniaFact]
    public void AGroundPointProbesAndProjectsBackToItsPlace()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        var at = b.V.ScreenOfGrid(new Vector2(40, 50), 0)!.Value;
        var world = b.V.WorldAt(at, ViewSize)!.Value;
        var (vp, _) = b.V.LastCamera!.Value;
        float u = (float)(at.X / ViewSize.Width), v = (float)(at.Y / ViewSize.Height);
        var (obj, ground) = b.V.ProbeAt(vp, u, v);
        Assert.Null(obj);
        Assert.NotNull(ground);
        Assert.Equal(world.X, ground.Value.X, 1);
        Assert.Equal(world.Z, ground.Value.Z, 1);
        var back = b.V.FractionOf(vp, ground.Value)!.Value;
        Assert.Equal(u, back.X, 3);
        Assert.Equal(v, back.Y, 3);
    }

    [AvaloniaFact]
    public void APointBehindTheCameraHasNoPlace()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        b.V.SetCamera(new Vector3(0, 20, 0), new Vector3(0, 0, 1), (float)(ViewSize.Width / ViewSize.Height));
        var (vp, _) = b.V.LastCamera!.Value;
        var s = b.V.Scene!;
        Assert.Null(b.V.FractionOf(vp, new Vector3(s.Cx, 40, s.Cz + 30)));   // above and behind the eye
    }

    [AvaloniaFact]
    public async Task PickAndWhereDrawnNeedAPictureFirst()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gw-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = MapProject.Create("Probe", 2, generated: false, height: 10, seed: 1, under: dir).Read().Session!;
            var t = new ClaudeTools(TestWindows.Editing(s));
            Assert.StartsWith("No picture", await t.Pick([[1, 1]]));
            Assert.StartsWith("No picture", await t.WhereDrawn([[0, 0]]));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
