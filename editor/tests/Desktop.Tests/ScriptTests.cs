using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>
/// Scripts (ScriptHost, ScriptWindow.RunCode): mistakes said by line, a run in its own process applied as one step in
/// History, an endless loop ended by Stop, the examples compiling.
/// </summary>
[Collection("DataDir")]
public sealed class ScriptTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gw-script-").FullName;
    private readonly MapProject? _was = MapProject.Current;

    public void Dispose()
    {
        MapProject.Current = _was;
        Directory.Delete(_dir, true);
    }

    private (EditSession, MapProject) Map()
    {
        var made = MapProject.Create("Scripted", 1, generated: false, height: 2, seed: 1, under: _dir);
        var map = MapProject.Current = MapProject.Open(made.Folder);
        return (map.Read(extras: false).Session!, map);
    }

    [Fact]
    public void MistakesAreSaidWithTheirLine()
    {
        var (image, _, errors) = ScriptHost.Compile("int a = 1;\nGround.Raise(0, 0, a)\n");
        Assert.Null(image);
        Assert.Contains(errors, e => e.StartsWith("line 2:", StringComparison.Ordinal));
        Assert.Null(ScriptHost.Compile("// nothing").Image);
    }

    [Fact]
    public void EveryExampleCompiles()
    {
        foreach (var e in ScriptExamples.All)
        {
            var (image, _, errors) = ScriptHost.Compile(e.Code);
            Assert.True(image != null, $"{e.Name}: {string.Join("; ", errors)}");
        }
    }

    [Fact]
    public async Task ARunInItsOwnProcessIsOneStepInHistory()
    {
        var (s, map) = Map();
        float before = s.Ground.HeightOf(s.Ground.W / 2 + (s.Ground.H / 2 * s.Ground.W));
        int steps = s.UndoList.Count;
        string said = await ScriptWindow.RunCode("""
            Ground.Shape(Map.CenterX, Map.CenterZ, 10, (dx, dz) => 5);
            Ground.Paint(Map.CenterX, Map.CenterZ, Ground.Layers[^1]);
            Print("hello " + Map.Name);
            """, "Script: test", () => (s, map), () => { }, CancellationToken.None);
        Assert.Contains("hello Scripted", said);
        Assert.Contains("Changed", said);
        Assert.Equal(steps + 1, s.UndoList.Count);
        Assert.Equal("Script: test", s.UndoLabel);
        var g = s.Ground;
        var (col, row) = (g.W / 2, g.H / 2);
        Assert.Contains(Enumerable.Range(0, g.W * g.H), p => g.HeightOf(p) > before + 4.9f);
        Assert.True(s.Undo());
        Assert.Equal(steps, s.UndoList.Count);
        Assert.Equal(before, g.HeightOf(col + (row * g.W)), 3);
    }

    [Fact]
    public async Task ErrorsAndStopChangeNothing()
    {
        var (s, map) = Map();
        int steps = s.UndoList.Count;
        string failed = await ScriptWindow.RunCode("Ground.Raise(Map.CenterX, Map.CenterZ, 3);\nObjects.Place(\"no such kind\", Map.CenterX, Map.CenterZ);", "Script: bad", () => (s, map), () => { }, CancellationToken.None);
        Assert.Contains("line 2: ArgumentException", failed);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        string stopped = await ScriptWindow.RunCode("while (true) { }", "Script: loop", () => (s, map), () => { }, stop.Token);
        Assert.Equal("Stopped: nothing changed.", stopped);
        Assert.Equal(steps, s.UndoList.Count);
    }
}
