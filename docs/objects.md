# Objects

The objects drawer along the bottom (`Ctrl+Space`) holds the map's assets, from its catalog and model folder, by
group. Click one and click the ground to place it, or drag it into the view. A kind with no icon in the catalog gets a
picture drawn from its model.

## Object

**Object** places one kind at a time: pick it, click the ground. `R` turns it 15°, `Shift+R` back.

## Brush

**Brush** scatters a group of kinds under the brush at the catalog's density and spacing: trees for a wood, plants
for a meadow. `Shift` + drag takes them away. **Keep from water** and **Random lean** shape where and how they stand. **Fill the whole map** does it everywhere at once, as one step: with the Mask on, only where it
lets (rocks on every steep slope, reeds by every shore).

## Biome

**Biome** paints a biome: its ground layer and its mix of kinds together. **Save biome** keeps the kinds selected as a
new biome in the catalog.

## Models

The map's model folder (`models` in `map.json`, else `models/` beside it) gives each asset its look:

- **glTF**: every `.glb` and `.gltf` anywhere under the folder is an asset named by its file (`Pine_01.glb` is
  `Pine_01`): its scene's meshes where its nodes put them, with their base colour and its texture, alpha cut-out or
  blending, and both sides when the material says so. A folder of glTF files needs nothing else: the drawer offers
  every model, in the group **Other** until the catalog sorts it.
- **The exporter's files** (`meshinfo.json`, `pieces/`, `meshes/`, `materials.json`, `tex/`), when the folder has them;
  a name in both is the exporter's.

An object whose asset has no model is drawn as a box.

## Stamp

Select objects and press **Save stamp** on the top bar to keep them as a stamp; **Stamp** puts the whole group down
again, each object as high above the ground as it was.

## Checks

**Checks** finds trees, plants and rocks floating above the ground, buried in it, standing under water or on ground
too steep, and selects them one by one or all at once. **Settle objects** puts them back on the ground.

## The objects list

**Objects** on the top bar lists every kind standing on the map and how many; type in **Find** to narrow it by name or
group. Pick a kind, then **Select them all**, **Go to the nearest** (the one nearest the camera, selected and framed),
**Lock or unlock them** (drawn, but clicks and boxes go past them: a forest you work around) or **Hide or show them**.
To place an object exactly, select it and type its position and turn on the Select card.

## The catalog

The map's `catalog.json` lists the kinds (their model, label, category and size range), the groups' density and
spacing, the biomes and the markers. The categories are the View panel's kinds.

A kind with `"bed": true` lies in the ground: wherever one of its objects is put, the ground rises to meet the model's
underside (a great root sunk to its widest) with a slope falling away round it, and the ground under the bed is kept
(`beds.bin`) so that moving or removing the object brings it back. Undo carries the bed with the move.
