using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Groundwork.Desktop;

// A generated map: made from the zone generator's numbers, which it keeps (map.json's generator: spec, and print, its
// files' fingerprint when they were made) so that it can be made again from new ones until it is worked on by hand.
public sealed partial class MapProject
{
    /// <summary>The numbers it was generated from and its files' fingerprint then; null: not generated (or made before it kept them).</summary>
    public (ZoneGenerator.Spec Spec, string Print)? Generated { get; private set; }

    /// <summary>Generated, and its files as they were made: nothing worked on by hand and saved.</summary>
    public bool Untouched => Generated is { } g && Print() == g.Print;

    /// <summary>
    /// Fills the map from the zone generator (its ground, paint, objects and water; whatever they were), planted with
    /// <paramref name="planting"/>, and keeps the numbers.
    /// </summary>
    public void Generate(ZoneGenerator.Spec spec, ZoneGenerator.Planting planting)
    {
        var map = ReadRaster();
        if (spec.Floaters > 0)
        {
            // its own islands, of its own ground (another map's in the copied catalog are not its)
            planting = planting with { Islands = FloatingIsland.WriteFor(this) };
        }
        var zone = ZoneGenerator.Generate(spec, map.Cols, map.Rows, map.X0, map.Y0, planting);
        Write(HeightsFile, zone.Heights, map.Rows, map.Cols);
        WriteMask(LayersFile, zone.Mask, map);
        System.IO.File.WriteAllText(ObjectsFile, new JsonArray([.. zone.Objects.Select(o => (JsonNode)new JsonArray(o.Model, R(o.At.X), R(o.At.Y), R(o.At.Z), 0, R(o.Yaw), 0, R(o.Scale)))]).ToJsonString());
        System.IO.File.WriteAllText(WaterFile, new JsonObject
        {
            ["rivers"] = new JsonArray([.. zone.Rivers.Select((r, i) => (JsonNode)new JsonObject
            {
                ["name"] = $"River {i + 1}",
                ["top"] = R(r.Top),
                ["end"] = R(r.End),
                ["points"] = new JsonArray([.. r.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.HalfWidth), R(p.Depth), R(p.Current)))]),
            })]),
            ["lakes"] = new JsonArray([.. zone.Lakes.Select((l, i) => (JsonNode)new JsonObject
            {
                ["name"] = $"Lake {i + 1}",
                ["level"] = R(l.Level),
                ["depth"] = R(l.Depth),
                ["outline"] = new JsonArray([.. l.Outline.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y)))]),
            })]),
        }.ToJsonString());
        if (spec.CoastDry)
        {
            // the world's edge falls away into nothing: no sea to fill it, and the ground past the cliff is a hole
            SeaLevel = -1000;
            byte[] holes = new byte[zone.Heights.Length * 4];
            for (int p = 0; p < zone.Heights.Length; p++)
            {
                byte hole = zone.Heights[p] < -300 ? (byte)255 : (byte)0;
                (holes[p * 4], holes[(p * 4) + 1], holes[(p * 4) + 2], holes[(p * 4) + 3]) = (hole, hole, hole, 255);
            }
            WriteMask(HolesFile, holes, map);
        }
        else
        {
            // made again without its drop: the sea back, no holes
            SeaLevel = 0;
            System.IO.File.Delete(HolesFile);
        }
        Generated = (spec, Print());
        WriteJson();
    }

    /// <summary>A fingerprint of the files the map is worked on in (those there are).</summary>
    private string Print()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string f in new[] { HeightsFile, LayersFile, ObjectsFile, WaterFile, HolesFile, AreasFile })
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(f)));
            if (System.IO.File.Exists(f))
            {
                using var s = System.IO.File.OpenRead(f);
                byte[] buffer = new byte[1 << 16];
                for (int n; (n = s.Read(buffer)) > 0;)
                {
                    hash.AppendData(buffer, 0, n);
                }
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static (ZoneGenerator.Spec, string)? ReadGenerated(JsonNode? g)
    {
        try
        {
            return g?["spec"] is JsonNode spec && g["print"]?.GetValue<string>() is { Length: > 0 } print && spec.Deserialize(SpecJson.Default.Spec) is { } s ? (s, print) : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return null;   // numbers it cannot read: a map like any other
        }
    }

    private static JsonObject WriteGenerated((ZoneGenerator.Spec Spec, string Print) g) =>
        new() { ["spec"] = JsonSerializer.SerializeToNode(g.Spec, SpecJson.Default.Spec), ["print"] = g.Print };
}

/// <summary>The generator's numbers as JSON (map.json's generator.spec).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ZoneGenerator.Spec))]
internal sealed partial class SpecJson : JsonSerializerContext;
