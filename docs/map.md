# Map projects

A map is a folder: a `map.json` and the files it names beside it. The start page lists the maps you opened; **Open
map folder…** adds one, **New map…** makes one.

## A new map

**New map…** asks for a name, a size in 63 m squares (the size of an Unreal landscape component, plus one metre) and
whether the ground is rolling or flat. The map goes in the maps folder (**Settings**; `Documents/Groundwork` unless you
choose another), centred on the origin, its first layer under 1.5 m. It shares the open map's model folder and starts
with a copy of its catalog.

## map.json

```json
{
  "name": "Coast",
  "origin": [-252, -252],
  "seaLevel": 0,
  "files": { "heights": "height.npy", "layers": "layers.png", "objects": "objects.json", "water": "water.json", "areas": "areas.json" },
  "layers": [
    { "name": "Grass", "colour": "#6b9945" },
    { "name": "Sand", "colour": "#ccbd85", "channel": "r" }
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
| `layers` | Up to four: the first is the ground everywhere, the others are painted over it from a channel (`r`, `g`, `b`). |
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
