using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The smallest and largest size Resize sets.</summary>
    private const float SmallestSize = 0.05f, LargestSize = 50;

    /// <summary>The first selected object's size (the Select card shows it); null when nothing is selected.</summary>
    public float? Size()
    {
        var s = Scene;
        return s == null ? null : _view.Selected.Where(i => i >= 0 && i < s.Things.Count && !s.Things[i].Gone).Select(i => (float?)s.Things[i].Scale).FirstOrDefault();
    }

    /// <summary>
    /// Sizes the selected objects, each where it stands: to a size, or (by) times a factor of its own. One step in History;
    /// the copies are selected, as after a move.
    /// </summary>
    public void Resize(float size, bool by)
    {
        Commit();
        if (Scene is not { } s || Session is not { } session || !float.IsFinite(size) || size <= 0)
        {
            return;
        }
        var moves = _view.Selected.Where(i => i >= 0 && i < s.Things.Count && !s.Things[i].Gone)
            .Select(i => (i, s.Things[i].Position, s.Things[i].Rotation, Math.Clamp(by ? s.Things[i].Scale * size : size, SmallestSize, LargestSize)))
            .Where(m => MathF.Abs(m.Item4 - s.Things[m.i].Scale) > 1e-4f)
            .ToList();
        if (moves.Count == 0)
        {
            return;
        }
        var copies = session.Transform(moves, Lang.T("history.undo.resized", moves.Count));
        _view.Select(copies);
        Message?.Invoke(Said.Of("select.msg.resized", copies.Count), false);
    }
}
