namespace Groundwork.Desktop;

// Before a frame is drawn: a new area is set up, the game's look is made, and what changed since the last frame
// (groups, shapes, overlays) is built again.
public sealed partial class GlView
{
    /// <summary>
    /// Sets up what the frame needs before it is drawn: a new area's ground and objects, the game's look (made once
    /// the ground is on the graphics card), and the objects reset.
    /// </summary>
    /// <param name="s">The area shown (null: none).</param>
    private void PrepareScene(WorldScene? s)
    {
        if (s != null && _sceneDirty)
        {
            _sceneDirty = false;
            // Another area: the last one's objects go (the models stay on the graphics card, unless
            // they are past the budget).
            DropObjects();
            if (_gpuBytes > GpuBudget)
            {
                DropModels();
            }
            BuildTerrain(s);
            StartModels(s);
        }
        if (s != null)
        {
            PrepareLayers(s);
        }
        if (s != null && _thingsReset)
        {
            _thingsReset = false;
            DropObjects();
            StartModels(s);
        }
    }
}
