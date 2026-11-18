using Avalonia.Headless.XUnit;
using Xunit;

namespace Groundwork.Desktop.Tests;

/// <summary>Recorded play sessions (PlayLog), their replay (ReplayPanel) and Claude's playlog_summary, on made-up files.</summary>
public sealed class PlayLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gw-playlog-" + Guid.NewGuid().ToString("N"));

    public PlayLogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private const string Head = """{"playlog":1,"game":"test","build":"abc","map":{"name":"Field","team":"t1","version":2},"player":"0a1b2c3d","started":"2026-11-18T20:14:03Z"}""";

    /// <summary>A session: walks east for 10 s, stands 40 s (a pause of 20 s in it), dies, respawns far away, teleports, ends.</summary>
    private static string Session()
    {
        var w = new System.Text.StringBuilder(Head).Append('\n');
        for (int t = 0; t <= 10; t++)
        {
            w.Append(System.Globalization.CultureInfo.InvariantCulture, $$"""{"t":{{t}},"e":"pos","p":[{{t * 2}},5,0],"yaw":90}""").Append('\n');
        }
        // standing at x 20 from 10 s to 30 s (20 s), a pause, then standing again 40..80 s (40 s): one stop, not 70 s
        for (int t = 11; t <= 30; t++)
        {
            w.Append(System.Globalization.CultureInfo.InvariantCulture, $$"""{"t":{{t}},"e":"pos","p":[20.5,5,0.5],"yaw":90}""").Append('\n');
        }
        w.Append("""{"t":31,"e":"pause","op":"on"}""").Append('\n').Append("""{"t":40,"e":"pause","op":"off"}""").Append('\n');
        for (int t = 40; t <= 80; t++)
        {
            w.Append(System.Globalization.CultureInfo.InvariantCulture, $$"""{"t":{{t}},"e":"pos","p":[21,5,1],"yaw":350}""").Append('\n');
        }
        w.Append("""{"t":81,"e":"quest","id":"q_well","step":"accept"}""").Append('\n');
        w.Append("""{"t":82,"e":"death","p":[21,5,1],"by":"Wolf"}""").Append('\n');
        w.Append("""{"t":90,"e":"respawn","p":[-100,3,-100]}""").Append('\n');
        w.Append("""{"t":91,"e":"pos","p":[-100,3,-98],"yaw":10}""").Append('\n');
        w.Append("""{"t":92,"e":"teleport","p":[50,3,50]}""").Append('\n');
        w.Append("""{"t":93,"e":"dance","p":[50,3,51]}""").Append('\n');   // an event of the game's own: kept, not listed
        w.Append("""{"t":95,"e":"end","why":"quit"}""").Append('\n');
        return w.Append("""{"t":96,"e":"po""").ToString();   // a crash's half line
    }

    private string Write(string text, string name = "s.playlog")
    {
        string f = Path.Combine(_dir, name);
        File.WriteAllText(f, text);
        return f;
    }

    [Fact]
    public void ASessionIsReadIntoLegsStopsAndItsEnd()
    {
        var (log, problem) = PlayLog.Read(Write(Session()));
        Assert.Null(problem);
        Assert.Equal(("Field", "t1", 2, "0a1b2c3d"), (log!.MapName, log.MapTeam, log.MapVersion, log.Player));
        Assert.Equal(1, log.Skipped);
        Assert.Equal("quit", log.Ended);
        Assert.Equal(9, log.Paused);
        Assert.Equal(95, log.Length);
        // broken at the respawn and at the teleport
        Assert.Equal(3, log.Legs.Count);
        Assert.Equal(new System.Numerics.Vector3(-100, 3, -100), log.Legs[1].Points[0].P);
        // the pause splits the standing: only the stretch after it is a stop (the 20 s before is too short)
        var stop = Assert.Single(log.Stops);
        Assert.Equal((40f, 82f), (stop.From, stop.To));   // still there when it died
    }

    [Fact]
    public void ThePlayerIsBetweenTwoPointsAndTurnsTheShortWay()
    {
        var (log, _) = PlayLog.Read(Write(Session()));
        var (p, yaw, map) = log!.At(2.5f)!.Value;
        Assert.Equal(5, p.X, 3);
        Assert.Equal(90, yaw, 3);
        Assert.Equal("Field", map);
        // 90° to 350° is 100° back the short way: half of it at 35 s, between the samples at 30 and 40 s
        Assert.Equal(40, log.At(35)!.Value.Yaw, 1);
        Assert.Null(log.At(91.5f));   // between the walk after the respawn and the teleport: a break
    }

    [Fact]
    public void WhatIsNoPlayLogSaysSo()
    {
        Assert.StartsWith("Not a play log", PlayLog.Read(Write("{\"hello\":1}\n")).Problem);
        Assert.Contains("version 7", PlayLog.Read(Write("{\"playlog\":7}\n")).Problem);
        Assert.Equal("No such file.", PlayLog.Read(Path.Combine(_dir, "none.playlog")).Problem);
    }

    [Fact]
    public void TheSummaryHasDeathsStopsAndQuests()
    {
        string said = PlayLog.Read(Write(Session())).Log!.Summary();
        Assert.Contains("Length 1:35, of it paused 0:09; ended: quit. 1 lines could not be read.", said);
        Assert.Contains("- 1:22 at x 21, z 1 (height 5), by Wolf", said);
        Assert.Contains("- 0:42 from 0:40 at x 21, z 1", said);
        Assert.Contains("- 1:21 q_well: accept", said);
        Assert.Contains("dance 1", said);
    }

    [AvaloniaFact]
    public async Task ReplayListsTheEventsAndClaudeReadsTheSameFacts()
    {
        var map = MapProject.Create("Field", 2, generated: false, height: 10, seed: 1, under: _dir);
        var s = map.Read().Session!;
        var w = TestWindows.Editing(s);
        string file = Write(Session());
        Assert.StartsWith("Replaying s.playlog: 1 deaths, 1 stops", w.OpenReplay(file));
        Assert.Equal(["0:40  stood still 0:42", "1:21  quest q_well: accept", "1:22  death, by Wolf", "1:30  respawn", "1:32  teleport", "1:35  end: quit"], w.Replay.Lines);
        Assert.NotNull(w.View.Replay);
        Assert.Equal(3, w.View.Replay!.Legs.Count);
        w.Replay.SeekTo(5);
        Assert.Equal(10, w.View.ReplayPlayer!.Value.P.X, 3);
        var t = new ClaudeTools(w);
        Assert.Contains("by Wolf", await t.PlaylogSummary());
        Assert.Contains("by Wolf", await t.PlaylogSummary(file));
        Assert.StartsWith("Give a .playlog", await t.PlaylogSummary(Path.Combine(_dir, "map.json")));
        w.Replay.Close();
        Assert.Null(w.View.Replay);
        Assert.StartsWith("No play session", await t.PlaylogSummary());
    }

    [Fact]
    public void AnotherMapOrVersionIsWarnedOf()
    {
        var map = MapProject.Create("Field", 2, generated: false, height: 10, seed: 1, under: _dir);
        var log = PlayLog.Read(Write(Session())).Log!;
        Assert.Contains("never published", MainWindow.ReplayWarning(log, map));
        map.SetTeam("t1", 3);
        Assert.Contains("version 2 of this map; this is version 3", MainWindow.ReplayWarning(log, map));
        map.SetTeam("t1", 2);
        Assert.Null(MainWindow.ReplayWarning(log, map));
        map.SetTeam("t9", 2);
        var other = PlayLog.Read(Write(Session().Replace("\"name\":\"Field\"", "\"name\":\"Hills\""), "o.playlog")).Log!;
        Assert.Contains("Recorded on the map Hills", MainWindow.ReplayWarning(other, map));
    }
}
