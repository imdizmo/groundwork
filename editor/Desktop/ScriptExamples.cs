namespace Groundwork.Desktop;

/// <summary>The script window's examples: each a whole script, ready to run on any map, and to start one's own from.</summary>
public static class ScriptExamples
{
    /// <summary>An example: its name in the list and its code.</summary>
    public sealed record Example(string Name, string Code);

    /// <summary>The examples, in the list's order.</summary>
    public static readonly Example[] All =
    [
        new("Mound in the middle", """
            // A mound 10 m high and 30 m across, in the middle of the map.
            Ground.Shape(Map.CenterX, Map.CenterZ, 15, (dx, dz) =>
            {
                float d = MathF.Sqrt(dx * dx + dz * dz) / 15;   // 0 in the middle, 1 at the edge
                return 10 * (1 - d);                            // metres to add
            });
            Print($"The top is at {Ground.Height(Map.CenterX, Map.CenterZ):0.0} m.");
            """),
        new("Terraced hill", """
            // A hill in the middle of the map cut into flat terraces, the steep steps painted with the last layer.
            float cx = Map.CenterX, cz = Map.CenterZ, radius = 60, height = 30, steps = 6;
            // The plants, trees and rocks it would bury go first.
            foreach (var o in Objects.Near(cx, cz, radius).Where(o => o.Group is "Trees" or "Rocks" or "Bushes" or "Plants"))
                Objects.Remove(o);
            float step = height / steps;
            Ground.Shape(cx, cz, radius, (dx, dz) =>
            {
                float d = MathF.Sqrt(dx * dx + dz * dz) / radius;
                float smooth = height * (1 - d * d);
                return MathF.Floor(smooth / step) * step;
            });
            string rock = Ground.Layers[^1];
            for (float x = cx - radius; x <= cx + radius; x++)
                for (float z = cz - radius; z <= cz + radius; z++)
                    if (Map.Inside(x, z) && MathF.Abs(Ground.Height(x + 1, z) - Ground.Height(x - 1, z)) + MathF.Abs(Ground.Height(x, z + 1) - Ground.Height(x, z - 1)) > 2)
                        Ground.Paint(x, z, rock);
            """),
        new("Mountain range across the map", """
            // Peaks in a line from the south-west to the north-east, each one different; the same range each run.
            Rnd.Seed = 7;
            int peaks = 4;
            for (int i = 0; i < peaks; i++)
            {
                float t = (i + 0.5f) / peaks;
                float x = Map.MinX + (Map.MaxX - Map.MinX) * (0.2f + 0.6f * t);
                float z = Map.MinZ + (Map.MaxZ - Map.MinZ) * (0.2f + 0.6f * t) + Rnd.Range(-20, 20);
                Ground.Mountain(x, z, "Lone peak", height: Rnd.Range(35, 60), radius: Rnd.Range(60, 80), seed: Rnd.Int(1, 99999));
            }
            """),
        new("Canyon with a river", """
            // A winding canyon 18 m wide from west to east, dug 3 m below the sea so water fills it.
            float width = 18, floor = Map.Sea - 3;
            for (float x = Map.MinX; x <= Map.MaxX; x++)
            {
                float middle = Map.CenterZ + Noise.At(x, 0, scale: 120, seed: 3) * 60;
                for (float dz = -width * 2; dz <= width * 2; dz++)
                {
                    float z = middle + dz;
                    if (!Map.Inside(x, z)) continue;
                    float edge = Math.Clamp((MathF.Abs(dz) - width / 2) / width, 0, 1);   // 0 on the floor, 1 at the rim
                    float h = Ground.Height(x, z);
                    Ground.Set(x, z, MathF.Min(h, floor + (h - floor) * edge * edge));
                }
            }
            """),
        new("Scatter on gentle slopes", """
            // The first kind of the catalog scattered where the ground is gentle and above the sea, in clumps.
            if (Objects.Kinds.Count == 0) { Print("This map's catalog has no kinds."); return; }
            string kind = Objects.Kinds[0];
            Rnd.Seed = 11;
            int placed = 0;
            foreach (var (x, z) in Map.Points(step: 6))
            {
                float h = Ground.Height(x, z);
                float slope = MathF.Abs(Ground.Height(x + 2, z) - Ground.Height(x - 2, z)) + MathF.Abs(Ground.Height(x, z + 2) - Ground.Height(x, z - 2));
                if (h > Map.Sea + 1 && slope < 1.5f && Noise.At(x, z, scale: 80) > 0.1f && Rnd.Chance(0.5f))
                {
                    Objects.Place(kind, x + Rnd.Range(-2, 2), z + Rnd.Range(-2, 2), yaw: Rnd.Range(0, 360), scale: Rnd.Range(0.8f, 1.2f));
                    placed++;
                }
            }
            Print($"Placed {placed} {kind}.");
            """),
        new("Paint by height and slope", """
            // The first layer everywhere, the second near the sea, the last on steep ground.
            var layers = Ground.Layers;
            foreach (var (x, z) in Map.Points())
            {
                float h = Ground.Height(x, z);
                float slope = MathF.Abs(Ground.Height(x + 1, z) - Ground.Height(x - 1, z)) + MathF.Abs(Ground.Height(x, z + 1) - Ground.Height(x, z - 1));
                Ground.Paint(x, z, slope > 1.6f ? layers[^1] : layers.Count > 1 && h < Map.Sea + 2 ? layers[1] : layers[0]);
            }
            """),
        new("Turn and size the trees", """
            // Every tree turned its own way and sized between 0.8 and 1.3, the same each run.
            Rnd.Seed = 5;
            int n = 0;
            foreach (var o in Objects.OfGroup("Trees"))
            {
                Objects.Move(o, yaw: Rnd.Range(0, 360), scale: Rnd.Range(0.8f, 1.3f));
                n++;
            }
            Print($"{n} trees turned and sized.");
            """),
    ];
}
