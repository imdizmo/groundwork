using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Groundwork.App;

namespace Groundwork.Desktop;

// The map as a script sees it, what it changed, one run of it, and its changes put into the map.
public static partial class ScriptHost
{
    /// <summary>The map as the script sees it: taken on the window's thread before the script runs.</summary>
    /// <param name="W">Ground points across (x).</param>
    /// <param name="H">Ground points deep (z).</param>
    /// <param name="Ox">World x of the first point.</param>
    /// <param name="Oz">World z of the first point.</param>
    /// <param name="Heights">The heights now, by point.</param>
    /// <param name="Original">The heights of the map's heightmap.</param>
    /// <param name="Locked">Points off the map (the padding round it): the script leaves them alone.</param>
    /// <param name="Things">The objects: index into the scene's things, model, position, group, rotation (Euler degrees), scale.</param>
    /// <param name="Kinds">The kinds a script may place: model and label.</param>
    /// <param name="Layers">The map's ground layers' names.</param>
    /// <param name="Bounds">The map's corners (the points not locked).</param>
    /// <param name="Sea">The sea level.</param>
    /// <param name="Name">The map's name.</param>
    public sealed record Snapshot(int W, int H, float Ox, float Oz, float[] Heights, float[] Original, bool[] Locked,
        List<(int Index, string Kind, Vector3 Position, string Group, Vector3 Rotation, float Scale)> Things,
        List<(string Model, string Label)> Kinds, string[] Layers, (float MinX, float MinZ, float MaxX, float MaxZ) Bounds, float Sea, string Name);

    /// <summary>What a script did.</summary>
    public sealed class Changes
    {
        /// <summary>New heights by ground point.</summary>
        public Dictionary<int, float> Heights { get; } = [];
        /// <summary>Paint, in the order painted: ground point, layer (index into the map's), strength 0..1.</summary>
        public List<(int Point, int Layer, float Strength)> Paint { get; } = [];
        /// <summary>Things to take away, indices into the scene's things.</summary>
        public HashSet<int> Remove { get; } = [];
        /// <summary>New objects: model, position (NaN height: on the ground at the end), rotation (Euler degrees), scale.</summary>
        public List<(string Model, Vector3 Position, Vector3 Rotation, float Scale)> Add { get; } = [];
        /// <summary>Things moved, turned or sized: index into the scene's things, its new place, rotation and scale.</summary>
        public Dictionary<int, (Vector3 Position, Vector3 Rotation, float Scale)> Moves { get; } = [];
        /// <summary>What the script printed (2000 lines at most).</summary>
        public StringBuilder Output { get; } = new();
        /// <summary>The script changed nothing.</summary>
        public bool Empty => Heights.Count == 0 && Paint.Count == 0 && Remove.Count == 0 && Add.Count == 0 && Moves.Count == 0;
    }

    /// <summary>Takes the snapshot of the open map, on the window's thread.</summary>
    internal static Snapshot Take(EditSession s, MapProject map)
    {
        var g = s.Ground;
        var raster = map.ReadRaster();
        int n = g.W * g.H;
        float[] heights = new float[n], original = new float[n];
        bool[] locked = new bool[n];
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        for (int p = 0; p < n; p++)
        {
            int gx = p % g.W, gz = p / g.W;
            float x = g.OriginX + gx, z = g.OriginZ + gz;
            var (col, row) = raster.Pixel(x, z);
            heights[p] = g.HeightOf(p);
            original[p] = g.Base[p];
            locked[p] = gx == 0 || gz == 0 || gx == g.W - 1 || gz == g.H - 1 || !raster.Holds(col, row);
            if (!locked[p])
            {
                (minX, minZ, maxX, maxZ) = (MathF.Min(minX, x), MathF.Min(minZ, z), MathF.Max(maxX, x), MathF.Max(maxZ, z));
            }
        }
        var things = new List<(int, string, Vector3, string, Vector3, float)>();
        lock (s.Scene.Things)
        {
            for (int i = 0; i < s.Scene.Things.Count; i++)
            {
                var t = s.Scene.Things[i];
                if (!t.Gone && AssetIds.NameOf(t.Prefab) is { } name)
                {
                    things.Add((i, name, t.Position, ObjectKinds.Of(name, t.Piece).ToString(), t.Rotation, t.Scale > 0 ? t.Scale : 1));
                }
            }
        }
        var (kinds, _, _) = MapTools.Catalog();
        return new Snapshot(g.W, g.H, g.OriginX, g.OriginZ, heights, original, locked, things, [.. kinds.Select(k => (k.Model, k.Label))],
            [.. map.Layers.Select(l => l.Name)], (minX, minZ, maxX, maxZ), map.SeaLevel, map.Name);
    }

    /// <summary>Runs the compiled script against the snapshot on the calling thread; the script's exceptions are thrown on.</summary>
    internal static Changes Execute(byte[] image, byte[]? pdb, Snapshot snap, CancellationToken cancel)
    {
        var ch = new Changes();
        var calls = new Callbacks(snap, ch, cancel);
        var alc = new AssemblyLoadContext("Script", isCollectible: true);
        try
        {
            var asm = pdb != null ? alc.LoadFromStream(new MemoryStream(image), new MemoryStream(pdb)) : alc.LoadFromStream(new MemoryStream(image));
            var host = asm.GetType("__Host") ?? throw new InvalidOperationException("the script's API is missing");
            calls.Bind(host);
            var entry = asm.EntryPoint ?? throw new InvalidOperationException("the script has no statements to run");
            try
            {
                _ = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            calls.PlaceOnGround();
            return ch;
        }
        finally
        {
            alc.Unload();
        }
    }

    /// <summary>
    /// The script's changes into the map as one step in History: heights, paint (each layer's channel towards 1, the
    /// others towards 0, by the strength), objects taken away, moved (keeping their data) and placed.
    /// </summary>
    internal static void Apply(EditSession s, MapProject map, Changes ch, string label)
    {
        var things = s.Scene.Things;
        bool Here(int i) => i >= 0 && i < things.Count && !things[i].Gone;
        var removed = ch.Remove.Where(Here).ToList();
        var moved = ch.Moves.Where(m => !ch.Remove.Contains(m.Key) && Here(m.Key)).ToList();
        var adds = ch.Add.Select(a => (new NewObject(0, AssetIds.Of(a.Model), a.Position, a.Rotation, MathF.Abs(a.Scale - 1) < 1e-3f ? 0 : a.Scale), ObjectKinds.Of(a.Model) == ObjectKind.Buildings)).ToList();
        adds.AddRange(moved.Select(m => (new NewObject(0, things[m.Key].Prefab, m.Value.Position, m.Value.Rotation, m.Value.Scale, s.Edits.FindAdded(things[m.Key].Id)?.Data), things[m.Key].Piece)));
        int[] channels = [.. map.Layers.Select(l => l.Channel)];
        _ = s.Commit(label, ch.Heights.Count == 0 && ch.Paint.Count == 0 ? null : g =>
        {
            var touched = new HashSet<int>();
            foreach (var (p, h) in ch.Heights)
            {
                g.SetHeight(p, h);
                _ = touched.Add(p);
            }
            foreach (var (p, layer, strength) in ch.Paint)
            {
                g.StartPaint(p);
                for (int c = 0; c < g.C; c++)
                {
                    if (c != Ground.HoleChannel)
                    {
                        ref float v = ref g.Paint[(p * g.C) + c];
                        v += ((c == channels[layer] ? 1 : 0) - v) * strength;
                    }
                }
                _ = touched.Add(p);
            }
            var list = touched.ToList();
            return (list, list.Count == 0 ? (0, 0, 0, 0) : (list.Min(p => p % g.W) - 1, list.Min(p => p / g.W) - 1, list.Max(p => p % g.W) + 1, list.Max(p => p / g.W) + 1));
        }, [.. removed, .. moved.Select(m => m.Key)], adds);
    }

    /// <summary>The callbacks one run gives its script: each reads or changes the run's copy of the map and notes what it changed.</summary>
    private sealed class Callbacks(Snapshot snap, Changes ch, CancellationToken cancel)
    {
        private readonly float[] _heights = (float[])snap.Heights.Clone();
        private readonly Dictionary<int, Noise> _noises = [];
        private int _lines;

        /// <summary>Binds the API's names in the script's host class to this run's callbacks and the map's values.</summary>
        public void Bind(Type host)
        {
            void Fill(string name, object? value) => host.GetField(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
            Fill("Height", new Func<float, float, float>((x, z) => Sample(_heights, x, z)));
            Fill("Original", new Func<float, float, float>((x, z) => Sample(snap.Original, x, z)));
            Fill("Set", new Action<float, float, float>(SetHeight));
            Fill("Paint", new Action<float, float, string, float>(PaintAt));
            Fill("Mountain", new Action<float, float, string, float, float, float, float, int>(MountainAt));
            Fill("Objects", new Func<(int, string, float, float, float, string, float, float, float, float)[]>(() =>
                [.. snap.Things.Select(t => (t.Index, t.Kind, t.Position.X, t.Position.Y, t.Position.Z, t.Group, t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Scale))]));
            Fill("Remove", new Action<int>(i =>
            {
                if (!snap.Things.Exists(t => t.Index == i))
                {
                    throw new ArgumentException($"no object {i} on the map (Objects.All gives them)");
                }
                lock (ch)
                {
                    _ = ch.Remove.Add(i);
                }
            }));
            Fill("Place", new Action<string, float, float, float, float, float, float, float>(Place));
            Fill("Move", new Action<int, float, float, float, float, float, float, float>(Move));
            Fill("Kinds", new Func<string[]>(() => [.. snap.Kinds.Select(k => k.Model)]));
            Fill("Layers", new Func<string[]>(() => snap.Layers));
            Fill("Noise", new Func<float, float, int, float>(NoiseAt));
            Fill("Print", new Action<string>(PrintLine));
            Fill("Check", new Action(cancel.ThrowIfCancellationRequested));
            Fill("Offsets", new Func<float, float, float, float, (int, int)>(Offsets));
            Fill("MinX", snap.Bounds.MinX);
            Fill("MinZ", snap.Bounds.MinZ);
            Fill("MaxX", snap.Bounds.MaxX);
            Fill("MaxZ", snap.Bounds.MaxZ);
            Fill("Sea", snap.Sea);
            Fill("Name", snap.Name);
        }

        /// <summary>The ground point under a world point, or -1 off the grid (or not a point at all).</summary>
        private int Point(float x, float z)
        {
            if (!float.IsFinite(x) || !float.IsFinite(z))
            {
                return -1;
            }
            int gx = (int)MathF.Round(x - snap.Ox), gz = (int)MathF.Round(z - snap.Oz);
            return gx < 0 || gz < 0 || gx >= snap.W || gz >= snap.H ? -1 : (gz * snap.W) + gx;
        }

        /// <summary>The height between ground points, as the ground is drawn.</summary>
        private float Sample(float[] a, float x, float z)
        {
            float fx = Math.Clamp(x - snap.Ox, 0, snap.W - 1), fz = Math.Clamp(z - snap.Oz, 0, snap.H - 1);
            int x0 = Math.Min((int)fx, snap.W - 2), z0 = Math.Min((int)fz, snap.H - 2);
            float u = fx - x0, v = fz - z0;
            float h00 = a[(z0 * snap.W) + x0], h10 = a[(z0 * snap.W) + x0 + 1], h01 = a[((z0 + 1) * snap.W) + x0], h11 = a[((z0 + 1) * snap.W) + x0 + 1];
            return (((h00 * (1 - u)) + (h10 * u)) * (1 - v)) + (((h01 * (1 - u)) + (h11 * u)) * v);
        }

        private void SetHeight(float x, float z, float h)
        {
            int p = Point(x, z);
            if (p >= 0 && !snap.Locked[p] && float.IsFinite(h))
            {
                lock (ch)
                {
                    _heights[p] = h;
                    ch.Heights[p] = h;
                }
            }
        }

        private void PaintAt(float x, float z, string layer, float strength)
        {
            int k = Array.FindIndex(snap.Layers, l => string.Equals(l, layer, StringComparison.OrdinalIgnoreCase));
            if (k < 0)
            {
                throw new ArgumentException($"no layer “{layer}” on this map (Ground.Layers: {string.Join(", ", snap.Layers)})");
            }
            int p = Point(x, z);
            if (p >= 0 && !snap.Locked[p] && strength > 0)
            {
                lock (ch)
                {
                    ch.Paint.Add((p, k, MathF.Min(strength, 1)));
                }
            }
        }

        private void MountainAt(float x, float z, string preset, float height, float radius, float rough, float turn, int seed)
        {
            var p = Array.Find(Mountain.Presets, m => string.Equals(m.Name, preset, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"no preset “{preset}” ({string.Join(", ", Mountain.Presets.Select(m => m.Name))})");
            var rolled = Mountain.Roll(p, new Random(seed));
            var spec = rolled with
            {
                Height = height > 0 ? height : rolled.Height,
                Radius = radius > 0 ? radius : rolled.Radius,
                Rough = rough >= 0 ? MathF.Min(rough, 1) : rolled.Rough,
                Turn = turn >= 0 ? turn : rolled.Turn,
            };
            var shape = Mountain.Shape(spec);
            var (fromZ, toZ) = Offsets(z, spec.Reach, snap.Bounds.MinZ, snap.Bounds.MaxZ);
            var (fromX, toX) = Offsets(x, spec.Reach, snap.Bounds.MinX, snap.Bounds.MaxX);
            for (int dz = fromZ; dz <= toZ; dz++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int dx = fromX; dx <= toX; dx++)
                {
                    float m = shape(dx, dz);
                    if (m != 0)
                    {
                        SetHeight(x + dx, z + dz, Sample(_heights, x + dx, z + dz) + m);
                    }
                }
            }
        }

        private void Place(string kind, float x, float z, float yaw, float scale, float y, float pitch, float roll)
        {
            int k = snap.Kinds.FindIndex(k => string.Equals(k.Model, kind, StringComparison.OrdinalIgnoreCase) || string.Equals(k.Label, kind, StringComparison.OrdinalIgnoreCase));
            if (k < 0)
            {
                throw new ArgumentException($"no kind “{kind}” in the catalog (Objects.Kinds lists them)");
            }
            foreach (var (what, v) in new[] { ("x", x), ("z", z), ("yaw", yaw), ("pitch", pitch), ("roll", roll) })
            {
                Number(what, v);
            }
            if (!float.IsNaN(y))
            {
                Number("y", y);
            }
            if (!float.IsFinite(scale) || scale <= 0)
            {
                throw new ArgumentException($"a scale of {scale}: give a size above 0 (1: the model's own)");
            }
            InMap(x, z);
            lock (ch)
            {
                ch.Add.Add((snap.Kinds[k].Model, new Vector3(x, y, z), new Vector3(pitch, yaw, roll), scale));
            }
        }

        private void Move(int index, float x, float z, float y, float yaw, float pitch, float roll, float scale)
        {
            if (!snap.Things.Exists(t => t.Index == index))
            {
                throw new ArgumentException($"no object {index} on the map (Objects.All gives them)");
            }
            foreach (var (what, v) in new[] { ("x", x), ("z", z), ("y", y), ("yaw", yaw), ("pitch", pitch), ("roll", roll) })
            {
                if (!float.IsNaN(v))
                {
                    Number(what, v);
                }
            }
            if (!float.IsNaN(scale) && (!float.IsFinite(scale) || scale <= 0))
            {
                throw new ArgumentException($"a scale of {scale}: give a size above 0 (1: the model's own)");
            }
            lock (ch)
            {
                var t = snap.Things.Find(t => t.Index == index);
                var (p, r, sc) = ch.Moves.TryGetValue(index, out var m) ? m : (t.Position, t.Rotation, t.Scale);
                static float Or(float v, float was) => float.IsNaN(v) ? was : v;
                InMap(Or(x, p.X), Or(z, p.Z));
                ch.Moves[index] = (new Vector3(Or(x, p.X), Or(y, p.Y), Or(z, p.Z)), new Vector3(Or(pitch, r.X), Or(yaw, r.Y), Or(roll, r.Z)), Or(scale, sc));
            }
        }

        private float NoiseAt(float x, float z, int seed)
        {
            lock (_noises)
            {
                if (!_noises.TryGetValue(seed, out var n))
                {
                    n = _noises[seed] = new Noise(seed);
                }
                return n.Fbm(x, z) / 1.6f;
            }
        }

        private void PrintLine(string text)
        {
            lock (ch)
            {
                if (_lines++ < 2000)
                {
                    _ = ch.Output.AppendLine(text);
                }
            }
        }

        /// <summary>Objects put on the ground (their height NaN): the height the ground has as the script leaves it.</summary>
        public void PlaceOnGround()
        {
            for (int i = 0; i < ch.Add.Count; i++)
            {
                var a = ch.Add[i];
                if (float.IsNaN(a.Position.Y))
                {
                    ch.Add[i] = a with { Position = a.Position with { Y = Sample(_heights, a.Position.X, a.Position.Z) } };
                }
            }
        }

        private void InMap(float x, float z)
        {
            var (minX, minZ, maxX, maxZ) = snap.Bounds;
            if (!(x >= minX && x <= maxX && z >= minZ && z <= maxZ))
            {
                throw new ArgumentException($"({x}, {z}) is off the map (x {minX} to {maxX}, z {minZ} to {maxZ}): keep to Map.Inside");
            }
        }

        private static void Number(string what, float v)
        {
            if (!float.IsFinite(v))
            {
                throw new ArgumentException($"{what} is {v}: give a number (not NaN or infinite)");
            }
        }
    }

    /// <summary>
    /// The whole-metre offsets from c, within reach of it, that can land within min..max: a short range of integers
    /// however large reach is. Empty (From above To) when reach is not a size or none lands within.
    /// </summary>
    internal static (int From, int To) Offsets(float c, float reach, float min, float max)
    {
        if (!(reach >= 0) || !float.IsFinite(c))
        {
            return (0, -1);
        }
        double from = Math.Max(Math.Max(-Math.Ceiling(reach), Math.Floor(min - (double)c)), -(1 << 24));
        double to = Math.Min(Math.Min(Math.Floor(reach), Math.Ceiling(max - (double)c)), 1 << 24);
        return from <= to ? ((int)from, (int)to) : (0, -1);
    }
}
