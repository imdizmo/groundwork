# Map projects

A map is a folder: a `map.json` and the files it names beside it. The start page lists the maps you opened; **Open
map folder…** adds one, **New map…** makes one.

## A new map

**New map…** asks for a name, a size in 63 m squares (the size of an Unreal landscape component, plus one metre) and
whether the ground is rolling or flat. The map goes in the maps folder (**Settings**; `Documents/Groundwork` unless you
choose another), centred on the origin, its first layer under 1.5 m. It shares the open map's model folder and starts
with a copy of its catalog.

Rolling ground can be **a whole zone** instead of gentle bumps. A form asks for its numbers:

| Setting | What it does |
|---|---|
| Style | A kind of zone to start from: rolling woods, grassland with plateaus, sky islands, broken badland at the world's edge, mesas and canyons, savanna, tundra coast, marsh coast, highlands. It fills in the numbers below and brings its own shapes. |
| Hills from, up to | The rolling ground's lowest and highest heights, metres. |
| Hills apart | How far apart the hills are, metres. |
| Mountain rim height, width | A rim of ridged mountains round the edge, high in places and lower in others (0: none). |
| Lakes | Each lake's size in hectares (`35, 18` makes two), set in the low ground with banks sloping to the water. |
| Wooded | The share of the ground under woods (not cliffs, not lakes); the woods grow in stands of one kind. |
| Trees in the woods | Trees per hectare in the woods; a few more stand in the open. |
| Tree size | How much bigger the trees are than their kinds' own size. A game's trees are often several times a real tree's: the share of ground under the crowns, not the count, is what makes a wood. |
| Seed | Another seed, the same numbers: another zone. |

The ground is painted by rule: rock on steep ground (less under trees), dirt under the woods and in patches, grass
elsewhere. The zone is planted with the open map's Trees, Bushes, Rocks and Plants.

The styles' shapes, beyond rolling hills: flat shelves with steep risers (plateaus, mesas), canyons cut down into the
ground, a coast on one side going under the sea or a cliff dropping into nothing, rivers running downhill into a lake
or the sea with their beds carved, floating islands (lumps of the map's own ground hanging in the air, earth and rock
below, trees and rocks on top; models written into its models folder as Islands kinds, so Unreal gets them like any
other), snow above a height, marsh flats with pools along
the sea, small bumps over everything so no ground is quite flat, and camps: buildings (the catalog's Buildings) in a ring
round levelled ground with props (its Props) among them, joined one to the next by levelled roads painted with the
second layer, and giant trees: a share of the trees, here and there rather than in whole groves, several times
their kind's size (a game's few great trees make much of its canopy). The first layer is painted on shores and snow
(or the second, for a coast with beaches and snow), the second in patches over the open ground, the third on steep
ground.

## map.json

```json
{
  "name": "Coast",
  "origin": [-252, -252],
  "seaLevel": 0,
  "files": { "heights": "height.npy", "layers": "layers.png", "objects": "objects.json", "water": "water.json", "areas": "areas.json", "holes": "holes.png" },
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
| `files.layers` | The paint: an RGBA `.png` the size of the heightmap, one layer a channel. |
| `files.objects` | The objects: a JSON list of `[asset, x, y, z, turn x, y, z, scale]`, and an object of [plugin data](plugins.md) when there is any. |
| `files.water` | The rivers and lakes ([Rivers and lakes](water.md)). |
| `files.areas` | The areas ([Areas](areas.md)). |
| `files.holes` | The holes: a grey `.png` the size of the heightmap, white where the ground is taken away; written once there is a hole. |
| `layers` | Up to four: the first is the ground everywhere, the others are painted over it from a channel (`r`, `g`, `b`). A layer's `texture` (a picture in the map's folder) is drawn instead of its colour, repeated every `size` metres (4 by default). |
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
