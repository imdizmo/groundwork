# Replaying play sessions

A game can record how people play a level made from a map: where they walked, where they died, where they stood
still, which quests they took. **⋯ → Replay a play session…** opens such a recording (a `.playlog` file) over the map
it was played on; pick several at once to see where many players went.

## The Replay card

- The path the player walked is drawn on the ground in amber, broken where they respawned, teleported or changed level.
- Posts mark what happened: red for deaths, orange where the player stood still (under 3 m of movement for 30 s or
  more, outside pauses: where people may be stuck), green for quest steps, pale for the rest (kills, building,
  crafting, places reached).
- The slider sets the time; **Play** runs it at 1×, 4×, 16× or 64×, the player shown as a blue post with a line the
  way they face. With **Follow** on, the view follows them.
- The list holds the session's events in time order: pick one to go to its time and place.
- **Close** takes the session off the map. Opening another map closes it too.

## Several sessions at once

Pick several files together (up to 200) and Replay shows them all: every path as a thin, faint line, and where people
died (red) and stood still (orange) as posts that grow taller where more did. Deaths and stops within 8 m of each other
count as one place. The list holds those places, most first, with how many sessions they happened in and, for deaths,
what killed them; pick one to go there.

The card warns when the session was played on another map, or on another version of this one (the map's version on
the team server): the ground may have changed since.

Claude reads the same facts with its `playlog_summary` tool: for one session its length and paused time, how it ended,
each death with its place and cause, the longest stops and the quest steps; for several, a line each and the places
where most died and stood still.

## The file

A `.playlog` is UTF-8 text, one JSON object a line, so a crash loses at most the last line; lines that cannot be read
are skipped and counted. The first line is the header:

```json
{"playlog":1,"game":"any text","build":"abc123","map":{"name":"Meadow","team":"<the map's team id>","version":2},"player":"3fa9c1d2","started":"2026-11-18T20:14:03Z"}
```

- `map`: the map the level was made from: its name, and its team id and version when it was published to the team.
- `player`: a random id the game keeps, never a name.
- `game` and `build` are shown, never interpreted.

Every later line has `t`, the seconds since `started`, and `e`, what happened. Places are `p`: `[x, height, z]` in the
map's metres (x east, z north; see [Coordinates](unreal.md#coordinates) for Unreal's), on the map's ground: where the
player's feet are, not the middle of their body. `yaw` is a compass bearing in degrees (0 north, 90 east).

| `e` | Fields |
|---|---|
| `pos` | `p`, `yaw`: once a second while playing |
| `death` | `p`, `by` |
| `kill` | `p`, `what` |
| `hit` | `p`, `from`, `dmg`, `hp` |
| `quest` | `id`, `step` |
| `craft` | `recipe`, `station` |
| `build` | `p`, `piece`, `op` |
| `gather` | `p`, `what` |
| `ui` | `panel`, `op` |
| `place` | `p`, `id` |
| `respawn`, `teleport` | `p`: the path breaks here |
| `map` | `name`, `team`, `version`: another level from here on |
| `pause` | `op`: `on` or `off`; paused time is never a stop |
| `end` | `why`: the last line of a session that closed cleanly |

Other events are kept and counted but not drawn. Files over 64 MB are not read.
