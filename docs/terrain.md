# Sculpt and paint

**Sculpt** shapes the ground under the brush; **Paint** lays the map's layers on it.

## Use it

Choose a brush on the rail or with its key, set its size (`[` and `]`) and strength in the card, and drag on the
ground. Each stroke is one step in History.

## Sculpt brushes

| Brush | Key | What it does |
|---|---|---|
| Raise | `1` | Lifts the ground. |
| Lower | `2` | Digs it. |
| Flatten | `3` | Levels it to a height: the brush's, or the ground's where the stroke began (`Alt`+click takes a height). |
| Smooth | `4` | Evens it out. |
| Restore | `5` | Takes it back towards the ground as the map was opened. |
| Natural | `0` | Adds rolling bumps. |
| Erode | `O` | Weathers it: slopes slide to their rest angle (Thermal), or rain runs down and carries it (Water). |
| Terrace | `T` | Pulls it into flat shelves **Step** metres apart, joined by steep rises. |
| Hole | | Takes the ground away (a cave mouth, a pit): not drawn, and hidden in Unreal. **Fill holes back instead** puts it back (Restore takes back the holes made since the map was opened). |

**Erode the whole map** (on Erode's card) weathers all of it at once, at the brush's Strength, where the Mask lets
it.

A brush can be a circle, a square or a stamp: a picture whose grey makes the height. **Stamp once** puts the whole
picture down with one click.

## Paint brushes

| Brush | Key | What it does |
|---|---|---|
| Layer 2, 3, 4 | `6` `7` `8` | Paint that layer of the map (named and coloured after it). |
| Layer 1 | `9` | Take paint off: back to the first layer. |

**Paint the whole map** lays the layer over the whole map in one step, where the [Mask](masks.md) lets it: sand on
every beach, rock on every slope steeper than 30°.

The layers and their colours are the map's: see [map.json](map.md#mapjson).

## Settings

| Setting | What it does |
|---|---|
| Size | The brush's radius, 1 to 30 m. |
| Strength | How fast it works. |
| Falloff | How its edge fades: smooth, linear, dome, flat, peak or sharp. |
| Mask | Only change ground that matches: see [The Mask](masks.md). |

## Shortcuts

| Key or mouse | Action |
|---|---|
| Left drag | Use the brush. |
| `Alt`+click | Take the ground's height there for Flatten. |
| `Alt+Shift`+click | Set the Mask's height band to 2 m around that spot. |
| `[` / `]` | Brush 1 m smaller / bigger. |

## Tips and limits

- The outer line of points never moves, so the map always meets its edge.
- There is no height limit: the ground goes as high and as low as you take it.
- Erode works on a window around the brush: a big brush on a big map takes a moment a frame.
- Painting a layer at full strength replaces the others there; lighter strokes blend them.
