# Claude

Groundwork can let Claude work in the editor: look at the map, sculpt, paint, place and move objects, and draw
rivers, each change a step you can see and undo.

## Connect

In **Settings**, turn on **Allow Claude to connect**. The editor then serves Claude's tools on `127.0.0.1` (the port in
Settings) for callers that send its token, and shows the command that adds it to Claude Code:

```sh
claude mcp add --transport http groundwork http://127.0.0.1:5731/mcp --header "Authorization: Bearer <token>"
```

Claude Desktop's configuration is shown beside it. Another address (every network, or one of this computer's) lets a
Claude on another computer connect; the token is asked for on every address.

## What Claude can do

Claude opens your maps (only ones you opened yourself), looks at the 3D view and the ground, lists and selects objects,
sculpts and paints with the brushes, places, moves, copies and removes objects, edits rivers and lakes, lays paths and
ramps, draws areas, makes holes and terraces, paints, fills or erodes the whole map, makes new zones, and saves. With
Unreal open it can start the game at a point (Play here); making the Unreal landscape asks you first. Every
change is a step marked as Claude's in History; **Take back Claude's changes** removes them.
