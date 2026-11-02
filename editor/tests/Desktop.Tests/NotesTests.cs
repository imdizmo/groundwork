using System.Numerics;
using Avalonia.Headless.XUnit;
using Groundwork.App;
using Groundwork.Sdk;
using SkiaSharp;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Notes between the user and Claude: the Notes tool pins the typed note where the user clicks and draws where they drag,
/// both kept in the map's notes folder; Claude reads them with the picture sent, answers them and pins its own.
/// </summary>
public sealed class NotesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-notes-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private (MainWindow W, ClaudeTools T, MapProject M) Open()
    {
        var map = MapProject.Create("Notes", 2, generated: false, height: 10, seed: 1, under: _dir);
        var w = TestWindows.Editing(map.Read().Session!);
        return (w, new ClaudeTools(w), map);
    }

    [AvaloniaFact]
    public void AClickPinsTheTypedNoteAndADragDraws()
    {
        var (w, _, map) = Open();
        var tool = new MapTools.NotesTool(w);
        var card = new Card { Text = "too bright" };
        tool.OnChosen(card);
        Point(tool, card, PointerAction.Press, 5, 5);
        Point(tool, card, PointerAction.Release, 5.1f, 5);
        Point(tool, card, PointerAction.Press, 0, 0);
        Point(tool, card, PointerAction.Drag, 3, 0);
        Point(tool, card, PointerAction.Drag, 6, 0);
        Point(tool, card, PointerAction.Release, 6, 1);

        var pin = Assert.Single(MapNotes.Pins);
        Assert.Equal(("too bright", 5f, 10f, 5f), (pin.Text, pin.X, pin.Y, pin.Z));
        Assert.Equal("", card.Text);   // emptied for the next note
        Assert.True(Assert.Single(MapNotes.Strokes).Count >= 3);
        Assert.NotEmpty(card.Shown);

        MapNotes.Load(Path.Combine(map.Folder, "notes"));   // as the map opened again
        Assert.Equal("too bright", Assert.Single(MapNotes.Pins).Text);
        Assert.Single(MapNotes.Strokes);
    }

    [AvaloniaFact]
    public async Task ClaudeReadsTheNotesAndThePictureAnswersAndPins()
    {
        var (_, t, _) = Open();
        var pin = MapNotes.Add(3, 10, 4, "floating?");
        using var bmp = new SKBitmap(32, 16);
        bmp.Erase(SKColors.Red);
        _ = MapNotes.Send("look at the pins", bmp.Encode(SKEncodedImageFormat.Png, 100).ToArray());

        var first = await t.MapNotes();
        string text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(first.Content[0]).Text.Replace(" ", "");
        Assert.Contains("\"new\":[\"lookatthepins\"]", text);
        Assert.Contains("floating?", text);
        Assert.IsType<ModelContextProtocol.Protocol.ImageContentBlock>(first.Content[1]);

        Assert.StartsWith("Note 1 answered", await t.MapNoteAnswer(pin.Id, "settled it"));
        Assert.True(pin.Done);
        Assert.Equal("settled it", pin.Reply);
        Assert.StartsWith("Pinned note 2", await t.MapNoteAdd(1, 2, "is this the spot?"));
        Assert.Equal("Claude", MapNotes.Pins[^1].By);

        string again = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>((await t.MapNotes()).Content[0]).Text.Replace(" ", "");
        Assert.Contains("\"new\":[]", again);
    }

    private static void Point(MapTools.NotesTool tool, Card card, PointerAction action, float x, float z) =>
        tool.OnPointer(card, new PointerInput(action, KeyModifiers.None, new Vector3(x, 50, z), -Vector3.UnitY) { Target = PointerTarget.Ground, Point = new Vector3(x, 10, z) });

    /// <summary>The Notes card: its text box, and what it shows in the view.</summary>
    private sealed class Card : IToolContext
    {
        public string Text { get; set; } = "";

        public IReadOnlyList<Shape> Shown { get; private set; } = [];

        public IAreaView Area => new PlainArea(-64, -64, 64, 64, 10);

        public float Number(string id) => 0;

        public int Choice(string id) => -1;

        public bool IsOn(string id) => false;

        public string Typed(string id) => id == "text" ? Text : "";

        public void SetText(string id, string text)
        {
            if (id == "text")
            {
                Text = text;
            }
        }

        public void SetNumber(string id, float value)
        {
        }

        public void SetList(string id, IReadOnlyList<(string Title, string Sub, string Tag)> items, int picked)
        {
        }

        public void Preview(IReadOnlyList<Shape> shapes) => Shown = shapes;

        public void Status(string text)
        {
        }

        public void Commit(string label, ToolEdit edit)
        {
        }

        public IReadOnlyList<int> Picked(string id) => [];
    }
}
