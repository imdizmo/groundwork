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
  nothing is written); without it, into the
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

## Play here

**▶ Play here** on the top bar starts the game in the open Unreal level (Play In Editor, in its level viewport) with the
player on the ground the camera looks at (in **Walk**: where you stand), facing the camera's way. Stop it in Unreal
(`Esc`). It needs the Groundwork Bridge plugin; Live need not be on.

## Send to Unreal

**Send to Unreal** saves the map, then runs the script `unreal.apply` names, its last line shown on the status bar.
The first time a map's script runs, the editor asks: run it only when you know where the map came from. It asks again
whenever the script changes.
