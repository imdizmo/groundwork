using System.Numerics;

namespace Groundwork.Desktop;

/// <summary>
/// Beds: the ground raised to meet the underside of an object of a bedded kind (the catalog's "bed": a great root, a
/// fallen trunk), with a skirt sloping away round it, so that it lies sunk in the ground wherever it is put. The bed is
/// laid in the step that places the object and taken away in the one that moves or removes it (the ground kept under
/// it, <see cref="Lifted"/>, comes back), so undo and redo carry it; it is saved with the map (beds.bin).
/// </summary>
public sealed partial class EditSession
{
    /// <summary>How steep a bed's skirt is: metres down for each metre out (about 31°).</summary>
    internal const float Slope = 0.6f;

    /// <summary>
    /// A model's meshes for beds, by prefab: each part's vertices (interleaved position, normal, uv: 8 floats a
    /// vertex, in the view's right-handed space), its triangles (three indices each) and its matrix in the model, and
    /// the model's own scale; null when there is no such model. Null: no models at hand (nothing bedded).
    /// </summary>
    public Func<int, (IReadOnlyList<(float[] Vertices, uint[] Triangles, Matrix4x4 Matrix)> Parts, Vector3 RootScale)?>? MeshesOf { get; set; }

    /// <summary>Whether a prefab is of a bedded kind (the map catalog's kinds with bed, unless set otherwise).</summary>
    public Func<int, bool> Bedded { get; set; } = CatalogBedded;

    /// <summary>The ground kept under the beds: each lifted point's height before the bed, by grid index.</summary>
    private readonly Dictionary<int, float> _lifted = [];

    /// <summary>The ground kept under the beds (grid index, height), to save with the map.</summary>
    public IReadOnlyDictionary<int, float> Lifted { get { lock (_lock) { return new Dictionary<int, float>(_lifted); } } }

    /// <summary>The ground kept under the beds, as the map's file has it.</summary>
    internal void SetLifted(IEnumerable<(int Point, float Height)> lifted)
    {
        lock (_lock)
        {
            _lifted.Clear();
            foreach (var (p, h) in lifted)
            {
                if (p >= 0 && p < Ground.W * Ground.H)
                {
                    _lifted[p] = h;
                }
            }
        }
    }

    // ponytail: the catalog read again when its file changes; the bedded kinds kept otherwise
    private static (string File, DateTime Written, HashSet<int> Prefabs) _bedded = ("", DateTime.MinValue, []);

    private static bool CatalogBedded(int prefab)
    {
        string file = MapTools.CatalogFile;
        var written = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
        if (_bedded.File != file || _bedded.Written != written)
        {
            _bedded = (file, written, [.. MapTools.Catalog().Kinds.Where(k => k.Bed).Select(k => Groundwork.App.AssetIds.Of(k.Model))]);
        }
        return _bedded.Prefabs.Contains(prefab);
    }

    /// <summary>
    /// Lays the beds of every bedded object that has none yet (a map just opened, its objects read from its files): the
    /// ground changed with no step of the history and nothing marked unsaved.
    /// </summary>
    public void BedAll()
    {
        if (MeshesOf == null)
        {
            return;
        }
        lock (_lock)
        {
            var touched = new List<int>();
            (int, int, int, int)? rect = null;
            List<WorldScene.Thing> things;
            lock (Scene.Things)
            {
                things = [.. Scene.Things.Where(t => !t.Gone && Bedded(t.Prefab))];
            }
            foreach (var t in things)
            {
                if (Footprint(t) is { } box && !Cells(box).Any(_lifted.ContainsKey))
                {
                    Bed(Ground, t.Prefab, t.Position, t.Rotation, t.Scale, touched, ref rect, []);
                }
            }
            if (rect is { } r)
            {
                Touch(r);
            }
        }
    }

    /// <summary>
    /// A step's ground change with the beds of its things: those taken away lose their beds (the ground kept under them
    /// comes back, the beds of others lying over it laid again), those brought get theirs; nothing added when none of
    /// them is bedded.
    /// </summary>
    private Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? WithBeds(
        Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground, IReadOnlyCollection<int> remove,
        IReadOnlyList<(NewObject Object, bool Piece)> add, List<(int Point, float Before, float After)> lifted)
    {
        if (MeshesOf == null)
        {
            return ground;
        }
        List<WorldScene.Thing> gone;
        var going = remove.ToHashSet();
        lock (Scene.Things)
        {
            gone = [.. remove.Distinct().Where(i => i >= 0 && i < Scene.Things.Count && !Scene.Things[i].Gone).Select(i => Scene.Things[i]).Where(t => Bedded(t.Prefab))];
        }
        var laid = add.Select(a => a.Object).Where(o => Bedded(o.Prefab)).ToList();
        if (gone.Count == 0 && laid.Count == 0)
        {
            return ground;
        }
        return g =>
        {
            var touched = new List<int>();
            (int, int, int, int)? rect = null;
            if (ground?.Invoke(g) is { } own)
            {
                touched.AddRange(own.Touched);
                rect = Join(rect, own.Rect);
            }
            foreach (var t in gone)
            {
                Unbed(g, t, going, touched, ref rect, lifted);
            }
            foreach (var o in laid)
            {
                Bed(g, o.Prefab, o.Position, o.Rotation, o.Scale, touched, ref rect, lifted);
            }
            return (touched, rect ?? (0, 0, 0, 0));
        };
    }

    /// <summary>A step's lifted points, each once: the height kept before it and after it (none where nothing changed).</summary>
    private static (int Point, float Before, float After)[] LiftedOf(List<(int Point, float Before, float After)> lifted)
    {
        var first = new Dictionary<int, float>();
        var last = new Dictionary<int, float>();
        foreach (var (p, before, after) in lifted)
        {
            _ = first.TryAdd(p, before);
            last[p] = after;
        }
        return [.. first.Where(kv => !(float.IsNaN(kv.Value) && float.IsNaN(last[kv.Key])) && kv.Value != last[kv.Key]).Select(kv => (kv.Key, kv.Value, last[kv.Key]))];
    }

    private static (int, int, int, int) Join((int X0, int Z0, int X1, int Z1)? a, (int X0, int Z0, int X1, int Z1) b) =>
        a is { } r ? (Math.Min(r.X0, b.X0), Math.Min(r.Z0, b.Z0), Math.Max(r.X1, b.X1), Math.Max(r.Z1, b.Z1)) : b;

    private IEnumerable<int> Cells((int X0, int Z0, int X1, int Z1) box)
    {
        for (int gz = Math.Max(0, box.Z0); gz <= Math.Min(Ground.H - 1, box.Z1); gz++)
        {
            for (int gx = Math.Max(0, box.X0); gx <= Math.Min(Ground.W - 1, box.X1); gx++)
            {
                yield return (gz * Ground.W) + gx;
            }
        }
    }

    /// <summary>
    /// Lays an object's bed: under its model, the ground raised to the model's underside where that stands above it (a
    /// root sunk to its widest); round it, a skirt falling <see cref="Slope"/> (each point at least its neighbours'
    /// less the slope, swept twice). The ground under every point raised is kept, once.
    /// </summary>
    private void Bed(Ground g, int prefab, Vector3 position, Vector3 rotation, float scale, List<int> touched, ref (int, int, int, int)? rect, List<(int Point, float Before, float After)> lifted)
    {
        if (Underside(prefab, position, rotation, scale) is not { } u)
        {
            return;
        }
        float most = 0;
        for (int k = 0; k < u.Under.Length; k++)
        {
            if (!float.IsNaN(u.Under[k]))
            {
                int gx = u.X0 + (k % u.W), gz = u.Z0 + (k / u.W);
                most = MathF.Max(most, u.Under[k] - g.HeightOf((gz * g.W) + gx));
            }
        }
        if (most <= 0)
        {
            return;
        }
        int skirt = (int)MathF.Ceiling(most / Slope) + 1;
        int x0 = Math.Max(0, u.X0 - skirt), z0 = Math.Max(0, u.Z0 - skirt), x1 = Math.Min(g.W - 1, u.X0 + u.W - 1 + skirt), z1 = Math.Min(g.H - 1, u.Z0 + u.H - 1 + skirt);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float[] target = new float[w * h];
        Array.Fill(target, float.NegativeInfinity);
        for (int k = 0; k < u.Under.Length; k++)
        {
            int gx = u.X0 + (k % u.W), gz = u.Z0 + (k / u.W);
            if (!float.IsNaN(u.Under[k]) && gx >= x0 && gx <= x1 && gz >= z0 && gz <= z1)
            {
                target[((gz - z0) * w) + (gx - x0)] = u.Under[k];
            }
        }
        // the skirt: a chamfer in two sweeps (forward and back), diagonals √2 as far
        const float diag = Slope * 1.41421f;
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                ref float t = ref target[(z * w) + x];
                if (x > 0) { t = MathF.Max(t, target[(z * w) + x - 1] - Slope); }
                if (z > 0) { t = MathF.Max(t, target[((z - 1) * w) + x] - Slope); }
                if (x > 0 && z > 0) { t = MathF.Max(t, target[((z - 1) * w) + x - 1] - diag); }
                if (x < w - 1 && z > 0) { t = MathF.Max(t, target[((z - 1) * w) + x + 1] - diag); }
            }
        }
        for (int z = h - 1; z >= 0; z--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                ref float t = ref target[(z * w) + x];
                if (x < w - 1) { t = MathF.Max(t, target[(z * w) + x + 1] - Slope); }
                if (z < h - 1) { t = MathF.Max(t, target[((z + 1) * w) + x] - Slope); }
                if (x < w - 1 && z < h - 1) { t = MathF.Max(t, target[((z + 1) * w) + x + 1] - diag); }
                if (x > 0 && z < h - 1) { t = MathF.Max(t, target[((z + 1) * w) + x - 1] - diag); }
            }
        }
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x0 + x, gz = z0 + z, p = (gz * g.W) + gx;
                float t = target[(z * w) + x], now = g.HeightOf(p);
                if (t <= now || g.Locked(gx, gz))
                {
                    continue;
                }
                if (!_lifted.ContainsKey(p))
                {
                    _lifted[p] = now;
                    lifted.Add((p, float.NaN, now));
                }
                g.SetHeight(p, t);
                touched.Add(p);
            }
        }
        rect = Join(rect, (x0 - 1, z0 - 1, x1 + 1, z1 + 1));
    }

    /// <summary>
    /// Takes an object's bed away: the ground kept under its footprint put back, then the beds of the other bedded
    /// objects reaching into it laid again (one flattened with it where they overlapped).
    /// </summary>
    private void Unbed(Ground g, WorldScene.Thing t, HashSet<int> going, List<int> touched, ref (int, int, int, int)? rect, List<(int Point, float Before, float After)> lifted)
    {
        if (Footprint(t) is not { } box)
        {
            return;
        }
        foreach (int p in Cells(box))
        {
            if (_lifted.Remove(p, out float kept))
            {
                lifted.Add((p, kept, float.NaN));
                g.SetHeight(p, kept);
                touched.Add(p);
            }
        }
        rect = Join(rect, (box.X0 - 1, box.Z0 - 1, box.X1 + 1, box.Z1 + 1));
        List<WorldScene.Thing> others;
        lock (Scene.Things)
        {
            others = [.. Scene.Things.Select((o, i) => (o, i)).Where(x => !x.o.Gone && !going.Contains(x.i) && x.o.Id != t.Id && Bedded(x.o.Prefab)).Select(x => x.o)];
        }
        foreach (var o in others)
        {
            if (Footprint(o) is { } ob && ob.X0 <= box.X1 && ob.X1 >= box.X0 && ob.Z0 <= box.Z1 && ob.Z1 >= box.Z0)
            {
                Bed(g, o.Prefab, o.Position, o.Rotation, o.Scale, touched, ref rect, lifted);
            }
        }
    }

    /// <summary>A placed model's vertices in grid coordinates (x, z: grid; y: height), part by part with its triangles.</summary>
    private List<(Vector3[] Points, uint[] Triangles)>? Placed(int prefab, Vector3 position, Vector3 rotation, float scale)
    {
        if (MeshesOf?.Invoke(prefab) is not { } model)
        {
            return null;
        }
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(rotation.Y * D, rotation.X * D, rotation.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var at = new Vector3(position.X - Scene.Cx, position.Y, -(position.Z - Scene.Cz));
        var placement = Matrix4x4.CreateScale(scale > 0 ? new Vector3(scale) : model.RootScale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(at);
        var parts = new List<(Vector3[], uint[])>();
        foreach (var (vertices, triangles, matrix) in model.Parts)
        {
            var m = matrix * placement;
            var pts = new Vector3[vertices.Length / 8];
            for (int i = 0; i < pts.Length; i++)
            {
                var v = Vector3.Transform(new Vector3(vertices[i * 8], vertices[(i * 8) + 1], vertices[(i * 8) + 2]), m);
                // view to world (north is -z in the view), world to the grid
                pts[i] = new Vector3(v.X + Scene.Cx - Ground.OriginX, v.Y, Scene.Cz - v.Z - Ground.OriginZ);
            }
            parts.Add((pts, triangles));
        }
        return parts;
    }

    /// <summary>The grid box a bedded object's bed may reach (its model's box, and its skirt as far as its height needs).</summary>
    private (int X0, int Z0, int X1, int Z1)? Footprint(WorldScene.Thing t)
    {
        if (Placed(t.Prefab, t.Position, t.Rotation, t.Scale) is not { Count: > 0 } parts)
        {
            return null;
        }
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var (pts, _) in parts)
        {
            foreach (var p in pts)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        float lowest = float.MaxValue;
        for (int gz = Math.Max(0, (int)min.Z); gz <= Math.Min(Ground.H - 1, (int)MathF.Ceiling(max.Z)); gz++)
        {
            for (int gx = Math.Max(0, (int)min.X); gx <= Math.Min(Ground.W - 1, (int)MathF.Ceiling(max.X)); gx++)
            {
                lowest = MathF.Min(lowest, _lifted.GetValueOrDefault((gz * Ground.W) + gx, Ground.HeightOf((gz * Ground.W) + gx)));
            }
        }
        int skirt = (int)MathF.Ceiling(MathF.Max(0, max.Y - lowest) / Slope) + 1;
        return ((int)MathF.Floor(min.X) - skirt, (int)MathF.Floor(min.Z) - skirt, (int)MathF.Ceiling(max.X) + skirt, (int)MathF.Ceiling(max.Z) + skirt);
    }

    /// <summary>
    /// A placed model's underside over the grid: for each grid point under it, the lowest height of its surface there
    /// (NaN where there is none), over the box of grid points the model covers.
    /// </summary>
    private (float[] Under, int X0, int Z0, int W, int H)? Underside(int prefab, Vector3 position, Vector3 rotation, float scale)
    {
        if (Placed(prefab, position, rotation, scale) is not { Count: > 0 } parts)
        {
            return null;
        }
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var (pts, _) in parts)
        {
            foreach (var p in pts)
            {
                (minX, minZ, maxX, maxZ) = (MathF.Min(minX, p.X), MathF.Min(minZ, p.Z), MathF.Max(maxX, p.X), MathF.Max(maxZ, p.Z));
            }
        }
        int x0 = Math.Max(0, (int)MathF.Floor(minX)), z0 = Math.Max(0, (int)MathF.Floor(minZ)), x1 = Math.Min(Ground.W - 1, (int)MathF.Ceiling(maxX)), z1 = Math.Min(Ground.H - 1, (int)MathF.Ceiling(maxZ));
        if (x1 < x0 || z1 < z0)
        {
            return null;
        }
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float[] under = new float[w * h];
        Array.Fill(under, float.NaN);
        foreach (var (pts, tris) in parts)
        {
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = pts[tris[i]], b = pts[tris[i + 1]], c = pts[tris[i + 2]];
                int cx0 = Math.Max(x0, (int)MathF.Ceiling(MathF.Min(a.X, MathF.Min(b.X, c.X)))), cx1 = Math.Min(x1, (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X))));
                int cz0 = Math.Max(z0, (int)MathF.Ceiling(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)))), cz1 = Math.Min(z1, (int)MathF.Floor(MathF.Max(a.Z, MathF.Max(b.Z, c.Z))));
                float det = ((b.X - a.X) * (c.Z - a.Z)) - ((c.X - a.X) * (b.Z - a.Z));
                if (cx1 < cx0 || cz1 < cz0 || MathF.Abs(det) < 1e-6f)
                {
                    continue;
                }
                for (int gz = cz0; gz <= cz1; gz++)
                {
                    for (int gx = cx0; gx <= cx1; gx++)
                    {
                        // barycentric weights of the grid point in the triangle's shadow
                        float wb = (((gx - a.X) * (c.Z - a.Z)) - ((c.X - a.X) * (gz - a.Z))) / det;
                        float wc = (((b.X - a.X) * (gz - a.Z)) - ((gx - a.X) * (b.Z - a.Z))) / det;
                        float wa = 1 - wb - wc;
                        if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f)
                        {
                            continue;
                        }
                        float y = (wa * a.Y) + (wb * b.Y) + (wc * c.Y);
                        ref float u = ref under[((gz - z0) * w) + (gx - x0)];
                        u = float.IsNaN(u) ? y : MathF.Min(u, y);
                    }
                }
            }
        }
        return (under, x0, z0, w, h);
    }
}
