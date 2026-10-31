namespace Groundwork.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The move in progress, or a new one of the selection; null when there is nothing to move.
    /// </summary>
    private Move? Begin()
    {
        if (_move != null)
        {
            return _move;
        }
        var s = Scene;
        if (s == null)
        {
            return null;
        }
        var items = new List<Item>();
        foreach (int i in _view.Selected)
        {
            var t = s.Things[i];
            if (t.Gone)
            {
                continue;
            }
            items.Add(new Item
            {
                Index = i,
                Thing = t,
                Lift = GroundAt(t.Position.X, t.Position.Z) is float g ? t.Position.Y - g : 0,
                Piece = IsPiece(t),
            });
        }
        if (items.Count == 0)
        {
            return null;
        }
        return _move = new Move { Items = items, Cx = items.Average(it => it.Thing.Position.X), Cz = items.Average(it => it.Thing.Position.Z), ByClaude = ClaudeTools.Acting.Value };
    }
}
