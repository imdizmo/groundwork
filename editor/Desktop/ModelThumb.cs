using System.Numerics;
using SkiaSharp;

namespace Groundwork.Desktop;

/// <summary>
/// A model's own picture, for kinds with no icon (glTF models, models the catalog doesn't list): drawn on the CPU with
/// a depth buffer, seen from the front right and a little above, lit from the upper left, on clear ground.
/// </summary>
internal static class ModelThumb
{
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, 0.75f, 0.55f));

    /// <summary>The model's picture as a PNG, <paramref name="size"/> pixels square; null when there is no such model.</summary>
    public static byte[]? Png(ModelStore store, string name, int size = 128)
    {
        if (store.LoadModel(name) is not { } model)
        {
            return null;
        }
        // Drawn twice as big, then shrunk: smooth edges.
        int n = size * 2;
        var view = Matrix4x4.CreateRotationY(-0.6f) * Matrix4x4.CreateRotationX(0.35f);
        var parts = new List<(ModelStore.MeshData Mesh, uint[] Tris, ModelStore.MaterialData Mat, ModelStore.ImageData? Tex, Matrix4x4 M)>();
        var textures = new Dictionary<string, ModelStore.ImageData?>();
        Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        foreach (var p in model.Parts)
        {
            if (store.LoadMesh(p.Mesh) is not { } mesh || p.Sub >= mesh.Submeshes.Length)
            {
                continue;
            }
            var mat = store.Material(p.Material);
            var tex = mat.Map is { } map ? textures.TryGetValue(map, out var t) ? t : textures[map] = store.LoadTexture(map) : null;
            var m = p.Matrix * view;
            foreach (uint i in mesh.Submeshes[p.Sub])
            {
                var v = Vector3.Transform(new Vector3(mesh.Vertices[i * 8], mesh.Vertices[(i * 8) + 1], mesh.Vertices[(i * 8) + 2]), m);
                lo = Vector3.Min(lo, v);
                hi = Vector3.Max(hi, v);
            }
            parts.Add((mesh, mesh.Submeshes[p.Sub], mat, tex, m));
        }
        float[] rgba = new float[n * n * 4];
        if (parts.Count > 0)
        {
            float[] depth = new float[n * n];
            Array.Fill(depth, float.MinValue);
            float scale = n * 0.92f / MathF.Max(1e-4f, MathF.Max(hi.X - lo.X, hi.Y - lo.Y));
            var mid = (lo + hi) / 2;
            foreach (var (mesh, tris, mat, tex, m) in parts)
            {
                Draw(mesh, tris, mat, tex, m, p => new Vector3(((p.X - mid.X) * scale) + (n / 2f), (n / 2f) - ((p.Y - mid.Y) * scale), p.Z), n, rgba, depth);
            }
        }
        using var big = new SKBitmap(new SKImageInfo(n, n, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        byte[] bytes = new byte[n * n * 4];
        for (int i = 0; i < n * n; i++)
        {
            for (int c = 0; c < 3; c++)
            {
                bytes[(i * 4) + c] = (byte)(MathF.Pow(Math.Clamp(rgba[(i * 4) + c], 0, 1), 1 / 2.2f) * 255 + 0.5f);
            }
            bytes[(i * 4) + 3] = (byte)(rgba[(i * 4) + 3] * 255);
        }
        System.Runtime.InteropServices.Marshal.Copy(bytes, 0, big.GetPixels(), bytes.Length);
        using var small = big.Resize(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        using var data = small.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>One part's triangles into the picture: nearest wins, cut-out texels dropped, back faces culled unless double sided.</summary>
    private static void Draw(ModelStore.MeshData mesh, uint[] tris, ModelStore.MaterialData mat, ModelStore.ImageData? tex, Matrix4x4 m, Func<Vector3, Vector3> screen, int n, float[] rgba, float[] depth)
    {
        float[] v = mesh.Vertices;
        float cutoff = MathF.Max(mat.Cutoff, 0.02f);
        bool cull = !mat.DoubleSided && !mesh.BothWays;
        var s = new Vector3[3];
        var nor = new Vector3[3];
        var uv = new Vector2[3];
        var col = new Vector4[3];
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                uint i = tris[t + k];
                s[k] = screen(Vector3.Transform(new Vector3(v[i * 8], v[(i * 8) + 1], v[(i * 8) + 2]), m));
                nor[k] = Vector3.TransformNormal(new Vector3(v[(i * 8) + 3], v[(i * 8) + 4], v[(i * 8) + 5]), m);
                uv[k] = new Vector2(v[(i * 8) + 6], v[(i * 8) + 7]) * new Vector2(mat.UvTransform.X, mat.UvTransform.Y) + new Vector2(mat.UvTransform.Z, mat.UvTransform.W);
                col[k] = mesh.Colors is { } c ? new Vector4(c[i * 4], c[(i * 4) + 1], c[(i * 4) + 2], c[(i * 4) + 3]) : Vector4.One;
            }
            // Screen y runs down: a front face (counter-clockwise seen from the camera) has a negative area here.
            float area = ((s[1].X - s[0].X) * (s[2].Y - s[0].Y)) - ((s[2].X - s[0].X) * (s[1].Y - s[0].Y));
            if (MathF.Abs(area) < 1e-6f || (cull && area > 0))
            {
                continue;
            }
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(s[0].X, MathF.Min(s[1].X, s[2].X))));
            int x1 = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(s[0].X, MathF.Max(s[1].X, s[2].X))));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(s[0].Y, MathF.Min(s[1].Y, s[2].Y))));
            int y1 = Math.Min(n - 1, (int)MathF.Ceiling(MathF.Max(s[0].Y, MathF.Max(s[1].Y, s[2].Y))));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = (((s[1].X - px) * (s[2].Y - py)) - ((s[2].X - px) * (s[1].Y - py))) / area;
                    float w1 = (((s[2].X - px) * (s[0].Y - py)) - ((s[0].X - px) * (s[2].Y - py))) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0)
                    {
                        continue;
                    }
                    int p = (y * n) + x;
                    float z = (w0 * s[0].Z) + (w1 * s[1].Z) + (w2 * s[2].Z);
                    if (z <= depth[p])
                    {
                        continue;
                    }
                    var c = mat.Color * ((w0 * col[0]) + (w1 * col[1]) + (w2 * col[2]));
                    if (tex is { Width: > 0, Height: > 0 })
                    {
                        var u = (w0 * uv[0]) + (w1 * uv[1]) + (w2 * uv[2]);
                        int tx = (int)((u.X - MathF.Floor(u.X)) * tex.Width) % tex.Width, ty = (int)((u.Y - MathF.Floor(u.Y)) * tex.Height) % tex.Height;
                        int q = ((ty * tex.Width) + tx) * 4;
                        c *= new Vector4(ModelStore.Linear(tex.Rgba[q] / 255f), ModelStore.Linear(tex.Rgba[q + 1] / 255f), ModelStore.Linear(tex.Rgba[q + 2] / 255f), tex.Rgba[q + 3] / 255f);
                    }
                    if (c.W < cutoff)
                    {
                        continue;
                    }
                    var normal = Vector3.Normalize((w0 * nor[0]) + (w1 * nor[1]) + (w2 * nor[2]));
                    if (normal.Z < 0)
                    {
                        normal = -normal;   // a double-sided face's back
                    }
                    float lit = 0.45f + (0.65f * MathF.Max(0, Vector3.Dot(normal, Light)));
                    depth[p] = z;
                    rgba[p * 4] = c.X * lit;
                    rgba[(p * 4) + 1] = c.Y * lit;
                    rgba[(p * 4) + 2] = c.Z * lit;
                    rgba[(p * 4) + 3] = 1;
                }
            }
        }
    }
}
