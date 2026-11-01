using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Wires the Select panel to the window and the 3D view: the selection and saved selections, picking a kind and
    /// Replace.
    /// </summary>
    private void WireSelectPanel()
    {
        SelectPanel.Message += Msg;
        SelectPanel.PickKindAsked += PickKind;
        SelectPanel.NameOf = NameOfPrefab;
        // Saved selections are kept per map (its folder's name).
        SelectPanel.WorldName = () => InMap ? MapProject.Current?.Name : null;
        SelectPanel.Things = () => View.Scene?.Things;
        SelectPanel.SelectedIds = () => View.Selected;
        SelectPanel.SelectIds = ids => { Tools.ChooseSelect(); View.Select(ids); };
        SelectPanel.AskName = initial => Dialogs.AskText(this, Lang.T("select.ask.keep.title"), Lang.T("select.ask.keep.text"), initial);
        SelectPanel.Confirm = text => Dialogs.Ask(this, Lang.T("select.ask.forget.title"), text, Lang.T("select.label.forget"));
        View.SelectToolAsked += Tools.ChooseSelect;
    }

    /// <summary>
    /// Wires the ground brushes: stamps, the tools' messages, No limit, loading a stamp picture and the brush's
    /// Alt + click.
    /// </summary>
    private void PaintWholeMap()
    {
        if (Session is not { } s || Tools.Tool is not { } tool)
        {
            return;
        }
        var mask = s.MaskNow();
        if (tool == BrushTool.Erode)
        {
            var b = s.Brush;
            int e = s.EditGround(Lang.T("brush.step.erodeWholeMap"), g => Sculpt.ErodeWhole(g, b.ErodeWater, b.RestAngle, b.Strength, mask, b.Random)).Count;
            Msg(Said.Of("brush.msg.erodeWholeMap", e));
            return;
        }
        if (Brush.PaintOf(tool) is not { } colour)
        {
            return;
        }
        int n = s.EditGround(Lang.T("brush.step.wholeMap", Brush.Label(tool)), g => Sculpt.FillPaint(g, colour, mask)).Count;
        Msg(Said.Of(mask == null ? "brush.msg.wholeMap" : "brush.msg.wholeMapMask", Brush.Label(tool), n));
    }

    private void WireGroundTools()
    {
        View.StampClicked += StampAt;
        Tools.Message += Msg;
        Tools.LoadStampAsked += async () => await LoadStampAsync();
        Tools.WholeMapAsked += PaintWholeMap;
        View.BrushAltClick += OnBrushAltClick;
    }
}
