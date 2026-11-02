using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// The 3D view's light: a midday sun, its ambient and sky colours and the fog, as linear colours for the shaders, and
/// the GLSL every shader shares (the fog and the screen's colour curve).
/// </summary>
public static class Lighting
{
    // Midday light, gamma-space colours turned linear.
    /// <summary>
    /// An sRGB (gamma-space) colour channel, 0..1, as linear.
    /// </summary>
    private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    /// <summary>
    /// An sRGB colour as linear.
    /// </summary>
    private static Vector3 Srgb(float r, float g, float b) => new(Lin(r), Lin(g), Lin(b));
    /// <summary>
    /// The direction towards the sun, in Unity's world space.
    /// </summary>
    public static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-0.42f, 0.72f, 0.55f));
    /// <summary>
    /// The sun's light (linear, past 1).
    /// </summary>
    public static readonly Vector3 SunColor = Srgb(1.0f, 0.886f, 0.742f) * 1.7f;
    /// <summary>
    /// The ambient light (linear), given to the terrain shader as its constant spherical harmonics term.
    /// </summary>
    public static readonly Vector3 Ambient = Srgb(0.463f, 0.574f, 0.706f);
    /// <summary>
    /// The fog's colour (linear).
    /// </summary>
    public static readonly Vector3 FogColor = Srgb(0.302f, 0.581f, 0.747f);
    /// <summary>
    /// The fog's colour looking towards the sun (linear).
    /// </summary>
    public static readonly Vector3 SunFogColor = Srgb(0.801f, 0.687f, 0.43f);
    /// <summary>
    /// The exponential-squared fog's density, per metre.
    /// </summary>
    public const float FogDensity = 0.001f;   // objects only (the ground has no fog): a haze over a kilometre or more, not a wall
    /// <summary>
    /// Where the fog starts, in metres from the camera (the view sets it each frame: the orbit's distance, so zooming
    /// out does not fog the area over; 0 walking or flying, as in game).
    /// </summary>
    public static float FogStart { get; set; }
    /// <summary>
    /// The sky's colour straight up (linear).
    /// </summary>
    public static readonly Vector3 SkyTop = Srgb(0.2f, 0.42f, 0.68f);
    /// <summary>
    /// The sky's colour at the horizon (linear).
    /// </summary>
    public static readonly Vector3 SkyHorizon = Srgb(0.55f, 0.72f, 0.84f);
    /// <summary>
    /// The sun in view space (z mirrored), for the objects' simpler lighting.
    /// </summary>
    public static Vector3 SunDirView => new(SunDir.X, SunDir.Y, -SunDir.Z);

    /// <summary>
    /// GLSL shared by the terrain, sky, water and object shaders: the fog (applyFogDir, fogColorFor) and toSRGB (linear
    /// to the screen's sRGB with a soft shoulder).
    /// </summary>
    public const string Common = """
		uniform vec3 uFogColor;
		uniform vec3 uSunFogColor;
		uniform float uFogDensity;
		// Where the fog starts (m from the camera): the point the camera turns around, so the area
		// worked on stays clear however far the view is zoomed out; only what lies beyond it fades.
		uniform float uFogStart;
		// Unity exponential-squared fog; looking towards the sun the fog takes the sun fog colour.
		vec3 fogColorFor(vec3 viewDir, vec3 sunDir) {
		  return mix(uFogColor, uSunFogColor, pow(max(dot(viewDir, sunDir), 0.0), 6.0));
		}
		vec3 applyFogDir(vec3 c, float dist, vec3 viewDir, vec3 sunDir) {
		  float f = 1.0 - exp(-pow(max(dist - uFogStart, 0.0) * uFogDensity, 2.0));
		  return mix(c, fogColorFor(viewDir, sunDir), clamp(f, 0.0, 1.0));
		}
		vec3 toSRGB(vec3 c) {
		  c = max(c, vec3(0.0));
		  // Filmic shoulder so the sun-lit snow does not clip hard.
		  c = c / (1.0 + max(c - 0.8, 0.0));
		  return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
		}
		""";
}
