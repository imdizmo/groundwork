using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Stamp once (Raise or Lower with a stamp): the whole picture into the ground, one undo step.
    /// </summary>
    /// <param name="gx">Grid point, east.</param>
    /// <param name="gz">Grid point, north.</param>
    internal void StampAt(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var b = Tools.Brush;
        float amount = b.StampHeight * (Tools.Tool == BrushTool.Lower ? -1 : 1);
        s.EditGround(Lang.T("brush.shape.stamp", b.StampLabel), g => Sculpt.StampOnce(g, b, gx, gz, amount, s.MaskNow()));
        Msg(amount >= 0 ? Said.Of("brush.msg.stampedUp", MathF.Abs(amount)) : Said.Of("brush.msg.stampedDown", MathF.Abs(amount)));
        UpdateSaveBar();
    }
}
