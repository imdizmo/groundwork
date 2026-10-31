using System.Numerics;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The ground's colour: the map's first layer, each painted layer over it by its weight; the layer a point mostly is.
/// </summary>
public class ShadersTests
{
    [Fact]
    public void TheGroundIsItsLayersColoursBlendedByTheirPaint()
    {
        var s = TestScenes.Flat(1).Scene;
        s = new WorldScene
        {
            Name = s.Name,
            W = s.W,
            H = s.H,
            Heights = s.Heights,
            Things = s.Things,
            Mask = s.Mask,
            LayerColours = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ],
        };
        Assert.Equal(Vector3.Zero, s.GroundColor(0));
        Assert.Equal(0, s.LayerAt(0));

        s.Mask[4] = 255;          // point 1: the second layer
        s.Mask[(2 * 4) + 1] = 128; // point 2: half the third layer
        Assert.Equal(Vector3.UnitX, s.GroundColor(1));
        Assert.Equal(1, s.LayerAt(1));
        Assert.Equal(0.5f, s.GroundColor(2).Y, 2);
        Assert.Equal(2, s.LayerAt(2));
        s.Mask[(2 * 4) + 1] = 120; // less than half: the first layer still
        Assert.Equal(0, s.LayerAt(2));
    }
}
