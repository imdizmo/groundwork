using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Groundwork.App;

namespace Groundwork.Desktop;

/// <summary>
/// Scripts run in a process of their own: the editor started again with <c>--script-host</c> (Serve), given the compiled
/// script and the map's snapshot on its input, answering with the changes, the script's error or "stopped" on its output
/// (RunInChild). Stop and the time limit close its input first, which ends a script at its loops' next Check, and kill
/// the process a second later when that was not enough. Its memory is capped (ScriptMemory). Both ends are the same
/// build, so the format has no version.
/// </summary>
public static partial class ScriptHost
{
    /// <summary>
    /// A script's error as its process reported it: where it happened, the exception's kind and its message; the kind is
    /// Crash when the process died or answered nothing.
    /// </summary>
    public sealed class ScriptException(string where, string kind, string message, Exception? inner = null) : Exception(message, inner)
    {
        /// <summary>"line 12: " when the error happened in the script's own code, else empty.</summary>
        public string Where { get; } = where;
        /// <summary>The exception's kind as the script threw it, or Crash when the process died.</summary>
        public string Kind { get; } = kind;
    }

    /// <summary>The most memory a script's process may hold (its .NET heap): 4 GB, so a runaway script cannot take the computer's.</summary>
    internal const long ScriptMemory = 4L << 30;

    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(1);

    private enum Answer : byte
    {
        Changes,
        Error,
        Stopped,
    }

    /// <summary>
    /// <c>--script-host</c>: runs the script given on the input and answers on the output. The input closing meanwhile is
    /// the editor's Stop. Nothing else of the editor starts.
    /// </summary>
    /// <returns>The exit code (0 once it has answered).</returns>
    internal static int Serve()
    {
        using var input = new BufferedStream(Console.OpenStandardInput(), 1 << 16);
        using var output = Console.OpenStandardOutput();
        byte[] image;
        byte[]? pdb;
        Snapshot snap;
        using (var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
        {
            (image, pdb, snap) = ReadRequest(r);
        }
        var stop = new CancellationTokenSource();
        var watcher = new Thread(() =>
        {
            try
            {
                while (input.ReadByte() >= 0)
                {
                }
            }
            catch (IOException)
            {
                // the pipe broke: the editor is gone, which is a stop too
            }
            stop.Cancel();
        })
        { IsBackground = true, Name = "script stop" };
        watcher.Start();
        // Only the answer goes on the output: a script's own Console output goes to the error output.
        Console.SetOut(Console.Error);
        using var w = new BinaryWriter(new BufferedStream(output), Encoding.UTF8);
        try
        {
            WriteAnswer(w, Execute(image, pdb, snap, stop.Token));
        }
        catch (OperationCanceledException)
        {
            w.Write((byte)Answer.Stopped);
        }
        catch (Exception ex)
        {
            w.Write((byte)Answer.Error);
            w.Write(Where(ex));
            w.Write(ex.GetType().Name);
            w.Write(ex.Message);
        }
        return 0;
    }

    /// <summary>
    /// Runs the compiled script against the snapshot in a process of its own. Cancelling ends the process (its input
    /// closed, a kill a second later), then OperationCanceledException.
    /// </summary>
    /// <exception cref="ScriptException">The script's own error, with its line; or its process died (Crash).</exception>
    public static async Task<Changes> RunInChild(byte[] image, byte[]? pdb, Snapshot snap, CancellationToken cancel)
    {
        var (file, arguments) = ChildCommand();
        var start = new ProcessStartInfo(file) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in arguments)
        {
            start.ArgumentList.Add(a);
        }
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x" + ScriptMemory.ToString("X");
        // No shell and no script text on the command line: the program is this editor (or the dotnet running it).
        using var p = Process.Start(start) ?? throw new ScriptException("", "Crash", $"the script's process ({file}) did not start");
        var errors = new StringBuilder();
        p.ErrorDataReceived += (_, e) =>
        {
            lock (errors)
            {
                if (e.Data != null && errors.Length < 2000)
                {
                    _ = errors.AppendLine(e.Data);
                }
            }
        };
        p.BeginErrorReadLine();
        var answer = new MemoryStream();
        var reading = p.StandardOutput.BaseStream.CopyToAsync(answer, CancellationToken.None);
        string? unsent = null;
        // The input stays open (closing it is the stop signal): flushed, not disposed.
        var sending = Task.Run(() =>
        {
            var buffered = new BufferedStream(p.StandardInput.BaseStream, 1 << 16);
            using var w = new BinaryWriter(buffered, Encoding.UTF8, leaveOpen: true);
            WriteRequest(w, image, pdb, snap);
            w.Flush();
            buffered.Flush();
        }, CancellationToken.None);
        try
        {
            try
            {
                await sending.WaitAsync(cancel);
            }
            catch (IOException e)
            {
                unsent = e.Message;
            }
            await p.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            p.StandardInput.Close();
            await Task.WhenAny(p.WaitForExitAsync(CancellationToken.None), Task.Delay(Grace, CancellationToken.None));
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync(CancellationToken.None);
            }
            try
            {
                await sending;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                Log.CaughtBrief(e, "finishing sending a stopped script");
            }
            throw;
        }
        finally
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
            }
        }
        await reading;
        answer.Position = 0;
        string report;
        lock (errors)
        {
            report = unsent != null ? $"the script could not be sent ({unsent})\n{errors}" : errors.ToString();
        }
        return ReadAnswer(answer, p.ExitCode, report);
    }

    internal static void WriteRequest(BinaryWriter w, byte[] image, byte[]? pdb, Snapshot s)
    {
        Block(w, image);
        w.Write(pdb?.Length ?? -1);
        if (pdb != null)
        {
            w.Write(pdb);
        }
        w.Write(s.W);
        w.Write(s.H);
        w.Write(s.Ox);
        w.Write(s.Oz);
        Block(w, MemoryMarshal.AsBytes(s.Heights.AsSpan()));
        Block(w, MemoryMarshal.AsBytes(s.Original.AsSpan()));
        Block(w, MemoryMarshal.AsBytes(s.Locked.AsSpan()));
        w.Write(s.Things.Count);
        foreach (var (index, kind, position, group, rotation, scale) in s.Things)
        {
            w.Write(index);
            w.Write(kind);
            Write(w, position);
            w.Write(group);
            Write(w, rotation);
            w.Write(scale);
        }
        w.Write(s.Kinds.Count);
        foreach (var (model, label) in s.Kinds)
        {
            w.Write(model);
            w.Write(label);
        }
        w.Write(s.Layers.Length);
        foreach (string l in s.Layers)
        {
            w.Write(l);
        }
        w.Write(s.Bounds.MinX);
        w.Write(s.Bounds.MinZ);
        w.Write(s.Bounds.MaxX);
        w.Write(s.Bounds.MaxZ);
        w.Write(s.Sea);
        w.Write(s.Name);
    }

    internal static (byte[] Image, byte[]? Pdb, Snapshot Snapshot) ReadRequest(BinaryReader r)
    {
        byte[] image = Block(r);
        int pdbLength = r.ReadInt32();
        byte[]? pdb = pdbLength < 0 ? null : Exactly(r, pdbLength);
        int w = r.ReadInt32(), h = r.ReadInt32();
        float ox = r.ReadSingle(), oz = r.ReadSingle();
        float[] heights = Floats(r), original = Floats(r);
        bool[] locked = new bool[r.ReadInt32()];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(locked.AsSpan()));
        int count = r.ReadInt32();
        var things = new List<(int, string, Vector3, string, Vector3, float)>(count);
        for (int i = 0; i < count; i++)
        {
            things.Add((r.ReadInt32(), r.ReadString(), Vector(r), r.ReadString(), Vector(r), r.ReadSingle()));
        }
        var kinds = new List<(string, string)>();
        for (int i = r.ReadInt32(); i > 0; i--)
        {
            kinds.Add((r.ReadString(), r.ReadString()));
        }
        string[] layers = new string[r.ReadInt32()];
        for (int i = 0; i < layers.Length; i++)
        {
            layers[i] = r.ReadString();
        }
        var bounds = (r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        return (image, pdb, new Snapshot(w, h, ox, oz, heights, original, locked, things, kinds, layers, bounds, r.ReadSingle(), r.ReadString()));
    }

    internal static void WriteAnswer(BinaryWriter w, Changes ch)
    {
        w.Write((byte)Answer.Changes);
        w.Write(ch.Heights.Count);
        foreach (var (p, height) in ch.Heights)
        {
            w.Write(p);
            w.Write(height);
        }
        w.Write(ch.Paint.Count);
        foreach (var (p, layer, strength) in ch.Paint)
        {
            w.Write(p);
            w.Write(layer);
            w.Write(strength);
        }
        w.Write(ch.Remove.Count);
        foreach (int i in ch.Remove)
        {
            w.Write(i);
        }
        w.Write(ch.Add.Count);
        foreach (var (model, position, rotation, scale) in ch.Add)
        {
            w.Write(model);
            Write(w, position);
            Write(w, rotation);
            w.Write(scale);
        }
        w.Write(ch.Moves.Count);
        foreach (var (i, m) in ch.Moves)
        {
            w.Write(i);
            Write(w, m.Position);
            Write(w, m.Rotation);
            w.Write(m.Scale);
        }
        w.Write(ch.Output.ToString());
    }

    /// <summary>Reads the process's answer; the script's error or a stop thrown on; no answer, or one cut short, a Crash.</summary>
    internal static Changes ReadAnswer(Stream answer, int exitCode, string errors)
    {
        try
        {
            using var r = new BinaryReader(answer, Encoding.UTF8, leaveOpen: true);
            switch ((Answer)r.ReadByte())
            {
                case Answer.Changes:
                {
                    var ch = new Changes();
                    for (int i = r.ReadInt32(); i > 0; i--)
                    {
                        ch.Heights[r.ReadInt32()] = r.ReadSingle();
                    }
                    for (int i = r.ReadInt32(); i > 0; i--)
                    {
                        ch.Paint.Add((r.ReadInt32(), r.ReadInt32(), r.ReadSingle()));
                    }
                    for (int i = r.ReadInt32(); i > 0; i--)
                    {
                        _ = ch.Remove.Add(r.ReadInt32());
                    }
                    for (int i = r.ReadInt32(); i > 0; i--)
                    {
                        ch.Add.Add((r.ReadString(), Vector(r), Vector(r), r.ReadSingle()));
                    }
                    for (int i = r.ReadInt32(); i > 0; i--)
                    {
                        ch.Moves[r.ReadInt32()] = (Vector(r), Vector(r), r.ReadSingle());
                    }
                    _ = ch.Output.Append(r.ReadString());
                    return ch;
                }
                case Answer.Error:
                    throw new ScriptException(r.ReadString(), r.ReadString(), r.ReadString());
                case Answer.Stopped:
                    throw new OperationCanceledException("the script was stopped");
                default:
                    throw new InvalidDataException("the script's process answered something else");
            }
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException)
        {
            string said = errors.Trim();
            throw new ScriptException("", "Crash", $"the script's process ended without an answer (exit code {exitCode}){(said.Length > 0 ? ":\n" + said : "")}", e);
        }
    }

    private static void Block(BinaryWriter w, ReadOnlySpan<byte> bytes)
    {
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    private static byte[] Block(BinaryReader r) => Exactly(r, r.ReadInt32());

    private static byte[] Exactly(BinaryReader r, int count)
    {
        byte[] bytes = new byte[count];
        r.BaseStream.ReadExactly(bytes);
        return bytes;
    }

    private static float[] Floats(BinaryReader r)
    {
        float[] a = new float[r.ReadInt32() / sizeof(float)];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(a.AsSpan()));
        return a;
    }

    private static void Write(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }

    private static Vector3 Vector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
