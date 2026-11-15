# Models from other programs

Other programs can send models straight into the map you have open: an asset browser sends a mesh you picked, a prop
generator sends the rock it just made. Within two seconds, the model is in the map's models folder, its kind is in the
catalog, and the [objects drawer](objects.md) opens on it, ready to place.

## Use it

1. Open a map in Groundwork.
2. In the other program, send the model to Groundwork.
3. The status bar says "1 model from … added to the map", and the drawer shows it. Click it, then click the ground.

A model sent while no map is open waits until you open one. A model sent again from the same program replaces the old
one: the objects already placed take its new look.

A program can send more with its models:

- **Its Unreal meshes.** When the map is [linked to an Unreal project](unreal.md), a program can copy its original
  Unreal assets into that project and name them. The map then places those meshes in Unreal ([Live](unreal.md) and
  Send to Unreal) instead of the sent models, and **Bring the models into Unreal** leaves them out.
- **A collection.** A collection of models comes in as a biome of its name: the Biome tool paints it at once, and the
  map's catalog sets its weights.
- **Where the model is in its library.** Right click its picture in the drawer: **Show in …** opens that program on
  it (when the program says where it is installed, below).

Sent models go into a folder per program in the map's models folder (`models/<program>/`), and their kinds into the
map's catalog. If the map had no catalog of its own, it gets one (`catalog.json`, named in `map.json`).

## Tips and limits

- A model is refused, and the other program says why, when its name is taken by another model of the map, or when it
  needs files beside it (its textures must be inside the `.glb`).
- Sending adds files to the map's folder at once: it is not a step in History. To take a model out, delete its file and
  its kind in the catalog.

## For program authors

The inbox is the folder `inbox` in Groundwork's data folder: `$XDG_DATA_HOME/Groundwork/inbox` on Linux
(`~/.local/share/Groundwork/inbox` when `XDG_DATA_HOME` is not set), `%LOCALAPPDATA%\Groundwork\inbox` on Windows.
Make it if it is not there.

A delivery is a folder of its own. Write it as `inbox/.<id>.partial/`, then rename it to `inbox/<id>/` when it is
whole; Groundwork leaves names starting with `.` alone. `<id>` is any plain name that is unique, such as
`myprogram-20261115-153012-a1b2`.

The folder holds `send.json` and the `.glb` files:

```json
{
  "version": 1,
  "source": "My Program",
  "kinds": [
    { "file": "Pine_01.glb", "model": "Pine_01", "label": "Pine", "category": "Trees", "scale": [0.8, 1.2] }
  ]
}
```

| Field | What it is |
|---|---|
| `version` | 1. |
| `source` | The program's name, at most 60 characters: the models go in a folder of that name. |
| `kinds` | 1 to 200 models. |
| `file` | A `.glb` in the delivery, by a plain name. The model is named by its file (`model`, if given, must match). |
| `label` | The name the drawer shows; the model's name if left out. |
| `category` | Trees, Rocks, Bushes, Plants, Props, Buildings, Markers or Other (anything else is Other). |
| `scale` | The size range objects are placed in; `[1, 1]` if left out. |
| `unreal` | Optional: the Unreal object path of the asset this model stands for, copied into the map's Unreal project (`/Game/Props/SM_Rock.SM_Rock`: letters, digits and `_` between the slashes). |
| `shelf` | Optional: the model's id in the program's library, for **Show in …** (at most 400 characters, not starting with `-`). |

Two more fields can go at the top, beside `source`:

| Field | What it is |
|---|---|
| `unrealProject` | The `.uproject` the `unreal` assets were copied into. They are kept only when it is the map's own; otherwise the models come in without them and the answer has `warnings`. |
| `collection` | A name of at most 80 characters: the delivery's models become the biome of that name, replacing one sent before. |

Each `.glb` is at most 512 MB and holds everything it needs: no `uri` in its images or buffers other than `data:`.
Metres, Y up (glTF's own axes), the origin at the model's base, where it meets the ground.

Groundwork answers in `inbox/.results/<id>.json`, `{"ok": true, "map": "<name>", "models": [...]}` (with
`"warnings": [...]` when part of it was left out) or `{"ok": false, "error": "..."}`, and deletes the delivery. No answer within a few seconds means no map is open (or
Groundwork is not running): the delivery waits.

While a map is open, `inbox/.map.json` says which, and the Unreal project it is linked to, so a program knows where
to copy its Unreal assets before it sends: `{"version": 1, "map": "<name>", "unrealProject": "<.uproject or null>",
"pid": <Groundwork's process>}`. It is deleted when no map is open.

### Show in your program

To get **Show in …** on the models you send, write `libraries/<program>.json` in Groundwork's data folder (beside
`inbox`) each time your program starts: `{"version": 1, "name": "<the source you send as>", "exe": "<absolute path
of your program>"}`. Groundwork runs it as `<exe> --show <shelf id>` (no shell) and does not wait for it.

## Related

- [Objects](objects.md#models): the models folder and the catalog
