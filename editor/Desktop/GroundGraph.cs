using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// A Ground graph: nodes that make, change and combine heightfields (rows of cols heights, a metre apart, the first row
/// the north edge, as a map's heightmap), joined by wires from a node's output to another's input; its Output node's
/// field is the ground it makes. Kept as JSON (a list of nodes with their numbers and place on the canvas, and the wires).
/// </summary>
public sealed class GroundGraph
{
    /// <summary>A node: its kind (GroundNodes.Kinds), its numbers and text, and where it sits on the canvas.</summary>
    public sealed class Node
    {
        /// <summary>Its number in the graph (wires name it).</summary>
        public int Id { get; set; }
        /// <summary>Its kind's id.</summary>
        public string Kind { get; set; } = "";
        /// <summary>Its place on the canvas.</summary>
        public double X { get; set; }
        /// <summary>Its place on the canvas.</summary>
        public double Y { get; set; }
        /// <summary>Its numbers, by the kind's parameter ids (missing: the parameter's default).</summary>
        public Dictionary<string, float> Values { get; } = [];
        /// <summary>Its text (a formula, a file), for the kinds that take one.</summary>
        public string Text { get; set; } = "";
    }

    /// <summary>A wire: from a node's output to another node's input (its index in the kind's inputs).</summary>
    public sealed record Wire(int From, int To, int Port);

    /// <summary>The nodes.</summary>
    public List<Node> Nodes { get; } = [];
    /// <summary>The wires.</summary>
    public List<Wire> Wires { get; } = [];

    /// <summary>A node of a kind added at a place, its numbers at their defaults.</summary>
    public Node Add(string kind, double x, double y)
    {
        var n = new Node { Id = Nodes.Count == 0 ? 1 : Nodes.Max(k => k.Id) + 1, Kind = kind, X = x, Y = y };
        Nodes.Add(n);
        return n;
    }

    /// <summary>
    /// Joins a node's output to another's input, replacing what that input had; nothing when it would make a loop (the
    /// output's node fed, however far back, by the input's).
    /// </summary>
    /// <returns>Whether the wire was made.</returns>
    public bool Join(int from, int to, int port)
    {
        if (from == to || Feeds(to, from))
        {
            return false;
        }
        Wires.RemoveAll(w => w.To == to && w.Port == port);
        Wires.Add(new Wire(from, to, port));
        return true;
    }

    /// <summary>Whether a node's output reaches another node, through any number of wires.</summary>
    public bool Feeds(int from, int to)
    {
        var seen = new HashSet<int>();
        var next = new Stack<int>([from]);
        while (next.TryPop(out int n))
        {
            if (n == to)
            {
                return true;
            }
            if (seen.Add(n))
            {
                foreach (var w in Wires.Where(w => w.From == n))
                {
                    next.Push(w.To);
                }
            }
        }
        return false;
    }

    /// <summary>Takes a node out, and its wires.</summary>
    public void Remove(int id)
    {
        Nodes.RemoveAll(n => n.Id == id);
        Wires.RemoveAll(w => w.From == id || w.To == id);
    }

    /// <summary>The node feeding an input, or null.</summary>
    public Node? Into(int node, int port) => Wires.FirstOrDefault(w => w.To == node && w.Port == port) is { } w ? Nodes.FirstOrDefault(n => n.Id == w.From) : null;

    /// <summary>The graph as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["nodes"] = new JsonArray([.. Nodes.Select(n => (JsonNode)new JsonObject
        {
            ["id"] = n.Id,
            ["kind"] = n.Kind,
            ["x"] = Math.Round(n.X, 1),
            ["y"] = Math.Round(n.Y, 1),
            ["values"] = new JsonObject(n.Values.OrderBy(v => v.Key).Select(v => KeyValuePair.Create(v.Key, (JsonNode?)v.Value))),
            ["text"] = n.Text.Length > 0 ? n.Text : null,
        })]),
        ["wires"] = new JsonArray([.. Wires.Select(w => (JsonNode)new JsonArray(w.From, w.To, w.Port))]),
    };

    /// <summary>A graph from JSON (what it cannot read is left out: an unknown kind's node, a wire to no node).</summary>
    public static GroundGraph FromJson(JsonNode? j)
    {
        var g = new GroundGraph();
        foreach (var n in j?["nodes"] as JsonArray ?? [])
        {
            if (n?["kind"]?.GetValue<string>() is not { } kind || GroundNodes.Of(kind) == null || n["id"]?.GetValue<int>() is not int id || g.Nodes.Any(o => o.Id == id))
            {
                continue;
            }
            var node = new Node { Id = id, Kind = kind, X = n["x"]?.GetValue<double>() ?? 0, Y = n["y"]?.GetValue<double>() ?? 0, Text = n["text"]?.GetValue<string>() ?? "" };
            foreach (var (k, v) in n["values"] as JsonObject ?? [])
            {
                if (v?.GetValue<float>() is float f && float.IsFinite(f))
                {
                    node.Values[k] = f;
                }
            }
            g.Nodes.Add(node);
        }
        foreach (var w in j?["wires"] as JsonArray ?? [])
        {
            if (w is JsonArray { Count: 3 } a && g.Nodes.Any(n => n.Id == a[0]!.GetValue<int>()) && g.Nodes.Any(n => n.Id == a[1]!.GetValue<int>()))
            {
                _ = g.Join(a[0]!.GetValue<int>(), a[1]!.GetValue<int>(), a[2]!.GetValue<int>());
            }
        }
        return g;
    }

    /// <summary>A graph to start from: hills, worn by valleys and erosion, into the output.</summary>
    public static GroundGraph Starter()
    {
        var g = new GroundGraph();
        var hills = g.Add("hills", 40, 60);
        var valleys = g.Add("valleys", 300, 60);
        var erosion = g.Add("erosion", 560, 60);
        var output = g.Add("output", 820, 60);
        _ = g.Join(hills.Id, valleys.Id, 0);
        _ = g.Join(valleys.Id, erosion.Id, 0);
        _ = g.Join(erosion.Id, output.Id, 0);
        return g;
    }
}
