# Map projects

A map is a folder: a `map.json` and the files it names beside it. The start page lists the maps you opened; **Open
map folder…** adds one, **New map…** makes one.

## A new map

**New map…** opens the generator: a name, a size in 63 m squares (the size of an Unreal landscape component, plus one
metre), a style, and the style's numbers as sliders, with the ground they make in 3D beside them. The preview is made
again a moment after a slider stops; drag it to turn round it, the wheel to come nearer. It shows the ground, its paint,
woods and water; trees, rocks, camps and floating islands come only with **Make the map**. The map goes in the maps
folder (**Settings**; `Documents/Groundwork` unless you choose another), centred on the origin. It shares the open map's
model folder and starts with a copy of its catalog.

The styles: rolling woods, grassland with plateaus, sky islands, broken badland at the world's edge, mesas and canyons,
savanna, tundra coast, marsh coast, highlands, fjords; each fills in every slider and brings its own shapes. **Flat
ground** and **Gently rolling ground** at the end of the list make a map with nothing on it. Every slider's number can
be typed too (past the slider's ends as well), and **Another** seed makes other ground from the same numbers. The
sliders, by group:

| Group | Sliders |
|---|---|
| Ground | The lowest and highest ground; how far apart the hills are; roughness (small bumps so no ground is quite flat); terrace steps and how much of the ground is terraced; canyons' depth, width and spacing; fjords' depth (under the sea at their mouths), width and spacing; cliffs from (ground steeper than this faced with walls of rock: the map's own cliff pieces, overlapping along it, the ground stepped behind them so the grass runs to the edge; 0: none); crags (the rock faces' grain, so deep: crags, buttresses and gullies, ribs and chutes running down them; 0: smooth). |
| Mountain rim | A rim of ridged mountains round the edge, high in places and lower in others (0: none), and its width. |
| Coast | How far in from one side the ground goes under the sea (0: none), how deep (up to 800 m), which side, or a drop into nothing instead. |
| Water | How many lakes and the biggest one's size (each next about half the last), set in the low ground with banks sloping to the water; rivers and their width; the sand along shores; marsh flats under a height, and the share of them that pools. |
| Woods & plants | The share of the ground under woods, trees per hectare in the woods and in the open, the trees' size (a game's trees are often several times a real tree's), giant trees and their size, bushes and plants per hectare. |
| Paint | The slope rock is painted from, the share of open ground patched with dirt, the height snow lies from. |
| Camps & roads | How many camps, buildings each, roads between them. |
| Extras | Floating islands. |

### Over a heightmap from another tool

**Ground → From a heightmap…** makes the map over ground you made elsewhere: export a heightmap from your terrain
tool as a 16-bit PNG, a raw `.r16` / `.raw` (16-bit, little-endian) or a raw `.r32` (32-bit float), of any width and
height. The file holds no metres, so give them: **Across** (the world's size west to east in your tool; a metre a
sample to start with), and the heights its black and its white stand for (**Black is**, **White is**: your tool's
height range; under 0 is under the sea: to put your tool's water level at the sea, take it off both). A raw file holds
no width either: when it is not square, give its **Samples across**. Pictures start at their north edge; for a raw
file written from the south, tick **Its first row is the south edge**.

The map keeps the heightmap's own scale: north to south follows from **Across**, and each side is rounded up to
Unreal's 63 m squares (the last strip carries on the heightmap's edge). It holds up to 128 squares a side (about 8 km,
Unreal's biggest landscape) and 6400 squares in all (a 5 km square's worth), so a long, narrow world fits as well as a
square one.

The generator then measures the ground (its lowest and highest ground, how far apart its hills are) and lays the zone
over it like a sheet: paint by height and slope, woods, plants and trees, snow, follow your shape, and nothing changes
the ground itself. The sliders that would (the rim, the coast, terraces, canyons, fjords, crags, roughness, marsh, cliffs,
lakes, rivers and camps) start at none; turn any of them up to have it worked into your ground. **Lowest ground** and
**Highest ground** stretch it between new heights; **Hills apart** is your ground's own and does not change. The
heightmap is kept beside the map as `base.npy` (`generator.base` in map.json), so **Make it again** lays the new numbers
over the same ground.

A generated map keeps its numbers (`generator` in map.json). Until it is worked on by hand and saved, **⋯ → Make it
again with other numbers…** opens the generator on them and makes the whole map again; **Undo make it again** goes back
to the numbers before. Once you change the map yourself it is yours, and the generator no longer offers to remake it.

The ground is painted by rule: rock on steep ground (less under trees), dirt under the woods and in patches, grass
elsewhere. The zone is planted with the open map's Trees, Bushes, Rocks and Plants.

The styles' shapes, beyond rolling hills: flat shelves with steep risers (plateaus, mesas), canyons cut down into the
ground, fjords (winding, branching inlets of the sea cut from the coast into high ground, or from every edge without a
coast: broken walls of rock, ribs and chutes running down the faces, buttresses and gullies, down to a flat floor,
deepest at the mouth, rising and narrowing inland until each ends; the walls are the ground itself, painted rock), a
coast on one side going under the sea or a cliff dropping into nothing, rivers running downhill into a lake or the sea
the way water would, round the hills on the ground's surface (a shallow bed, a pond where a hollow would hold the
water), floating islands (lumps of the map's own ground hanging in the air, rounded earth below, trees and rocks on top;
models written into its models folder as Islands kinds, so Unreal gets them like any other), snow above a height, marsh
flats with pools along the sea, small bumps over everything so no ground is quite flat, and camps: buildings (the
catalog's Buildings) in a ring round levelled ground with props (its Props) among them, joined one to the next by
levelled roads painted with the second layer, and giant trees: a share of the trees, here and there rather than in whole
groves, several times their kind's size (a game's few great trees make much of its canopy). The first layer is painted
on shores and snow (or the second, for a coast with beaches and snow), the second in patches over the open ground, the
third on steep ground.

## map.json

```json
{
  "name": "Coast",
  "origin": [-252, -252],
  "seaLevel": 0,
  "files": { "heights": "height.npy", "layers": "layers.png", "objects": "objects.json", "water": "water.json", "areas": "areas.json", "holes": "holes.png", "beds": "beds.bin" },
  "layers": [
    { "name": "Grass", "colour": "#6b9945" },
    { "name": "Sand", "colour": "#ccbd85", "channel": "r", "texture": "textures/sand.png", "size": 4 }
  ],
  "catalog": "catalog.json",
  "models": "/path/to/models",
  "stamps": "stamps",
  "unreal": { "project": "/path/to/Project", "apply": "/path/to/apply.sh", "waterPrefix": "GW_Water_" }
}
```

| Key | What it is |
|---|---|
| `origin` | Where the heightmap's first pixel is, in the engine's metres (x, y). One pixel is one metre. |
| `seaLevel` | The water's height, metres. The editor shows heights as the file has them. |
| `files.heights` | The ground: a float32 `.npy`, rows by columns, metres. |
| `files.layers` | The paint: a `.png` the size of the heightmap, one layer in each of its `r`, `g` and `b`. A map of more than four layers has a list of them, three more layers a file; files the list leaves out are named after the first (`layers2.png`, `layers3.png`), and one not made yet is unpainted. |
| `files.objects` | The objects: a JSON list of `[asset, x, y, z, turn x, y, z, scale]`, and an object of [plugin data](plugins.md) when there is any. |
| `files.water` | The rivers and lakes ([Rivers and lakes](water.md)). |
| `files.areas` | The areas ([Areas](areas.md)). |
| `files.holes` | The holes: a grey `.png` the size of the heightmap, white where the ground is taken away; written once there is a hole. |
| `files.beds` | The ground kept under the beds of bedded objects (the catalog's `bed` kinds, [Objects](objects.md#the-catalog)): a count, then grid index and height pairs (int32, float32, little-endian); written once there is a bed. |
| `layers` | Up to eight: the first is the ground everywhere, the others are painted over it from a channel: `r`, `g`, `b` of the first layers file, `r2`, `g2`, `b2` of the second, `r3` of the third (in that order when not given). A layer's `texture` (a picture in the map's folder) is drawn instead of its colour, repeated every `size` metres (4 by default). |
| `catalog` | The kinds the object tools offer and the categories the View panel sorts by. |
| `models` | The model folder the 3D view draws objects with (it may be anywhere; see [Objects](objects.md#models)). |
| `unreal` | The Unreal project [Live](unreal.md) talks to, the script **Send to Unreal** runs, the texture the layers go into (else paint layers by name), the water bodies' label prefix. |

Every file the editor writes must be inside the map's folder and must not be a link: a map from someone else cannot
write anywhere else. The old files are kept as `.bak` on each save.

## Growing a map

**Grow map** on the rail adds 63 m squares all round or on one side, flat or carrying on the edge's relief. It saves
first, then opens the bigger map.

## Shortcuts

| Key | Action |
|---|---|
| `Ctrl+S` | Save the map. |
| `Ctrl+Space` | Show or hide the objects drawer. |
| `F1` | This help, at the page of what you are using. |

## Exporting the heightmap

**⋯ → Export the heightmap** writes the map's heights as they are now to a 16-bit grey PNG or a raw `.r16` file
(Unreal's landscape import formats, and most terrain tools'): 32768 is 0 m, 128 steps a metre, as an Unreal landscape
at Z scale 100 reads them. The first row is the map's first (its origin's) row.
