using System.Numerics;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class MapCaves
{
    /// <summary>
    /// Draws the tunnels in the 3D view as the editor's own scene layer: each a tube of rock under the ground, the
    /// ground left undrawn where the tube breaks through it (the mouths). Built on a worker whenever the ground, the
    /// objects or the tunnels change.
    /// </summary>
    public sealed class Scene : ISceneProvider
    {
        /// <inheritdoc/>
        public SceneLayer? Build(IAreaView area)
        {
            Tunnel[] tunnels;
            lock (Tunnels)
            {
                tunnels = [.. Tunnels.Where(t => t.Points.Count >= 2)];
            }
            if (tunnels.Length == 0)
            {
                return null;
            }
            var layer = new SceneLayer();
            int seed = 1;
            foreach (var t in tunnels)
            {
                var line = Line(t, area.GroundHeight);
                if (line.Count < 2)
                {
                    continue;
                }
                var (pos, nrm, col, tri) = Tube(line, t.EndOpen, seed++);
                layer.Meshes.Add(new SceneMesh(pos, nrm, tri) { Colors = col });
                foreach (var (x0, z0, x1, z1) in Mouths(line, area.GroundHeight))
                {
                    layer.Holes.Add(new GroundHole(x0, z0, x1, z1));
                }
            }
            return layer;
        }
    }

    /// <summary>The tunnel nearest a point on the ground, and its point nearest (within reach), or -1 for none.</summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="reach">How near a point must be, metres.</param>
    public static (Tunnel? Tunnel, int Point) Near(float x, float z, float reach)
    {
        Tunnel? best = null;
        int at = -1;
        float bestD = reach;
        foreach (var t in Tunnels)
        {
            for (int i = 0; i < t.Points.Count; i++)
            {
                float d = Vector2.Distance(new Vector2(t.Points[i].X, t.Points[i].Z), new Vector2(x, z));
                if (d < bestD)
                {
                    (best, at, bestD) = (t, i, d);
                }
            }
        }
        return (best, at);
    }
}
