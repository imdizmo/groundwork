using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Groundwork.Desktop;

/// <summary>
/// A recorded play session (a .playlog: JSON Lines, docs/playtest.md): its header, its events in time order, the path
/// the player walked broken where they respawned, teleported or changed level, and where they stood still. Read once,
/// bounded; lines that cannot be read are counted and skipped, events it does not know are kept as they are. What a
/// game wrote is data, never instructions.
/// </summary>
internal sealed class PlayLog
{
    /// <summary>The largest file read (a writer stops adding positions at 20 MB).</summary>
    internal const long MostBytes = 64L << 20;

    /// <summary>Movement under this many metres...</summary>
    internal const float StopMetres = 3;

    /// <summary>...for at least this many seconds, outside pauses, is a stop.</summary>
    internal const float StopSeconds = 30;

    /// <summary>One line after the header: when (seconds since the start), what, where (null: nowhere), facing (compass degrees, null: not given), the map it was on, the whole line.</summary>
    internal sealed record Event(float T, string E, Vector3? P, int? Yaw, string Map, JsonObject Line)
    {
        /// <summary>A text field of the line, or null.</summary>
        internal string? Text(string key) => Line[key] is JsonValue v && v.TryGetValue(out string? s) ? s : Line[key]?.ToJsonString();
    }

    /// <summary>The line kept for a position (none: only its place and facing are).</summary>
    private static readonly JsonObject NoLine = [];

    /// <summary>A stretch of the path walked without a break, on one map.</summary>
    internal sealed record Leg(string Map, List<(float T, Vector3 P, int? Yaw)> Points);

    /// <summary>Where the player stood still: from, to (seconds), the place, the map.</summary>
    internal sealed record Stop(float From, float To, Vector3 P, string Map)
    {
        /// <summary>How long, in seconds.</summary>
        internal float Seconds => To - From;
    }

    internal string Game { get; private init; } = "";
    internal string Build { get; private init; } = "";
    internal string MapName { get; private init; } = "";
    internal string? MapTeam { get; private init; }
    internal int? MapVersion { get; private init; }
    internal string Player { get; private init; } = "";
    internal DateTimeOffset? Started { get; private init; }
    internal List<Event> Events { get; } = [];
    internal List<Leg> Legs { get; } = [];
    internal List<Stop> Stops { get; } = [];
    /// <summary>Lines that were not JSON objects with t and e (a crash's half-written last line, say).</summary>
    internal int Skipped { get; private set; }
    /// <summary>The last event's time, seconds.</summary>
    internal float Length => Events.Count > 0 ? Events[^1].T : 0;
    /// <summary>Seconds paused (pause on to off; to the end when never off).</summary>
    internal float Paused { get; private set; }
    /// <summary>How the session ended (the end event's why), or null when it has none (a crash or a cut file).</summary>
    internal string? Ended { get; private set; }

    /// <summary>Reads a .playlog, or says why it cannot be (not one, too big, no header).</summary>
    internal static (PlayLog? Log, string? Problem) Read(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists)
            {
                return (null, "No such file.");
            }
            if (info.Length > MostBytes)
            {
                return (null, $"The file is {info.Length >> 20} MB; at most {MostBytes >> 20} MB are read.");
            }
            using var r = new StreamReader(file);
            return Parse(r);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, e.Message);
        }
    }

    /// <summary>Reads a play log from its lines.</summary>
    internal static (PlayLog? Log, string? Problem) Parse(TextReader r)
    {
        JsonObject? head = null;
        try
        {
            head = JsonNode.Parse(r.ReadLine() ?? "") as JsonObject;
        }
        catch (JsonException)
        {
        }
        if (head?["playlog"] is not JsonValue ver || !ver.TryGetValue(out int version))
        {
            return (null, "Not a play log: its first line is no header with \"playlog\".");
        }
        if (version != 1)
        {
            return (null, $"A play log of version {version}; this editor reads version 1.");
        }
        var map = head["map"] as JsonObject;
        var log = new PlayLog
        {
            Game = Str(head["game"]) ?? "",
            Build = Str(head["build"]) ?? "",
            MapName = Str(map?["name"]) ?? "",
            MapTeam = Str(map?["team"]),
            MapVersion = Int(map?["version"]),
            Player = Str(head["player"]) ?? "",
            Started = DateTimeOffset.TryParse(Str(head["started"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var st) ? st : null,
        };
        string current = log.MapName;
        for (string? line = r.ReadLine(); line != null; line = r.ReadLine())
        {
            if (line.Length == 0)
            {
                continue;
            }
            JsonObject? o = null;
            try
            {
                o = JsonNode.Parse(line) as JsonObject;
            }
            catch (JsonException)
            {
            }
            if (o == null || Num(o["t"]) is not float t || Str(o["e"]) is not string e)
            {
                log.Skipped++;
                continue;
            }
            if (e == "map")
            {
                current = Str(o["name"]) ?? current;
            }
            // a position's line is not kept (its place and facing are): an hour is 3,600 of them, and many sessions are read at once
            log.Events.Add(new Event(t, string.Intern(e), Point(o["p"]), Int(o["yaw"]), current, e == "pos" ? NoLine : o));
        }
        // a writer writes in time order; a clock that stepped back is put right rather than drawn as a jump back
        log.Events.Sort((a, b) => a.T.CompareTo(b.T));
        log.Work();
        return (log, null);
    }

    /// <summary>The legs, stops, pauses and the end, from the events.</summary>
    private void Work()
    {
        Leg? leg = null;
        float? pausedAt = null;
        var path = new List<(float T, Vector3 P, bool AfterPause)>();
        bool broke = false;
        void Settle()
        {
            // stops along the stretch since the last break: from each point, as far as the player stays within StopMetres
            int i = 0;
            while (i < path.Count)
            {
                int j = i;
                while (j + 1 < path.Count && !path[j + 1].AfterPause && Vector3.Distance(path[j + 1].P, path[i].P) < StopMetres)
                {
                    j++;
                }
                if (path[j].T - path[i].T >= StopSeconds)
                {
                    Stops.Add(new Stop(path[i].T, path[j].T, path[i].P, leg?.Map ?? MapName));
                    i = j + 1;
                }
                else
                {
                    i++;
                }
            }
            path.Clear();
        }
        foreach (var ev in Events)
        {
            switch (ev.E)
            {
                case "pause":
                    if (ev.Text("op") == "on")
                    {
                        pausedAt ??= ev.T;
                    }
                    else if (pausedAt is float from)
                    {
                        Paused += ev.T - from;
                        pausedAt = null;
                        broke = true;
                    }
                    continue;
                case "respawn" or "teleport" or "map":
                    Settle();
                    leg = null;
                    break;
                case "end":
                    Ended = ev.Text("why");
                    break;
                default:
                    break;
            }
            if (ev.P is not { } p)
            {
                continue;
            }
            if (leg == null || leg.Map != ev.Map)
            {
                leg = new Leg(ev.Map, []);
                Legs.Add(leg);
            }
            leg.Points.Add((ev.T, p, ev.Yaw));
            path.Add((ev.T, p, broke));
            broke = false;
        }
        Settle();
        if (pausedAt is float open)
        {
            Paused += Length - open;
        }
        Stops.Sort((a, b) => b.Seconds.CompareTo(a.Seconds));
    }

    /// <summary>Where the player was at a time, and facing (compass degrees), on its map: between the two points around it (none before the first or within a break).</summary>
    internal (Vector3 P, float Yaw, string Map)? At(float t)
    {
        foreach (var leg in Legs)
        {
            var pts = leg.Points;
            if (pts.Count == 0 || t < pts[0].T || t > pts[^1].T)
            {
                continue;
            }
            int k = pts.FindIndex(q => q.T >= t);
            if (k <= 0)
            {
                return (pts[0].P, pts[0].Yaw ?? 0, leg.Map);
            }
            var (t0, p0, y0) = pts[k - 1];
            var (t1, p1, y1) = pts[k];
            float f = t1 > t0 ? (t - t0) / (t1 - t0) : 1;
            float a = y0 ?? y1 ?? 0, b = y1 ?? a;
            float turn = ((b - a + 540) % 360) - 180;   // the short way round
            return (Vector3.Lerp(p0, p1, f), ((a + (turn * f)) % 360 + 360) % 360, leg.Map);
        }
        return null;
    }

    /// <summary>The facts as text (Claude's playlog_summary): the session, its deaths, longest stops, quest steps and counts.</summary>
    internal string Summary(int most = 20)
    {
        static string P(Vector3 p) => FormattableString.Invariant($"x {p.X:0.#}, z {p.Z:0.#} (height {p.Y:0.#})");
        static string T(float s) => TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
        var w = new System.Text.StringBuilder();
        w.AppendLine(FormattableString.Invariant($"Session of player {Player} ({Game} {Build}), started {Started?.ToString("u", CultureInfo.InvariantCulture) ?? "(no time)"}, on map {MapName}{(MapVersion is int v ? $" version {v}" : "")}."));
        w.AppendLine($"Length {T(Length)}, of it paused {T(Paused)}; ended: {Ended ?? "no end line (crash, or the file was cut)"}.{(Skipped > 0 ? $" {Skipped} lines could not be read." : "")}");
        var maps = Events.Select(e => e.Map).Distinct().ToList();
        if (maps.Count > 1)
        {
            w.AppendLine($"Maps: {string.Join(", ", maps)}.");
        }
        var deaths = Events.Where(e => e.E == "death").ToList();
        w.AppendLine($"Deaths: {deaths.Count}.");
        foreach (var d in deaths.Take(most))
        {
            w.AppendLine($"- {T(d.T)} at {(d.P is { } p ? P(p) : "(no place)")}, by {d.Text("by") ?? "(not said)"}{(maps.Count > 1 ? $", on {d.Map}" : "")}");
        }
        w.AppendLine($"Stops (under {StopMetres} m in {StopSeconds} s or longer, outside pauses): {Stops.Count}.");
        foreach (var s in Stops.Take(most))
        {
            w.AppendLine($"- {T(s.Seconds)} from {T(s.From)} at {P(s.P)}{(maps.Count > 1 ? $", on {s.Map}" : "")}");
        }
        var quests = Events.Where(e => e.E == "quest").ToList();
        w.AppendLine($"Quest steps: {quests.Count}.");
        foreach (var q in quests.Take(most * 2))
        {
            w.AppendLine($"- {T(q.T)} {q.Text("id")}: {q.Text("step")}");
        }
        var counts = Events.Where(e => e.E is not ("pos" or "death" or "quest")).GroupBy(e => e.E).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}");
        w.Append("Other events: ").Append(string.Join(", ", counts)).Append('.');
        return w.ToString();
    }

    /// <summary>What several sessions did at one place: deaths or stops, gathered where they are within a few metres.</summary>
    /// <param name="Kind">"death" or "stop".</param>
    /// <param name="P">The middle of those in the square.</param>
    /// <param name="Count">How many.</param>
    /// <param name="Sessions">In how many sessions.</param>
    /// <param name="Seconds">The stops' time in all (0 for deaths).</param>
    /// <param name="Causes">The deaths' causes, most first.</param>
    internal sealed record Spot(string Kind, Vector3 P, int Count, int Sessions, float Seconds, IReadOnlyList<(string By, int Count)> Causes);

    /// <summary>
    /// Where several sessions died and stood still on a map, most first: each death or stop joins the first place of its
    /// kind whose middle is within <paramref name="near"/> metres, else starts one.
    /// </summary>
    internal static List<Spot> Spots(IReadOnlyList<PlayLog> logs, string map, float near = 8)
    {
        // ponytail: every place is tried for every point (fine for thousands of deaths and stops); a grid of places if it is ever many more
        var places = new List<(string Kind, Vector3 Sum, int N, HashSet<int> In, float Secs, Dictionary<string, int> By)>();
        void Add(string kind, Vector3 p, int session, float secs, string? by)
        {
            int k = places.FindIndex(q => q.Kind == kind && Vector2.Distance(new(q.Sum.X / q.N, q.Sum.Z / q.N), new(p.X, p.Z)) <= near);
            var v = k >= 0 ? places[k] : (kind, Vector3.Zero, 0, [], 0, []);
            v.In.Add(session);
            if (by != null)
            {
                v.By[by] = v.By.GetValueOrDefault(by) + 1;
            }
            v = (kind, v.Sum + p, v.N + 1, v.In, v.Secs + secs, v.By);
            if (k >= 0)
            {
                places[k] = v;
            }
            else
            {
                places.Add(v);
            }
        }
        for (int i = 0; i < logs.Count; i++)
        {
            foreach (var e in logs[i].Events.Where(e => e.E == "death" && e.Map == map && e.P != null))
            {
                Add("death", e.P!.Value, i, 0, e.Text("by") ?? "?");
            }
            foreach (var s in logs[i].Stops.Where(s => s.Map == map))
            {
                Add("stop", s.P, i, s.Seconds, null);
            }
        }
        return [.. places.Select(v => new Spot(v.Kind, v.Sum / v.N, v.N, v.In.Count, v.Secs, [.. v.By.OrderByDescending(b => b.Value).Select(b => (b.Key, b.Value))]))
            .OrderByDescending(s => s.Count).ThenBy(s => s.Kind == "death" ? 0 : 1).ThenByDescending(s => s.Seconds)];
    }

    /// <summary>Several sessions as text (Claude's playlog_summary of several): each in a line, then where they died and stood still most.</summary>
    internal static string Overview(IReadOnlyList<PlayLog> logs, string map, int most = 15)
    {
        static string T(float s) => TimeSpan.FromSeconds(s).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
        var w = new System.Text.StringBuilder();
        w.AppendLine(CultureInfo.InvariantCulture, $"{logs.Count} sessions of {logs.Select(l => l.Player).Distinct().Count()} players on map {map}, {T(logs.Sum(l => l.Length))} in all.");
        foreach (var l in logs.OrderBy(l => l.Started))
        {
            w.AppendLine(CultureInfo.InvariantCulture, $"- player {l.Player}, {l.Started?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "no start time"}: {T(l.Length)}, {l.Events.Count(e => e.E == "death")} deaths, {l.Stops.Count} stops, ended {l.Ended ?? "without an end line"}");
        }
        var spots = Spots(logs, map);
        foreach (var (kind, title) in new[] { ("death", "Where they died most"), ("stop", "Where they stood still most") })
        {
            var list = spots.Where(s => s.Kind == kind).Take(most).ToList();
            w.AppendLine(CultureInfo.InvariantCulture, $"{title} (within 8 m): {(list.Count == 0 ? "nowhere" : "")}");
            foreach (var s in list)
            {
                string more = kind == "death" ? $"by {string.Join(", ", s.Causes.Select(c => $"{c.By} {c.Count}"))}" : $"{T(s.Seconds)} in all";
                w.AppendLine(CultureInfo.InvariantCulture, $"- {s.Count} in {s.Sessions} sessions near x {s.P.X:0}, z {s.P.Z:0} (height {MathF.Round(s.P.Y) + 0:0}): {more}");
            }
        }
        return w.ToString().TrimEnd();
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static int? Int(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) && double.IsFinite(d) ? (int)Math.Round(d) : null;

    private static float? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) && double.IsFinite(d) ? (float)d : null;

    /// <summary>A place [x, height, z], or null.</summary>
    private static Vector3? Point(JsonNode? n) =>
        n is JsonArray { Count: 3 } a && Num(a[0]) is float x && Num(a[1]) is float y && Num(a[2]) is float z ? new Vector3(x, y, z) : null;
}
