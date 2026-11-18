using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

/// <summary>
/// Claude's pictures of the 3D view (screenshot): from a camera given or as it is, no wider than 1280 pixels.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// screenshot: the 3D view (from the camera given, else as it is) or the map, as a JPEG no wider than 1280
    /// pixels; four views in one with <c>views</c> 4 or more.
    /// </summary>
    [McpServerTool(Name = "screenshot", ReadOnly = true, Title = "See the view")]
    [Description("A picture of what the editor shows: the 3D view of the map (optionally from a camera: looking at x, z from yaw degrees round, pitch degrees down, distance metres away). Use it to see the result of a change.")]
    public async Task<ModelContextProtocol.Protocol.CallToolResult> Screenshot(
        [Description("World x the camera looks at (optional: the view as it is).")] float? x = null,
        [Description("World z the camera looks at.")] float? z = null,
        [Description("Round the point, in degrees (0: looking north).")] float yaw = 45,
        [Description("Down from level, in degrees (90: from straight above).")] float pitch = 55,
        [Description("Metres from the point.")] float distance = 120,
        [Description("Height of the point looked at (optional: the ground's).")] float? height = null,
        [Description("4: four pictures in one (from above, and from the south-west, east and north-west, all at the point, or the map's middle), to see a group or a place from every side. 1: one picture (default).")] int views = 1)
    {
        if (LoneCoordinate(x, z) is string lone)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = lone } } };
        }
        if (views >= 4)
        {
            _shot = null;
            return await FourViews(x, z, distance, height);
        }
        string path = Path.Combine(Path.GetTempPath(), $"gw-claude-{Guid.NewGuid():N}.png");
        _shot = null;
        try
        {
            string? problem = await WithCamera(async () =>
            {
                if (_w.Session == null)
                {
                    return "Nothing to see: open a map (map_open).";
                }
                // A read-only look: the user's camera is put back once the picture is taken (ShootView).
                Action? orbit = x is float cx && z is float cz ? () => _w.View.Orbit(cx, cz, yaw, pitch, distance, height) : null;
                _shot = await ShootView(path, orbit);
                return null;
            });
            if (problem != null)
            {
                return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = problem } } };
            }
            byte[] jpeg = Shrink(await File.ReadAllBytesAsync(path), out int pw, out int ph);
            _shot = _shot is { } shot ? shot with { Width = pw, Height = ph } : null;
            return new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(jpeg, "image/jpeg"), new ModelContextProtocol.Protocol.TextContentBlock { Text = CameraSays(_shot) } } };
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>unreal_screenshot: the open Unreal level from a camera, as the game draws it.</summary>
    [McpServerTool(Name = "unreal_screenshot", ReadOnly = true, Title = "See the Unreal level")]
    [Description("A picture of the open Unreal level as the game draws it (real materials, fog, lighting, post-process), from a camera looking at x, z (editor coordinates, as screenshot) from yaw degrees round (0: looking north), pitch degrees down and distance metres away. While the game is playing (map_play_here), from the running game. Use it to judge the mood and the light, which the editor's own view does not show. Needs Unreal with the Groundwork Bridge plugin.")]
    public async Task<ModelContextProtocol.Protocol.CallToolResult> UnrealScreenshot(float x, float z, float yaw = 45, float pitch = 20, float distance = 40,
        [Description("Height of the point looked at (optional: the ground's, plus 2 m).")] float? height = null, float fov = 70)
    {
        float? ground = await OnUi(() => _w.Session is { } s ? new SceneLayers.AreaView(s.Scene).GroundHeight(x, z) : (float?)null);
        var target = new System.Numerics.Vector3(x, height ?? (ground ?? 0) + 2, z);
        var (png, said) = await UnrealLive.Screenshot(target, yaw, pitch, distance, fov, 1280, 720);
        return png == null
            ? new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = said } } }
            : new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(Shrink(png), "image/jpeg"), new ModelContextProtocol.Protocol.TextContentBlock { Text = said } } };
    }

    /// <summary>
    /// Saves a picture of the 3D view to <paramref name="path"/>, from the camera <paramref name="orbit"/> points (none: the
    /// view as it is), and puts the user's camera back afterwards, so that a look does not move it. On the window's thread,
    /// within the camera's turn (WithCamera).
    /// </summary>
    /// <param name="path">The PNG file to write.</param>
    /// <param name="orbit">Points the camera, or null to keep it as it is.</param>
    /// <returns>The camera the picture was taken with (for pick and where_drawn), or null without an area.</returns>
    internal async Task<Shot?> ShootView(string path, Action? orbit)
    {
        var kept = _w.View.WorldCamera;
        try
        {
            orbit?.Invoke();
            await ViewPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
            return _w.Session is { } s && _w.View.LastCamera is var (vp, eye) && _w.View.WorldCamera is var (yaw, pitch, distance, target)
                ? new Shot(s.Scene, vp, eye, target, yaw * 180 / MathF.PI, pitch * 180 / MathF.PI, distance, 0, 0)
                : null;
        }
        finally
        {
            _w.View.WorldCamera = kept;
        }
    }

    /// <summary>A picture no wider than 1280 pixels, as a JPEG (smaller to send and to look at).</summary>
    internal static byte[] Shrink(byte[] png) => Shrink(png, out _, out _);

    /// <summary>A picture no wider than 1280 pixels, as a JPEG, and its size.</summary>
    internal static byte[] Shrink(byte[] png, out int width, out int height)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        float k = Math.Min(1f, 1280f / bitmap.Width);
        using var scaled = k < 1 ? bitmap.Resize(new SkiaSharp.SKImageInfo((int)(bitmap.Width * k), (int)(bitmap.Height * k)), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear)) : bitmap.Copy();
        (width, height) = (scaled.Width, scaled.Height);
        using var image = SkiaSharp.SKImage.FromBitmap(scaled);
        return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray();
    }
}
