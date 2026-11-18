using System.Numerics;
using System.Text.Json.Nodes;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// The map's names: names on places (a town, a peak, a bay) and named regions (an outline on the ground), kept in its
/// names file. World metres (x east, z north). A name has a size: a region's, a place's or a small place's; the view
/// shows the small ones only up close (GlView.Names).
/// </summary>
public static class MapNames
{
    /// <summary>How big a name is, and from how far it shows.</summary>
    public enum Size
    {
        /// <summary>A region's: big, spaced capitals, seen from anywhere.</summary>
        Region,
        /// <summary>A place's: a town, a bay, a peak.</summary>
        Place,
        /// <summary>A small place's: a farm, a well, a camp; shown up close only.</summary>
        Small,
    }

    /// <summary>A name on the map: its text, where (world metres), its size.</summary>
    public sealed class Label
    {
        /// <summary>The text shown.</summary>
        public string Text { get; set; } = "Place";
        /// <summary>World x, metres (east).</summary>
        public float X { get; set; }
        /// <summary>World z, metres (north).</summary>
        public float Z { get; set; }
        /// <summary>Its size.</summary>
        public Size Size { get; set; } = Size.Place;
    }

    /// <summary>A named region: an outline on the ground (world metres, closed back to its first point), its colour (0..1).</summary>
    public sealed class Region
    {
        /// <summary>Its name, shown over its middle at the region size.</summary>
        public string Name { get; set; } = "Region";
        /// <summary>The outline's points, x and z in world metres.</summary>
        public List<Vector2> Points { get; } = [];
        /// <summary>Its colour.</summary>
        public Vector3 Colour { get; set; } = new(0.95f, 0.85f, 0.55f);
    }

    /// <summary>The names on places.</summary>
    public static List<Label> Labels { get; } = [];
    /// <summary>The regions.</summary>
    public static List<Region> Regions { get; } = [];
    /// <summary>Called when they change.</summary>
    public static event Action? Changed;

    /// <summary>The longest name kept.</summary>
    public const int MostText = 80;

    /// <summary>Reads the map's names file (none: no names).</summary>
    public static void Load(string file)
    {
        Read(File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject : null);
        Changed?.Invoke();
        ModExtensions.Redraw();
    }

    /// <summary>Whether there is nothing to keep.</summary>
    public static bool None => Labels.Count == 0 && Regions.Count == 0;

    private static void Read(JsonObject? o)
    {
        Labels.Clear();
        Regions.Clear();
        static float F(JsonNode? n, float d) => n is JsonValue v && v.TryGetValue(out float f) && float.IsFinite(f) ? f : d;
        static string T(JsonNode? n, string d) => n is JsonValue v && v.TryGetValue(out string? s) && s.Trim().Length > 0 ? Clip(s) : d;
        foreach (var l in o?["names"] as JsonArray ?? [])
        {
            var size = T(l?["size"], "place") switch { "region" => Size.Region, "small" => Size.Small, _ => Size.Place };
            Labels.Add(new Label { Text = T(l?["text"], "Place"), X = F(l?["x"], 0), Z = F(l?["z"], 0), Size = size });
        }
        foreach (var r in o?["regions"] as JsonArray ?? [])
        {
            var region = new Region { Name = T(r?["name"], "Region") };
            if (r?["color"] is JsonArray c && c.Count >= 3)
            {
                region.Colour = new Vector3(Math.Clamp(F(c[0], 1), 0, 1), Math.Clamp(F(c[1], 1), 0, 1), Math.Clamp(F(c[2], 1), 0, 1));
            }
            foreach (var p in r?["points"] as JsonArray ?? [])
            {
                if (p is JsonArray xz && xz.Count >= 2)
                {
                    region.Points.Add(new Vector2(F(xz[0], 0), F(xz[1], 0)));
                }
            }
            if (region.Points.Count >= 3)
            {
                Regions.Add(region);
            }
        }
    }

    /// <summary>A name as kept: trimmed, one line, at most MostText characters.</summary>
    public static string Clip(string s)
    {
        string one = string.Join(' ', s.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return one.Length > MostText ? one[..MostText] : one;
    }

    /// <summary>Writes the map's names file (with no names and no file yet, nothing is written).</summary>
    public static void Save(string file)
    {
        if (None && !File.Exists(file))
        {
            return;
        }
        File.WriteAllText(file, Snapshot());
    }

    /// <summary>The names as the file holds them (an undo step keeps two of these).</summary>
    public static string Snapshot()
    {
        static float R(float v) => MathF.Round(v * 100f) / 100f;
        return new JsonObject
        {
            ["note"] = "World metres: x east, z north (Unreal X = x, Y = -z). size: region, place or small. A region's points go round its outline.",
            ["names"] = new JsonArray([.. Labels.Select(l => (JsonNode)new JsonObject
            {
                ["text"] = l.Text, ["x"] = R(l.X), ["z"] = R(l.Z), ["size"] = l.Size.ToString().ToLowerInvariant(),
            })]),
            ["regions"] = new JsonArray([.. Regions.Select(r => (JsonNode)new JsonObject
            {
                ["name"] = r.Name,
                ["color"] = new JsonArray(R(r.Colour.X), R(r.Colour.Y), R(r.Colour.Z)),
                ["points"] = new JsonArray([.. r.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y)))]),
            })]),
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Back to a snapshot (undo, redo).</summary>
    public static void Restore(string snapshot)
    {
        Read(JsonNode.Parse(snapshot) as JsonObject);
        Touch();
    }

    /// <summary>Tells the view and the tool they changed.</summary>
    public static void Touch()
    {
        Changed?.Invoke();
        ModExtensions.Redraw();
    }

    /// <summary>Whether a point (world x, z) is inside a region's outline (even-odd).</summary>
    public static bool Holds(Region r, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = r.Points.Count - 1; i < r.Points.Count; j = i++)
        {
            Vector2 a = r.Points[i], b = r.Points[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Where a region's name stands: the middle of its outline's area (its points' mean when the area is nil).</summary>
    public static Vector2 Middle(Region r)
    {
        float area = 0, cx = 0, cz = 0;
        for (int i = 0, j = r.Points.Count - 1; i < r.Points.Count; j = i++)
        {
            Vector2 a = r.Points[j], b = r.Points[i];
            float cross = (a.X * b.Y) - (b.X * a.Y);
            area += cross;
            cx += (a.X + b.X) * cross;
            cz += (a.Y + b.Y) * cross;
        }
        if (MathF.Abs(area) < 1e-3f)
        {
            var sum = Vector2.Zero;
            foreach (var p in r.Points)
            {
                sum += p;
            }
            return sum / Math.Max(1, r.Points.Count);
        }
        return new Vector2(cx / (3 * area), cz / (3 * area));
    }

    /// <summary>The regions holding a point (world x, z), innermost (smallest) first: what a game says on entering.</summary>
    public static IEnumerable<Region> At(Vector2 p) => Regions.Where(r => Holds(r, p)).OrderBy(r => MathF.Abs(Area(r)));

    /// <summary>A region's area, square metres (signed by the outline's turn).</summary>
    public static float Area(Region r)
    {
        float a = 0;
        for (int i = 0, j = r.Points.Count - 1; i < r.Points.Count; j = i++)
        {
            a += (r.Points[j].X * r.Points[i].Y) - (r.Points[i].X * r.Points[j].Y);
        }
        return a / 2;
    }

    /// <summary>A name no label or region has yet: the word and the first free number.</summary>
    public static string NewName(string word)
    {
        int n = 1;
        while (Labels.Any(l => l.Text == $"{word} {n}") || Regions.Any(r => r.Name == $"{word} {n}"))
        {
            n++;
        }
        return $"{word} {n}";
    }
}
