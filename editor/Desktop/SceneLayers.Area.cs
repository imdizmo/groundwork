using System.Numerics;
using Groundwork.App;
using Groundwork.Sdk;

namespace Groundwork.Desktop;

public static partial class SceneLayers
{
    /// <summary>
    /// An open area as scene providers read it (IAreaView): its bounds, its objects with the changes not saved yet, and
    /// its ground as the editor has it (heights, biomes, the game's terrain colours and paint), read on a worker. The
    /// changes not saved yet are copied when it is made (on the window's thread); the objects' data and the ground are
    /// read as they are when asked (a brush stroke going on is seen part way, and drawn again once done).
    /// </summary>
    internal sealed class AreaView : IAreaView
    {
        private readonly WorldScene _s;
        private readonly List<WorldScene.Thing> _things;
        private readonly Dictionary<string, IReadOnlyList<IObjectView>> _objects = [];

        /// <summary>The area of a scene, as it is now.</summary>
        /// <param name="s">The scene.</param>
        /// <param name="without">Objects left out as if deleted, by id (those a step is about to move).</param>
        public AreaView(WorldScene s, IEnumerable<int>? without = null)
        {
            _s = s;
            var left = without?.ToHashSet() ?? [];
            lock (s.Things)
            {
                _things = [.. s.Things.Where(t => !t.Gone && !left.Contains(t.Id))];
            }
        }

        /// <inheritdoc/>
        public float MinX => _s.OriginX;

        /// <inheritdoc/>
        public float MinZ => _s.OriginZ;

        /// <inheritdoc/>
        public float MaxX => _s.OriginX + _s.W - 1;

        /// <inheritdoc/>
        public float MaxZ => _s.OriginZ + _s.H - 1;

        private bool Inside(Vector3 p) => p.X >= MinX && p.X < MaxX && p.Z >= MinZ && p.Z < MaxZ;

        /// <inheritdoc/>
        public IReadOnlyList<IObjectView> ObjectsOf(string asset)
        {
            lock (_objects)
            {
                if (!_objects.TryGetValue(asset, out var found))
                {
                    _objects[asset] = found = Read(AssetIds.Of(asset), asset);
                }
                return found;
            }
        }

        /// <summary>The objects of an asset in the area, with their data.</summary>
        private List<IObjectView> Read(int asset, string name) =>
            [.. _things.Where(t => t.Prefab == asset && Inside(t.Position))
                .Select(t => (IObjectView)(_s.Session?.ViewOf(t) ?? new ObjectView(name, t.Position, t.Rotation, null, t.Scale) { Id = t.Id }))];

        /// <summary>The grid point nearest a world point, held to the area.</summary>
        private (int X, int Z) Nearest(float x, float z) =>
            (Math.Clamp((int)MathF.Round(x - _s.OriginX), 0, _s.W - 1), Math.Clamp((int)MathF.Round(z - _s.OriginZ), 0, _s.H - 1));

        /// <inheritdoc/>
        public float GroundHeight(float x, float z)
        {
            float u = Math.Clamp(x - _s.OriginX, 0, _s.W - 1), v = Math.Clamp(z - _s.OriginZ, 0, _s.H - 1);
            int ix = Math.Min((int)u, _s.W - 2), iz = Math.Min((int)v, _s.H - 2);
            float fx = u - ix, fz = v - iz;
            double H(int a, int b) => _s.Heights[(iz + b) * _s.W + ix + a];
            // Two triangles a cell: (0,0), (1,0), (0,1), then (1,1), (0,1), (1,0), worked out in double and rounded once.
            return fx + fz <= 1
                ? (float)(H(0, 0) + (fx * (H(1, 0) - H(0, 0))) + (fz * (H(0, 1) - H(0, 0))))
                : (float)(H(1, 1) + ((1.0 - fx) * (H(0, 1) - H(1, 1))) + ((1.0 - fz) * (H(1, 0) - H(1, 1))));
        }

        /// <inheritdoc/>
        public int Layer(float x, float z)
        {
            var (gx, gz) = Nearest(x, z);
            return _s.LayerAt(gz * _s.W + gx);
        }

        /// <inheritdoc/>
        public Vector3 GroundColor(int x, int z)
        {
            var (gx, gz) = Nearest(x, z);
            return _s.GroundColor(gz * _s.W + gx);
        }

        /// <inheritdoc/>
        public uint GroundMask(int x, int z) => Rgba(_s.Mask, x, z);

        /// <summary>Four bytes a grid point (RGBA) as one number, red lowest.</summary>
        private uint Rgba(byte[] bytes, int x, int z)
        {
            var (gx, gz) = Nearest(x, z);
            int i = (gz * _s.W + gx) * 4;
            return (uint)(bytes[i] | bytes[i + 1] << 8 | bytes[i + 2] << 16 | bytes[i + 3] << 24);
        }
    }
}
