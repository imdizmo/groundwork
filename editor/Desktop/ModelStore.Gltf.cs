using System.Numerics;
using Groundwork.App;
using SharpGLTF.Schema2;
using SkiaSharp;

namespace Groundwork.Desktop;

public sealed partial class ModelStore
{
    /// <summary>
    /// glTF models (.glb, or .gltf with its files beside it) anywhere under the models folder, one asset each, named by
    /// its file name. A model is its default scene: each mesh primitive a part, placed by its node's world matrix; the
    /// base colour (factor, texture, alpha mode and cutoff, both sides) its material. Ids are the asset's name, '#', then
    /// what of it ("#mesh2", "#mat0", "#img1").
    /// </summary>
    private sealed class Gltf : ISource
    {
        /// <summary>How many files are kept parsed at once (the models of a map are read a few at a time).</summary>
        private const int Kept = 8;

        /// <summary>Each asset's file, the first found for a name.</summary>
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        /// <summary>The files parsed last, the newest first.</summary>
        // ponytail: a short list, searched in order; a dictionary when far more than Kept are worth keeping
        private readonly List<(string Name, ModelRoot Root)> _parsed = [];

        /// <summary>The glTF files under a folder.</summary>
        public Gltf(string folder)
        {
            foreach (string f in Files(folder).Order(StringComparer.Ordinal))
            {
                _files.TryAdd(Path.GetFileNameWithoutExtension(f), f);
            }
        }

        /// <summary>The assets' names.</summary>
        public IEnumerable<string> Names => _files.Keys;

        /// <summary>Every .glb and .gltf under a folder (none when it cannot be read).</summary>
        public static List<string> Files(string folder)
        {
            try
            {
                return Directory.Exists(folder)
                    ? [.. Directory.EnumerateFiles(folder, "*.gl*", SearchOption.AllDirectories).Where(f => f.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))]
                    : [];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(e, "listing the glTF models");
                return [];
            }
        }

        /// <inheritdoc/>
        public string? ObjectName(int prefab) => null;

        /// <inheritdoc/>
        public Model? Model(string name)
        {
            if (Root(name) is not { } root || root.DefaultScene is not { } scene)
            {
                return null;
            }
            var parts = new List<Part>();
            foreach (var node in scene.VisualChildren.SelectMany(n => Below(n).Prepend(n)))
            {
                if (node.Mesh is { } mesh)
                {
                    for (int p = 0; p < mesh.Primitives.Count; p++)
                    {
                        parts.Add(new Part($"{name}#mesh{mesh.LogicalIndex}", p, $"{name}#mat{mesh.Primitives[p].Material?.LogicalIndex ?? -1}", node.WorldMatrix));
                    }
                }
            }
            return new Model(name, parts, Vector3.One);
        }

        /// <summary>Every node under a node, at any depth.</summary>
        private static IEnumerable<Node> Below(Node n) => n.VisualChildren.SelectMany(c => Below(c).Prepend(c));

        /// <inheritdoc/>
        public MeshData? Mesh(string id)
        {
            if (Split(id, "mesh") is not var (name, index) || Root(name) is not { } root || index >= root.LogicalMeshes.Count)
            {
                return null;
            }
            var verts = new List<float>();
            var subs = new List<uint[]>();
            List<float>? colors = null;   // made at the first primitive that has vertex colours (white for those before it)
            foreach (var prim in root.LogicalMeshes[index].Primitives)
            {
                uint first = (uint)(verts.Count / 8);
                var pos = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                if (pos == null)
                {
                    subs.Add([]);
                    continue;
                }
                var normal = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uv = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var color = prim.GetVertexAccessor("COLOR_0")?.AsColorArray();
                if (color != null && colors == null)
                {
                    colors = [.. Enumerable.Repeat(1f, (int)first * 4)];
                }
                for (int v = 0; v < pos.Count; v++)
                {
                    var n = normal != null ? normal[v] : Vector3.UnitY;
                    var t = uv != null ? uv[v] : Vector2.Zero;
                    verts.AddRange([pos[v].X, pos[v].Y, pos[v].Z, n.X, n.Y, n.Z, t.X, t.Y]);
                    if (colors != null)
                    {
                        var c = color != null ? color[v] : Vector4.One;
                        colors.AddRange([c.X, c.Y, c.Z, c.W]);
                    }
                }
                // glTF's front faces are counter-clockwise; the store turns every mesh's winding around (it is Unity's
                // in the exporter's files), so it comes the other way here
                subs.Add([.. prim.GetTriangleIndices().SelectMany(tri => new[] { first + (uint)tri.A, first + (uint)tri.C, first + (uint)tri.B })]);
            }
            return new MeshData([.. verts], [.. subs]) { Colors = colors?.ToArray() };
        }

        /// <inheritdoc/>
        public MaterialData Material(string id)
        {
            if (Split(id, "mat") is not var (name, index) || Root(name) is not { } root || index >= root.LogicalMaterials.Count)
            {
                return new MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
            }
            var m = root.LogicalMaterials[index];
            var channel = m.FindChannel("BaseColor");
            var image = channel?.Texture?.PrimaryImage;
            var tt = channel?.TextureTransform;
            return new MaterialData(
                channel?.Color ?? Vector4.One,   // glTF's factors are linear already
                image == null ? null : $"{name}#img{image.LogicalIndex}",
                m.Alpha == AlphaMode.MASK ? m.AlphaCutoff : 0,
                m.DoubleSided,
                tt == null ? new Vector4(1, 1, 0, 0) : new Vector4(tt.Scale.X, tt.Scale.Y, tt.Offset.X, tt.Offset.Y),
                Transparent: m.Alpha == AlphaMode.BLEND);
        }

        /// <inheritdoc/>
        public ImageData? Texture(string id)
        {
            if (Split(id, "img") is not var (name, index) || Root(name) is not { } root || index >= root.LogicalImages.Count)
            {
                return null;
            }
            try
            {
                using var data = SKData.CreateCopy(root.LogicalImages[index].Content.Content.Span);
                using var codec = SKCodec.Create(data);
                if (codec == null)
                {
                    return null;
                }
                var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                byte[] rgba = new byte[info.BytesSize];
                unsafe
                {
                    fixed (byte* p = rgba)
                    {
                        if (codec.GetPixels(info, (IntPtr)p) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                        {
                            return null;
                        }
                    }
                }
                return Smaller(new ImageData(info.Width, info.Height, rgba));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.CaughtBrief(e, $"decoding the texture {id}");
                return null;
            }
        }

        /// <summary>
        /// A picture at most MaxTexture on its longer side (halved while larger): the view needs no more, and a map of
        /// 2K and 4K pictures otherwise holds a quarter to a sixteenth more of them in memory and on the card.
        /// </summary>
        private static ImageData Smaller(ImageData img)
        {
            // ponytail: a 2×2 box filter in managed memory (no native buffers held by the decoder's allocator)
            while (Math.Max(img.Width, img.Height) > MaxTexture)
            {
                int w = Math.Max(1, img.Width / 2), h = Math.Max(1, img.Height / 2);
                byte[] src = img.Rgba, dst = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int y0 = Math.Min(y * 2, img.Height - 1), y1 = Math.Min((y * 2) + 1, img.Height - 1);
                    for (int x = 0; x < w; x++)
                    {
                        int x0 = Math.Min(x * 2, img.Width - 1), x1 = Math.Min((x * 2) + 1, img.Width - 1);
                        int a = ((y0 * img.Width) + x0) * 4, b = ((y0 * img.Width) + x1) * 4, c = ((y1 * img.Width) + x0) * 4, d = ((y1 * img.Width) + x1) * 4, o = ((y * w) + x) * 4;
                        for (int k = 0; k < 4; k++)
                        {
                            dst[o + k] = (byte)((src[a + k] + src[b + k] + src[c + k] + src[d + k] + 2) >> 2);
                        }
                    }
                }
                img = new ImageData(w, h, dst);
            }
            return img;
        }

        /// <summary>The largest side a model's picture is kept at.</summary>
        internal const int MaxTexture = 1024;

        /// <summary>An id's asset and index ("Tree#mesh2": Tree, 2), or null when it is not of that kind.</summary>
        private static (string Name, int Index)? Split(string id, string kind)
        {
            int at = id.LastIndexOf('#');
            return at > 0 && id.AsSpan(at + 1).StartsWith(kind, StringComparison.Ordinal) && int.TryParse(id.AsSpan(at + 1 + kind.Length), out int i) && i >= 0
                ? (id[..at], i)
                : null;
        }

        /// <summary>An asset's file, parsed (kept among the last few), or null when there is none or it cannot be read.</summary>
        private ModelRoot? Root(string name)
        {
            if (!_files.TryGetValue(name, out string? file))
            {
                return null;
            }
            lock (_parsed)
            {
                int at = _parsed.FindIndex(p => p.Name == name);
                if (at >= 0)
                {
                    var hit = _parsed[at];
                    _parsed.RemoveAt(at);
                    _parsed.Insert(0, hit);
                    return hit.Root;
                }
            }
            ModelRoot root;
            try
            {
                root = ModelRoot.Load(file, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.CaughtBrief(e, $"reading the glTF model {name}");
                return null;
            }
            lock (_parsed)
            {
                _parsed.Insert(0, (name, root));
                if (_parsed.Count > Kept)
                {
                    _parsed.RemoveAt(Kept);
                }
            }
            return root;
        }
    }
}
