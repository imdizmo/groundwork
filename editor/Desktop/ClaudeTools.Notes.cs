using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Groundwork.Desktop;

/// <summary>Claude's side of the Notes tool: the user's pins, lines and pictures read, answered, and Claude's own pins.</summary>
public sealed partial class ClaudeTools
{
    /// <inheritdoc/>
    [McpServerTool(Name = "map_notes", ReadOnly = true, Title = "Read the user's notes")]
    [Description("What the user marked for you with the Notes tool: the messages they sent with a picture of the view (new: not read before; the latest picture comes with this), the notes pinned at points (id, x, y, z, text, by you or Claude, done, the answer) and the lines they drew round things (points x, z). Answer a note with map_note_answer; pin your own with map_note_add. Read them when the user says they marked something.")]
    public Task<CallToolResult> MapNotes() => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return new CallToolResult { IsError = true, Content = { new TextContentBlock { Text = why } } };
        }
        var fresh = Desktop.MapNotes.Messages.Where(m => !m.Read).ToList();
        var latest = Desktop.MapNotes.Messages.LastOrDefault();
        var result = new CallToolResult
        {
            Content =
            {
                new TextContentBlock
                {
                    Text = ToJson(new
                    {
                        @new = fresh.Select(m => m.Message),
                        picture = latest == null ? null : new { message = latest.Message, at = latest.At.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), wasNew = !latest.Read },
                        pins = Desktop.MapNotes.Pins.Select(p => new { id = p.Id, x = MathF.Round(p.X, 1), y = MathF.Round(p.Y, 1), z = MathF.Round(p.Z, 1), text = p.Text, by = p.By, done = p.Done, answer = p.Reply }),
                        lines = Desktop.MapNotes.Strokes.Select(s => s.Where((_, i) => i % Math.Max(1, s.Count / 20) == 0 || i == s.Count - 1).Select(q => new[] { MathF.Round(q.X, 1), MathF.Round(q.Z, 1) })),
                    }),
                },
            },
        };
        if (latest != null && Desktop.MapNotes.Folder is { } folder && File.Exists(Path.Combine(folder, latest.Picture)))
        {
            result.Content.Add(ImageContentBlock.FromBytes(Shrink(File.ReadAllBytes(Path.Combine(folder, latest.Picture))), "image/jpeg"));
        }
        if (fresh.Count > 0)
        {
            fresh.ForEach(m => m.Read = true);
            Desktop.MapNotes.Touch();
        }
        return result;
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_note_answer", Title = "Answer a note")]
    [Description("Answers a note (map_notes' id): your answer shows under it in the user's Notes list; done (default) marks it dealt with.")]
    public Task<string> MapNoteAnswer(int id, string answer, bool done = true) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        if (Desktop.MapNotes.Pins.FirstOrDefault(p => p.Id == id) is not { } pin)
        {
            return $"No note {id}.";
        }
        (pin.Reply, pin.Done) = (answer, done);
        Desktop.MapNotes.Touch();
        return $"Note {id} answered" + (done ? " and done." : ".");
    });

    /// <inheritdoc/>
    [McpServerTool(Name = "map_note_add", Title = "Pin a note for the user")]
    [Description("Pins your own note at x, z (on the ground, or y metres up): it shows blue in the user's Notes tool. Use it to point at something you want the user to look at or decide.")]
    public Task<string> MapNoteAdd(float x, float z, string text, float? y = null) => OnUi(() =>
    {
        if (NoMap() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var pin = Desktop.MapNotes.Add(x, y ?? new SceneLayers.AreaView(s.Scene).GroundHeight(x, z), z, text, "Claude");
        return $"Pinned note {pin.Id} at {x:0.#}, {z:0.#}.";
    });
}
