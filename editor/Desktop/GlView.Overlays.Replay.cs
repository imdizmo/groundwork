using System.Numerics;

namespace Groundwork.Desktop;

// A recorded play session over the map (Replay): the path walked, its events as posts by kind, and the player at the
// replay's time with a line the way they face.
public sealed partial class GlView
{
    /// <summary>What Replay shows, in world metres; null: nothing.</summary>
    internal sealed record ReplayShown(IReadOnlyList<Vector3[]> Legs, IReadOnlyList<(Vector3 P, ReplayMark Kind)> Marks);

    /// <summary>The kinds of replay posts, each its colour.</summary>
    internal enum ReplayMark
    {
        /// <summary>A death: red.</summary>
        Death,
        /// <summary>Where the player stood still: orange.</summary>
        Stop,
        /// <summary>A quest step: green.</summary>
        Quest,
        /// <summary>Anything else with a place: pale.</summary>
        Other,
    }

    /// <summary>The colours of the posts, by kind.</summary>
    private static readonly Vector4[] ReplayColours = [new(1, 0.25f, 0.2f, 1), new(1, 0.6f, 0.1f, 1), new(0.4f, 0.95f, 0.35f, 1), new(0.85f, 0.85f, 0.8f, 1)];

    /// <summary>The session shown (Replay sets it).</summary>
    internal ReplayShown? Replay
    {
        get; set
        {
            field = value;
            _replayData = null;
            Wake();
        }
    }

    /// <summary>The player at the replay's time: where, and facing (compass degrees: 0 north, 90 east); null: not on this map then.</summary>
    internal (Vector3 P, float Yaw)? ReplayPlayer
    {
        get; set
        {
            field = value;
            Wake();
        }
    }

    /// <summary>The path's and the posts' lines in view space, worked out once a session and scene.</summary>
    private (WorldScene S, float[] Path, float[][] Posts)? _replayData;

    /// <summary>The replay's lines on the graphics card: the path, the posts by kind, the player.</summary>
    private uint _replayPathVao, _replayPathVbo, _replayPlayerVao, _replayPlayerVbo;
    private readonly uint[] _replayPostVao = new uint[4], _replayPostVbo = new uint[4];

    /// <summary>The path (segments half a metre over the ground points walked) and the posts (8 m tall, a cross 3 m wide at the top), in view space.</summary>
    internal static (float[] Path, float[][] Posts) ReplayLines(WorldScene s, ReplayShown r)
    {
        var path = new List<float>();
        foreach (var leg in r.Legs)
        {
            for (int i = 1; i < leg.Length; i++)
            {
                var a = ToView(s, leg[i - 1]) + new Vector3(0, 0.5f, 0);
                var b = ToView(s, leg[i]) + new Vector3(0, 0.5f, 0);
                path.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            }
        }
        var posts = new List<float>[4];
        for (int k = 0; k < 4; k++)
        {
            posts[k] = [];
        }
        foreach (var (p, kind) in r.Marks)
        {
            var c = ToView(s, p);
            var top = c + new Vector3(0, 8, 0);
            var list = posts[(int)kind];
            list.AddRange([c.X, c.Y, c.Z, top.X, top.Y, top.Z]);
            list.AddRange([top.X - 1.5f, top.Y, top.Z, top.X + 1.5f, top.Y, top.Z, top.X, top.Y, top.Z - 1.5f, top.X, top.Y, top.Z + 1.5f]);
        }
        return ([.. path], [.. posts.Select(l => l.ToArray())]);
    }

    /// <summary>The player's post (as a live player's) and a 3 m line the way they face, in view space.</summary>
    internal static float[] ReplayPlayerLines(WorldScene s, Vector3 p, float yaw)
    {
        float a = yaw * MathF.PI / 180;
        // a compass bearing: north is +z in the world, east +x
        var ahead = p + (new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * 3) + new Vector3(0, 1.2f, 0);
        var eye = ToView(s, p + new Vector3(0, 1.2f, 0));
        var tip = ToView(s, ahead);
        return [.. PlayerPosts(s, [("", p)]), eye.X, eye.Y, eye.Z, tip.X, tip.Y, tip.Z];
    }

    /// <summary>Draws the replay: the path amber, the posts by kind, the player light blue.</summary>
    private void DrawReplay(WorldScene s, Matrix4x4 vp)
    {
        if (Replay is not { } r)
        {
            return;
        }
        if (_replayData is not { } d || d.S != s)
        {
            var (path, posts) = ReplayLines(s, r);
            _replayData = d = (s, path, posts);
        }
        if (d.Path.Length > 0)
        {
            DrawLines(ref _replayPathVao, ref _replayPathVbo, d.Path, vp, new Vector4(1, 0.8f, 0.2f, 1), width: 1.5f);
        }
        for (int k = 0; k < 4; k++)
        {
            if (d.Posts[k].Length > 0)
            {
                DrawLines(ref _replayPostVao[k], ref _replayPostVbo[k], d.Posts[k], vp, ReplayColours[k], width: 2.5f);
            }
        }
        if (ReplayPlayer is var (p, yaw))
        {
            DrawLines(ref _replayPlayerVao, ref _replayPlayerVbo, ReplayPlayerLines(s, p, yaw), vp, new Vector4(0.31f, 0.76f, 1, 1), width: 2);
        }
    }
}
