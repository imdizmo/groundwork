namespace Groundwork.Desktop;

/// <summary>
/// The paint check of the mask: whether a ground point's paint passes the paint rule.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>Whether a ground point's paint passes the paint rule.</summary>
    /// <param name="p">The ground point index.</param>
    /// <returns>True when the paint passes, or the rule does not care about paint.</returns>
    internal bool PaintOk(int p)
    {
        if (_paint == Mask.PaintRule.Any)
        {
            return true;
        }
        bool painted = _paintFlags![p] != 0;
        var paint = _colours!.AsSpan(p * _c, _c);
        float all = 0;
        for (int c = 0; c < _c; c++)
        {
            all += c == Ground.HoleChannel ? 0 : paint[c];
        }
        return _paint switch
        {
            Mask.PaintRule.Unpainted => !painted || all < 0.3f,
            _ when !painted => false,
            Mask.PaintRule.Painted => all >= 0.3f,
            _ => Ground.ChannelOf(_paint - Mask.PaintRule.Layer2 + 1) is int ch && ch < _c && paint[ch] >= 0.5f,
        };
    }
}
