using System.Numerics;
using Groundwork.App;
using SkiaSharp;

namespace Groundwork.Desktop;

/// <summary>
/// The Biome tool's biomes when the catalog names none: one for each of the map's layers, with the catalog's kinds that
/// belong on such ground (trees in a forest, plants on grass, rocks on stone). And each biome's swatch for the
/// palette: its ground's colour with its kinds' pictures.
/// </summary>
internal static class MapBiomes
{
    /// <summary>Which groups of kinds a layer's name calls for, by words in it (none: the ground only).</summary>
    private static readonly (string[] Words, string[] Groups)[] Fits =
    [
        (["forest", "wood", "pine", "jungle", "grove", "taiga"], ["Trees", "Bushes"]),
        (["grass", "meadow", "field", "farm", "marsh", "swamp", "moss", "soft"], ["Plants"]),
        (["rock", "stone", "scree", "cliff", "mountain", "gravel", "crag"], ["Rocks"]),
    ];

    /// <summary>A biome for each layer: its ground, and the kinds of the groups its name calls for (each at weight 1).</summary>
    internal static List<MapTools.Biome> FromLayers(IReadOnlyList<MapProject.Layer> layers, IReadOnlyList<MapTools.Kind> kinds) =>
        [.. layers.Select(l =>
        {
            string name = l.Name.ToLowerInvariant();
            var groups = Fits.Where(f => f.Words.Any(name.Contains)).SelectMany(f => f.Groups).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new MapTools.Biome(l.Name, l.Name, 1, kinds.Where(k => groups.Contains(k.Category)).Take(12).ToDictionary(k => k.Model, _ => 1f));
        })];

    /// <summary>Where the swatches are kept (the data folder's, one file a biome, made again when it changes).</summary>
    private static string Folder => Path.Combine(AppSettings.DataDir, "cache", "biomes");

    /// <summary>A biome's swatch, a PNG file: its ground's colour, flecked, with up to three of its kinds' pictures; null when it cannot be written.</summary>
    internal static string? Swatch(MapTools.Biome b, Vector3? ground, IReadOnlyList<MapTools.Kind> kinds)
    {
        try
        {
            var colour = ground ?? new Vector3(0.4f, 0.5f, 0.3f);
            var icons = kinds.Where(k => b.Kinds.ContainsKey(k.Model) && k.Icon != null && File.Exists(k.Icon)).Take(3).Select(k => k.Icon!).ToList();
            string key = $"{b.Name}|{colour}|{string.Join('|', icons)}";
            string file = Path.Combine(Folder, $"{Hash(key)}.png");
            if (File.Exists(file))
            {
                return file;
            }
            _ = Directory.CreateDirectory(Folder);
            using var bmp = new SKBitmap(96, 64);
            using (var g = new SKCanvas(bmp))
            {
                var c = new SKColor((byte)(Math.Clamp(colour.X, 0, 1) * 255), (byte)(Math.Clamp(colour.Y, 0, 1) * 255), (byte)(Math.Clamp(colour.Z, 0, 1) * 255));
                g.Clear(c);
                // flecks, so a flat colour reads as ground
                var rng = new Random(b.Name.Length * 7919);
                using var fleck = new SKPaint { IsAntialias = true };
                for (int i = 0; i < 90; i++)
                {
                    float k = 0.8f + ((float)rng.NextDouble() * 0.4f);
                    fleck.Color = new SKColor((byte)Math.Min(255, c.Red * k), (byte)Math.Min(255, c.Green * k), (byte)Math.Min(255, c.Blue * k));
                    g.DrawCircle(rng.Next(96), rng.Next(64), 1.5f + ((float)rng.NextDouble() * 2), fleck);
                }
                for (int i = 0; i < icons.Count; i++)
                {
                    using var icon = SKBitmap.Decode(icons[i]);
                    if (icon != null)
                    {
                        g.DrawBitmap(icon, SKRect.Create(4 + (i * 30), 18, 40, 40));
                    }
                }
            }
            // ponytail: a swatch per biome and look, never pruned; a few kilobytes each
            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            File.WriteAllBytes(file, data.ToArray());
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Hash(string s) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s)))[..16];
}
