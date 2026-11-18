using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

/// <summary>
/// The last screenshot's camera, and what is where on that picture: pick (a pixel to the object and the ground there)
/// and where_drawn (a world point to its pixel), so a change can be checked on the picture without working out
/// coordinates by hand.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>The camera of a screenshot: the scene it showed, its view × projection, its eye and target in world
    /// metres, its turn and tilt in degrees, distance in metres, and the picture's size as sent.</summary>
    internal sealed record Shot(WorldScene Scene, Matrix4x4 ViewProj, Vector3 Eye, Vector3 Target, float Yaw, float Pitch, float Distance, int Width, int Height);

    /// <summary>The last single screenshot's camera (null: none yet, four views, or it failed).</summary>
    private Shot? _shot;

    /// <summary>Most points pick or where_drawn take at once.</summary>
    private const int MostProbes = 200;

    /// <summary>The camera of a picture in words, sent with it.</summary>
    private static string CameraSays(Shot? c) => c == null ? "" : FormattableString.Invariant(
        $"Camera: looking at x {c.Target.X:0.#}, z {c.Target.Z:0.#} (height {c.Target.Y:0.#}) from yaw {c.Yaw:0.#}°, pitch {c.Pitch:0.#}° down, {c.Distance:0.#} m away; eye at x {c.Eye.X:0.#}, z {c.Eye.Z:0.#}, height {c.Eye.Y:0.#}; vertical field of view 60°. The picture is {c.Width} x {c.Height} pixels, [0, 0] its top left: pick says what is at its pixels, where_drawn where world points are on it.");

    /// <summary>Why the last picture's camera cannot be used now, or null.</summary>
    private string? NoShot() =>
        _w.Session is not { } s ? "No map is open (map_open)."
        : _shot == null ? "No picture to read: take one with screenshot (one view; four views cannot be read)."
        : _shot.Scene != s.Scene ? "The last picture was of another area: take a screenshot again."
        : null;

    /// <summary>pick: what is at pixels of the last screenshot.</summary>
    [McpServerTool(Name = "pick", ReadOnly = true, Title = "What is at pixels of the last picture")]
    [Description("What is at pixels of the last screenshot (one view), as a click there would find it, on the map as it is now: the object (id as list_objects gives it, name, kind) unless the ground is nearer, and the ground point the pixel looks at (x, height, z, its layer; none: sky). Use it to find where something drawn is, or what a spot of the picture is, instead of working out coordinates. Up to 200 pixels.")]
    public Task<string> Pick([Description("The pixels: [[px, py], …], from the picture's top left, in the picture's own size.")] float[][] pixels) => OnUi(() =>
    {
        if (NoShot() is string why)
        {
            return why;
        }
        if (pixels.Length == 0 || pixels.Length > MostProbes || pixels.Any(p => p.Length < 2))
        {
            return $"Give 1 to {MostProbes} pixels, each [px, py].";
        }
        var c = _shot!;
        var scene = c.Scene;
        int reads = scene.Reads;
        bool gave = false;
        string json;
        lock (scene.Things)
        {
            json = ToJson(pixels.Select(p =>
            {
                float px = p[0], py = p[1];
                if (px < 0 || py < 0 || px > c.Width || py > c.Height)
                {
                    return (object)new { px, py, error = $"outside the picture ({c.Width} x {c.Height})" };
                }
                var (hit, ground) = _w.View.ProbeAt(c.ViewProj, px / c.Width, py / c.Height);
                object? obj = null;
                if (hit is int i && i < scene.Things.Count && !scene.Things[i].Gone)
                {
                    obj = Describe(scene.Things[i], i);
                    gave = true;
                }
                object? at = ground is { } g ? new
                {
                    x = MathF.Round(g.X, 2),
                    height = MathF.Round(g.Y, 2),
                    z = MathF.Round(g.Z, 2),
                    layer = LayerName(scene, g.X, g.Z),
                } : null;
                return new { px, py, @object = obj, ground = at };
            }));
        }
        if (gave)
        {
            GaveIds(scene, reads);
        }
        return json;
    });

    /// <summary>The layer the ground mostly is at a world point.</summary>
    private static string LayerName(WorldScene scene, float x, float z)
    {
        int gx = Math.Clamp((int)MathF.Round(x - scene.OriginX), 0, scene.W - 1), gz = Math.Clamp((int)MathF.Round(z - scene.OriginZ), 0, scene.H - 1);
        int k = scene.LayerAt((gz * scene.W) + gx);
        return MapProject.Current?.Layers.ElementAtOrDefault(k)?.Name ?? k.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>where_drawn: where world points are on the last screenshot.</summary>
    [McpServerTool(Name = "where_drawn", ReadOnly = true, Title = "Where points are on the last picture")]
    [Description("Where world points are on the last screenshot (one view): each one's pixel [px, py] from the picture's top left, or that it is off the picture or behind the camera. Give [x, z] for the ground there, or [x, height, z]. Use it to find a place or an object (its x, y, z from list_objects) on the picture. Whether something nearer hides it is not checked: pick its pixel for that. Up to 200 points.")]
    public Task<string> WhereDrawn([Description("The points: [[x, z], …] on the ground, or [[x, height, z], …].")] float[][] points) => OnUi(() =>
    {
        if (NoShot() is string why)
        {
            return why;
        }
        if (points.Length == 0 || points.Length > MostProbes || points.Any(p => p.Length is < 2 or > 3))
        {
            return $"Give 1 to {MostProbes} points, each [x, z] or [x, height, z].";
        }
        var c = _shot!;
        var scene = c.Scene;
        return ToJson(points.Select(p =>
        {
            float x = p[0], z = p[^1];
            if (p.Length == 2 && Bounds(scene) is var (minX, minZ, maxX, maxZ) && (x < minX || x > maxX || z < minZ || z > maxZ))
            {
                return (object)new { x, z, error = "outside the map: give its height, [x, height, z]" };
            }
            float y = p.Length == 3 ? p[1] : Picking.HeightAt(scene, x - scene.Cx, -(z - scene.Cz));
            if (_w.View.FractionOf(c.ViewProj, new Vector3(x, y, z)) is not { } f)
            {
                return new { x, height = MathF.Round(y, 2), z, error = "behind the camera" };
            }
            float px = MathF.Round(f.X * c.Width, 1), py = MathF.Round(f.Y * c.Height, 1);
            bool on = f.X is >= 0 and <= 1 && f.Y is >= 0 and <= 1;
            return on ? new { x, height = MathF.Round(y, 2), z, px, py } : new { x, height = MathF.Round(y, 2), z, px, py, error = "off the picture" };
        }));
    });
}
