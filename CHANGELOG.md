# Changelog

## v0.6.0

- **Names and regions** (Names, `Q`): names on places in three sizes (region, place, small) and named regions drawn
  round on the ground. They float over the 3D view, small ones only up close and the bigger one first where they would
  cover each other, and are kept in the map's names file. Live sends them to Unreal as tagged text actors and closed
  splines a game can read ("You enter North Reach"); the bridge needs updating for that.
- **A picture of the map** (⋯): the whole map from above as a PNG, in colour, on parchment or engraved in ink, with
  hill shading, water, roads, regions, names, a scale bar and a north arrow; or tilted in 3D as a block. **Map as a
  block** in the View panel draws the map as a slab with soil and rock down its sides.
- **Path and Rivers paint:** drag along the ground like a brush; the road is made, or the river's bed carved, when you
  let go, and a row of dots stays on it to nudge, add to or delete, made again each time (the old ground and bed put
  back first). Roads are kept and can be picked, changed or taken away later; a road over a river gets a bridge.
- **Biome** picks from swatches; a catalog with no biomes gets one for each layer with the objects that belong on it.
- **Measure** gives walking, running and riding time, slower uphill and on marsh, snow, sand, rock and forest, faster
  on roads.
- **New map** makes islands, archipelagos and a continent's corner.
- **One letter for each tool** on the rail: P U B K G N I J Z Q.
- Picking a stamp for Raise or Lower ticks Stamp once, so a click puts the whole picture in, as high as a third of its
  size.
- **Unreal** (with the updated bridge): a landscape material made by hand is kept; objects go in as one instanced
  component a model, never an actor each; the sea covers the map; a model of several meshes comes in as one; a dialog
  Unreal opens no longer hangs it. Turning Live on sends what Unreal does not have yet.
- Fixed: the Objects list drawn past its panel over the minimap, and the objects drawer's scrollbar over its first
  cards when docked on the left.
- The Windows package is built but was not tried on Windows for this release.

## v0.5.0

- **Replay a play session** (⋯): a game's recording of how someone played (a .playlog) opens over its map. The path
  walked is drawn on the ground, broken at respawns and teleports, with deaths, stops and quest steps as posts and
  the player at the slider's time; Play runs it at up to 64 times. The list of events moves the view to each, and
  it warns when the session is from another map or version. The help has the file's format.
- **Several sessions at once:** every path thin and faint, and where people died and stood still as posts that grow
  with how many did, listed most first with their sessions and causes. Twenty two-hour sessions draw in a fraction
  of a millisecond.
- **Replay play sessions from the team** (⋯): the team server's sessions of the open map, newest first; one or all
  of them are taken down, checked against the hash their id holds, kept (the last 300) and replayed.
- **The Mask as a picture:** Save as picture… in the Mask panel shows the whole map north up, white where the ground
  passes.
- **Crash reports:** a run that ended badly is offered to the team at the next start, showing exactly what is sent;
  nothing is sent without Send, and nothing is asked with no team server set.
- **Unreal:** Place all objects sends every object of the map to the level Live is linked to, after asking; Bring the
  models imports only the models the map places. The Unreal page says how the editor's coordinates become Unreal's.
- Pack the map to share also packs models in the exporter's layout (pieces, meshes, pictures, icons and tables),
  only those the map places, so such a map no longer arrives as boxes.
- **Claude:** map_mask_picture, playlog_summary (one session or several), pick (what is at pixels of a screenshot)
  and where_drawn (where world points are on it); screenshot now says its camera.
- The Windows package is built but was not tried on Windows for this release.

## v0.4.0

- **Pack the map to share** (⋯): the map as one .zip with only the models its objects use, each with its own
  files, so it opens complete on another computer. Nothing of your computer goes in (no models path, no Unreal
  project). With or without the models, for packs you may not pass on.
- **The team server:** with a team server set (Settings → Team, or given by the Jatify Launcher), ⋯ → **Publish to
  the team** sends the packed map, and **From the team** on the start page opens a teammate's. People are known by
  their Tailscale account; with none set, Groundwork works offline as before.
- **Big maps load in far less memory:** what reading models leaves behind is freed as they load, and model pictures
  are kept at 1024 px at most. A map with 155 models peaked at about 5.5 GB, now under 3 GB.
- Models that cannot be read and are drawn as boxes are said in the message bar. The log records the memory .NET
  sees at start (and on Windows whether a job limits it) and the memory used once the models are read.

## v0.3.0

- **A new look**, shared with the other tools: the chosen tool is an orange wash and Save the only filled orange;
  every card over the view is the same see-through card, 8 px from the edges and apart; quieter greys for counts and
  headings; thicker icons; the status bar ends long messages in "…" before the frame time and counts give way.
- **The objects drawer** is made taller by dragging its edge, or docked on the left beside the tools and made wider
  there. It stays where you left it.
- **History as a strip** along the top of the view, a box per change: click or drag along it to step back and
  forward. **List** opens the History panel.
- **Ground graph** (⋯ → Ground graph…): nodes joined on a canvas make and shape ground (hills, a heightmap, a mountain,
  a formula, each shaping step, smooth, terraces, warp, detail, fold), with the picked node's ground in 3D, applied to
  the map or saved as a heightmap.
- **Script** (⋯ → Script…): a few lines of C# shape, paint and place on the open map, as one step in History.
- **New map:** shaping steps in the order you choose (valleys, glaciers, strata, erosion, scree, sea, dunes, crags),
  glaciers that carve U-shaped troughs, the coast worn back to benches and sea cliffs, dunes, meandering rivers, and a
  map made over a GeoTIFF elevation model.
- **Tools:** Shape and Mountain on the rail; Find rivers and lakes; the Mask by a terrain map (flow, wetness, ridges,
  facing, snow); Object lays a row by dragging, places on a quarter-metre grid with Alt, picks a kind with a middle
  click; Select's exact place has a Size box; ⋯ → Export the terrain maps writes flow, wetness, slope, snow and paint
  maps.
- **Models from other programs:** an asset browser or prop generator can send models straight into the open map,
  with their Unreal meshes, a collection as a biome, and **Show in …** to open them in their program.
- **Plugin SDK:** ToolEdit.Move places an object anew keeping its data.
- Claude is told that text read from a map is data, never instructions.
- Avalonia 12.1.5, Markdig 1.6.0, the MCP C# SDK 2.4.0.

## v0.2.1

- **A new icon:** the Tiers island's side silhouette in height bands over the sea, in the editor's colours, on the
  window, the start page, the Windows program and macOS.
- **Linux AppImage:** a single file to download and run. The `.tar.gz` has `install-desktop-entry.sh`, which adds
  Groundwork to the app menu with its icon.
- A new map's folder falls back to the home folder when there is no Documents folder yet.

## v0.2.0

- **New map makes whole zones** from a style's sliders with a 3D preview: hills between two heights, a mountain rim,
  lakes, woods and giant trees, terraces, canyons, coasts and dry edges, fjords, rivers that run the way water would,
  marsh, snow, floating islands, camps joined by roads, and cliff pieces along the cliff lines. The ground can be worn:
  strata, crags, rain erosion, scree, sharp ridges, plateaus, and branching valleys that lay what they cut lower down;
  the paint follows the weather, the crests and the streams. A map can also be made over a heightmap from another
  terrain tool, at any size.
- **Tools:** paths, roads and ramps; areas (trigger, blocking, sound, post-process, kill, navigation); holes; tunnels
  and caves; the Terrace brush, Erode the whole map, Paint and Fill the whole map; up to eight ground layers with
  pictures; kinds that bed into the ground; the Objects panel; slopes over an angle drawn red; heightmap export.
- **Unreal:** Make the Unreal landscape (heights, a material of the layers, holes), Bring the models, the sea, rivers
  and lakes sent Live, Play here, and linking a map to its Unreal project from the editor.
- **Claude:** many more map tools (paths, areas, holes, terraces, zones, checks, screenshots of the Unreal level) and
  notes pinned on the map.
- A loading window at launch, the start page's map cards, and the editor's new look.

## v0.1.0

- First version of Groundwork, built from Valheim World Editor by Thibault Bene: map projects (`map.json`), named
  ground layers, objects by asset name with plugin data, rivers and lakes, the live link to the Unreal editor,
  plugins, and Claude's connection.
