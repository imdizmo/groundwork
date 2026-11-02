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
Unreal open it can start the game at a point (Play here) and look at the level as the game draws it (fog, lighting,
real materials); making the Unreal landscape asks you first. Every
change is a step marked as Claude's in History; **Take back Claude's changes** removes them.

Claude can also measure what it made: the slope, layer and water depth at a point; objects floating, buried or under
water; trees and buildings on ground too steep for them; roads that run under a river, lake or the sea; and how many
of a kind stand in a circle and how far apart.

## Notes for Claude

**Notes** (on the rail under MAP) is how you point Claude at things. Type a note and click where it belongs, on the
ground or on an object, to pin it there. Drag to draw a line round what you mean; `Shift`+click deletes a pin or a
line. **Send the view to Claude** takes a picture of the view as it is, pins and lines in it and each pin's number
over it, with your message. Claude reads them, answers each note (its answer shows under the note, and it can mark it
done) and pins its own notes in blue where it wants you to look or decide.

Notes live in the map's `notes` folder (`notes.json` and the last ten pictures) and are saved as you make them. They
are not part of the map: Save, undo and Unreal ignore them.
