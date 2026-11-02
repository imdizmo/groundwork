using System.Numerics;
using System.Text.Json;

namespace Groundwork.Desktop;

/// <summary>
/// The map's notes between the user and Claude (the Notes tool, Claude's map_notes): pins with a note at a point, lines
/// drawn on the ground and objects, and pictures of the view sent with a message. Kept in the map's notes folder
/// (notes.json and the pictures), written on every change: they are a conversation, not part of the map, so they have
/// no undo and Save does not wait for them.
/// </summary>
public static class MapNotes
{
    /// <summary>Pictures kept; older ones are deleted.</summary>
    private const int PicturesKept = 10;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>A note pinned at a point (editor metres).</summary>
    public sealed class Pin
    {
        /// <summary>Its number, never reused in the map.</summary>
        public int Id { get; set; }
        /// <summary>East, metres.</summary>
        public float X { get; set; }
        /// <summary>Height, metres.</summary>
        public float Y { get; set; }
        /// <summary>North, metres.</summary>
        public float Z { get; set; }
        /// <summary>What it says.</summary>
        public string Text { get; set; } = "";
        /// <summary>Who pinned it: "you" or "Claude".</summary>
        public string By { get; set; } = "you";
        /// <summary>Dealt with.</summary>
        public bool Done { get; set; }
        /// <summary>The answer to it, if any.</summary>
        public string? Reply { get; set; }
    }

    /// <summary>A picture of the view sent with a message.</summary>
    public sealed class Sent
    {
        /// <summary>The message.</summary>
        public string Message { get; set; } = "";
        /// <summary>The picture's file name in the notes folder.</summary>
        public string Picture { get; set; } = "";
        /// <summary>When, local time.</summary>
        public DateTime At { get; set; }
        /// <summary>Claude has read it.</summary>
        public bool Read { get; set; }
    }

    private sealed class File
    {
        public int Next { get; set; } = 1;
        public List<Pin> Pins { get; set; } = [];
        public List<float[][]> Strokes { get; set; } = [];
        public List<Sent> Sent { get; set; } = [];
    }

    /// <summary>The pins.</summary>
    public static List<Pin> Pins { get; private set; } = [];
    /// <summary>The lines drawn, each its points (editor metres).</summary>
    public static List<List<Vector3>> Strokes { get; private set; } = [];
    /// <summary>The pictures sent, oldest first (the last <see cref="PicturesKept"/>).</summary>
    public static List<Sent> Messages { get; private set; } = [];
    private static int _next = 1;

    /// <summary>Called when they change (the Notes tool draws them again).</summary>
    public static event Action? Changed;

    /// <summary>The notes folder of the open map, or null when none is open.</summary>
    public static string? Folder { get; private set; }

    /// <summary>Reads a map's notes (none: no notes).</summary>
    public static void Load(string folder)
    {
        Folder = folder;
        var f = System.IO.File.Exists(Path.Combine(folder, "notes.json")) ? JsonSerializer.Deserialize<File>(System.IO.File.ReadAllText(Path.Combine(folder, "notes.json"))) ?? new() : new();
        (Pins, Strokes, Messages, _next) = (f.Pins, [.. f.Strokes.Select(s => s.Where(p => p.Length >= 3).Select(p => new Vector3(p[0], p[1], p[2])).ToList())], f.Sent, f.Next);
        Changed?.Invoke();
    }

    /// <summary>A new pin, saved.</summary>
    public static Pin Add(float x, float y, float z, string text, string by = "you")
    {
        var p = new Pin { Id = _next++, X = x, Y = y, Z = z, Text = text, By = by };
        Pins.Add(p);
        Touch();
        return p;
    }

    /// <summary>A picture of the view sent with a message: written to the notes folder, the oldest beyond ten deleted.</summary>
    public static Sent Send(string message, byte[] png)
    {
        string folder = Folder ?? throw new InvalidOperationException("No map is open.");
        Directory.CreateDirectory(folder);
        var sent = new Sent { Message = message, Picture = $"sent-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png", At = DateTime.Now };
        System.IO.File.WriteAllBytes(Path.Combine(folder, sent.Picture), png);
        Messages.Add(sent);
        foreach (var old in Messages.Take(Math.Max(0, Messages.Count - PicturesKept)).ToList())
        {
            System.IO.File.Delete(Path.Combine(folder, old.Picture));
            _ = Messages.Remove(old);
        }
        Touch();
        return sent;
    }

    /// <summary>Writes them and tells the tool.</summary>
    public static void Touch()
    {
        if (Folder != null)
        {
            Directory.CreateDirectory(Folder);
            var f = new File { Next = _next, Pins = Pins, Strokes = [.. Strokes.Select(s => s.Select(p => new[] { p.X, p.Y, p.Z }).ToArray())], Sent = Messages };
            System.IO.File.WriteAllText(Path.Combine(Folder, "notes.json"), JsonSerializer.Serialize(f, Json));
        }
        Changed?.Invoke();
    }
}
