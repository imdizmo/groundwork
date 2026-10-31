using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// End: each selected object drops from where it is onto whatever is under it: the top of another object (OnTop),
    /// or else the ground.
    /// </summary>
    public void Drop()
    {
        if (Begin() is not { } m)
        {
            return;
        }
        int onObjects = 0;
        foreach (var it in m.Items)
        {
            var p = Target(m, it);
            // Past the area's edge there is no ground: only objects below, else it stays as high as it is.
            float ground = GroundAt(p.X, p.Z) ?? p.Y, y = ground;
            if (OnTop(m, it, p, ground) is float top)
            {
                y = top;
                onObjects++;
            }
            it.Lift = y - ground - m.Dy;
        }
        m.Dropped = true;
        Preview(m);
        Later();
        Message?.Invoke(Said.Of("select.msg.dropped", m.Items.Count, onObjects, m.Items.Count - onObjects), false);
    }
}
