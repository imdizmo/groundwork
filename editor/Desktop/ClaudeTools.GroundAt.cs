using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The most points ground_at answers for at once.</summary>
    private const int MostPoints = 500;

    /// <summary>The ground's slope at a grid point, degrees (from its neighbours a metre either way).</summary>
    internal static float SlopeAt(Ground g, int gx, int gz)
    {
        int l = Math.Max(0, gx - 1), r = Math.Min(g.W - 1, gx + 1), u = Math.Max(0, gz - 1), d = Math.Min(g.H - 1, gz + 1);
        float dx = (g.HeightOf((gz * g.W) + r) - g.HeightOf((gz * g.W) + l)) / Math.Max(1, r - l);
        float dz = (g.HeightOf((d * g.W) + gx) - g.HeightOf((u * g.W) + gx)) / Math.Max(1, d - u);
        return MathF.Atan(MathF.Sqrt((dx * dx) + (dz * dz))) * 180 / MathF.PI;
    }

    /// <summary>ground_at: the ground at world points of the open area: its height now, as generated, its biome.</summary>
    [McpServerTool(Name = "ground_at", ReadOnly = true, Title = "The ground at points")]
    [Description("The ground at points of the open map, exactly: its height now (between the 1 m grid points, as the editor draws it), its height when the map was opened (the difference is what edits changed), the layer it mostly is, its slope in degrees, and how deep water stands there (0: dry). Up to 500 points at once. Use it to set objects' heights, check slopes and levels, and read a place exactly.")]
    public Task<string> GroundAtPoints(
        [Description("The points: [[x, z], [x, z], …].")] float[][] points) => OnUi(() =>
    {
        if (_w.Session is not { } s)
        {
            return "No map is open (map_open).";
        }
        if (points.Length == 0 || points.Length > MostPoints || points.Any(p => p.Length < 2))
        {
            return $"Give 1 to {MostPoints} points, each [x, z].";
        }
        var scene = s.Scene;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        float ox = scene.OriginX, oz = scene.OriginZ;
        return ToJson(points.Select(p =>
        {
            float x = p[0], z = p[1];
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                return (object)new { x, z, error = "outside the map" };
            }
            int gx = Math.Clamp((int)MathF.Round(x - ox), 0, scene.W - 1), gz = Math.Clamp((int)MathF.Round(z - oz), 0, scene.H - 1), i = gz * scene.W + gx;
            float h = Picking.HeightAt(scene, x - scene.Cx, -(z - scene.Cz));
            return new
            {
                x,
                z,
                height = MathF.Round(h, 3),
                original = MathF.Round(s.Ground.Original(i), 3),
                layer = MapProject.Current?.Layers.ElementAtOrDefault(scene.LayerAt(i))?.Name ?? scene.LayerAt(i).ToString(),
                slope = MathF.Round(SlopeAt(s.Ground, gx, gz), 1),
                water = MathF.Round(MathF.Max(0, MathF.Max(scene.Water, MapWater.SurfaceAt(x, z) ?? float.MinValue) - h), 2),
            };
        }));
    });
}
