using System.Numerics;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Ends a move: the originals are replaced by copies at their new places, and those get selected. A move that
    /// changed nothing is just dropped: one made with the mouse or keys by less than a centimetre across, a millimetre
    /// up and a thousandth of a degree round. A typed one (exact place) is always made, however small: PlaceAt never
    /// starts one that asks for nothing.
    /// </summary>
    public void Commit()
    {
        _commitTimer.Stop();
        var m = _move;
        _move = null;
        if (m == null)
        {
            return;
        }
        _view.SetPreviews(null);
        if (!m.NoSnap && MathF.Abs(m.Dx) + MathF.Abs(m.Dz) < 0.01f && MathF.Abs(m.Turn) < NoTurn && MathF.Abs(m.Dy) < NoLift && !m.Dropped || Session is not { } session)
        {
            return;
        }
        GroundBlock(m);
        var moves = m.Items.Select(it => (it.Index, Target(m, it), it.Thing.Rotation + new Vector3(0, m.Turn, 0), it.Thing.Scale)).ToList();
        // The step is whoever started the move's: a key move of the user's committed by a control Claude sets (the
        // Select panel's Apply) stays the user's, and one Claude started stays Claude's when the timer commits it.
        bool acting = ClaudeTools.Acting.Value;
        ClaudeTools.Acting.Value = m.ByClaude;
        List<int> copies;
        try
        {
            copies = session.Transform(moves, Lang.T("history.undo.moved", moves.Count));
        }
        finally
        {
            ClaudeTools.Acting.Value = acting;
        }
        _view.Select(copies);
        Message?.Invoke(Said.Of("select.msg.moved", copies.Count, Said.Empty), false);
    }
}
