# Unreal

A map whose `map.json` names an Unreal project (`unreal.project`) can follow every change in the open Unreal level.

## The Groundwork Bridge plugin

Live talks to Unreal through the **Groundwork Bridge** editor plugin, which has a repository of its own:
[groundwork-bridge](https://github.com/imdizmo/groundwork-bridge). Its README says how to build it for your engine and
put it in your project's `Plugins` folder.

When the Unreal editor starts with the plugin, it listens on `127.0.0.1:5772` (this computer only) and writes a fresh
token to the project's `Saved/GroundworkBridge.json`; the token file is gone when Unreal closes. Nothing it does is
saved: the level is marked changed, as your own edits there would be, and you save it in Unreal. The editor and the
plugin speak a numbered protocol: **Live** refuses a plugin of another version and says which one to update.

## Live

**Live** on the top bar connects to the open level. From then on, each change goes there as you make it:

- **Ground**: the heights in the changed rectangle, into the level's landscape (its first edit layer). The map's
  pixel (0, 0) sits at its `origin` in Unreal metres, one pixel a landscape vertex.
- **Layers**: each layer's weights in the changed rectangle. With `unreal.layersTexture` set, a layer goes into its
  `channel` of that texture (the one texture the landscape material reads whose name starts so: when several do,
  nothing is written; the layers of a second or third layers file go by name); without it, into the
  landscape's paint layer of the same name. Layers the level does not have are named on the status bar.
- **Objects**: added and taken away, as instances of the mesh the models folder's `names.json` names for the asset
  (a new mesh is placed as an actor in the `Groundwork/Objects` folder). A marker becomes an actor of its catalog
  class, tagged `gw_marker:<id>`.
- **Holes**: the landscape's visibility where the Hole brush took the ground away. The landscape material needs a
  *Landscape Visibility Mask* node (into Opacity Mask, Masked blend mode) for Unreal to show them.
- **Rivers and lakes**: the level's river and lake bodies labelled `unreal.waterPrefix` + the river's name (spaces as
  `_`; `GW_Water_` by default; the prefix must be 3 or more letters, digits or `_`, ending in `_`) are rewritten in
  place; a new one is made like the first of its kind already there
  (materials, falloff), one the map no longer has is deleted. Other water (an ocean) is never touched.

Changes made in Unreal by hand come back as a step of their own ("From Unreal"): ground, objects of the meshes in
`names.json`, markers, and the rivers and lakes with the prefix.

## Making the landscape

**⋯ → Make the Unreal landscape from this map** makes the map's landscape in the open level: its size (the map's 63 m
squares, one component each), its place and its heights as they are now. A level that has a landscape already is asked
about first: rebuilding it keeps its material, edit layers and water brushes, and gives it the map's size and heights.
Maps higher than about 250 m get a larger height scale, so they fit. Nothing is saved in Unreal until you save the level;
Live then works on the new landscape.

## Play here

**▶ Play here** on the top bar starts the game in the open Unreal level (Play In Editor, in its level viewport) with the
player on the ground the camera looks at (in **Walk**: where you stand), facing the camera's way. Stop it in Unreal
(`Esc`). It needs the Groundwork Bridge plugin; Live need not be on.

## Send to Unreal

**⋯ → Make the Unreal landscape** also paints it: the bridge makes a landscape material of the map's layers (each
layer's texture repeated every `size` metres, or its colour; holes cut), a paint layer per map layer and the textures,
saved under `/Game/Groundwork/Maps/<map>`, then every layer is painted as the map has it, the holes are cut, and the rivers, lakes and the map's sea (an
ocean at its sea level, when the map has one) are made.

**⋯ → Bring the models into Unreal** imports the map's glTF models that Unreal has no mesh for yet (the models
folder's `names.json` lists those it has) through Unreal's own importer, each into its own folder under
`/Game/Groundwork/Models`, saved in the project, and adds their meshes to `names.json`. From then on Live and Send
place those objects like any others. It needs the Groundwork Bridge plugin with the import route.

**Send to Unreal** saves the map, then runs the script `unreal.apply` names, its last line shown on the status bar.
The first time a map's script runs, the editor asks: run it only when you know where the map came from. It asks again
whenever the script changes.
