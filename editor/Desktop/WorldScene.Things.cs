namespace Groundwork.Desktop;

public sealed partial class WorldScene
{
    /// <summary>
    /// After an edit: the heights and layer weights of a rectangle of grid points from the ground
    /// (grid points x0..x1, z0..z1, inclusive).
    /// </summary>
    public void Refresh(Ground g, int x0, int z0, int x1, int z1)
    {
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int p = gz * W + gx;
                Heights[p] = g.HeightOf(p);
                for (int c = 0; c < 4; c++)
                {
                    Mask[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(g.MaskOf(p, c) * 255f), 0, 255);
                }
            }
        }
    }
}
