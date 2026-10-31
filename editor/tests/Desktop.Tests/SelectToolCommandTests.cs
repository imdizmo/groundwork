using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Groundwork.App;
using Xunit;
using static Groundwork.Desktop.Tests.SelectToolRun;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// The Select tool's keys and commands: Same kind, Drop onto what is under the objects or the ground, lifting and
/// turning with the keys, and Delete with nothing selected.
/// </summary>
[Collection("World files")]
public class SelectToolCommandTests
{
    [AvaloniaFact]
    public void SameKindSelectsEveryShownObjectOfTheSelectedKinds()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 30, 50, false), T(3, Wall, 45, 30, 45));
        var said = MessageReader(v.Tool);
        v.Tool.SameKind();
        Assert.StartsWith("Select an object of each kind", said());
        v.W.View.Select([0]);
        v.Tool.SameKind();
        Assert.Equal([0, 1], v.Selected);
        // Hidden kinds are left out.
        v.W.View.SetShown(ObjectKinds.Of("Beech1", false), false);
        v.W.View.Select([0, 2]);
        v.Tool.SameKind();
        Assert.Equal([2], v.Selected);
    }

    // A tree a mod hid 10 km under the ground is not picked with the others of its kind, nor by inverting.
    [AvaloniaFact]
    public void ObjectsBuriedDeepUnderTheGroundAreNotPicked()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, -9962, 50, false), T(3, Wall, 45, 30, 45));
        v.W.View.Select([0]);
        v.Tool.SameKind();
        Assert.Equal([0], v.Selected);
        v.Tool.Invert();
        Assert.Equal([2], v.Selected);
    }

    // Nor by a zone drawn round where it lies, nor by Whole building, even when its box reaches up to the wall above
    // it (touching it).
    [AvaloniaFact]
    public void ObjectsBuriedDeepUnderTheGroundAreNotTakenByAZoneOrWholeBuilding()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 41, -9962, 40));
        var c = v.S.Scene;
        v.W.View.SetBoxes([(1, new Vector3(40 - c.Cx, -9962, -(41 - c.Cz)), new Vector3(42 - c.Cx, 30.5f, -(39 - c.Cz)))]);
        v.Tool.ZoneOnGround = true;
        v.Drag(KeyModifiers.None, (36, 36), (46, 36), (46, 44), (36, 44), (36, 37));
        Assert.Equal([0], v.Selected);
        v.W.View.Select([]);
        v.Tool.WholeBuilding([0]);
        Assert.Equal([0], v.Selected);
    }

    [AvaloniaFact]
    public void KeysLiftAndTurnTheSelection()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.W.View.Select([0]);
        v.Tool.OnGround = false;
        Assert.True(v.Tool.Key(Key.PageUp, false));
        Assert.True(v.Tool.Key(Key.PageUp, true));
        Assert.True(v.Tool.Key(Key.PageDown, false));
        Assert.True(v.Tool.Key(Key.OemPeriod, false));
        Assert.True(v.Tool.Key(Key.OemComma, true));
        Assert.False(v.Tool.Key(Key.Q, false));
        v.Tool.Commit();
        // +0.25 + 1 - 0.25 = +1 m; +1° then -15°.
        Assert.Equal(31, v.Last.Position.Y, 2);
        Assert.Equal(346, (v.Last.Rotation.Y % 360 + 360) % 360, 1);
    }

    [AvaloniaFact]
    public void DeleteWithNothingSelectedSaysSo()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        var said = MessageReader(v.Tool);
        v.Tool.Delete();
        Assert.Equal("Select objects to delete first.", said());
        Assert.False(v.S.CanUndo);
    }
}
