# Select

**Select** (`E`) picks objects to move, turn, copy, hide or delete.

## Use it

Click an object to select it, or drag a box over the ground to select what is inside. Drag a selected object to move
the selection; drag an arrow or the ring of the handle to move along an axis or turn.

## Selecting

| Mouse or key | Action |
|---|---|
| Click | That object alone. Click again: the next one behind it. |
| `Ctrl`+click | Add it, or take it out. |
| `Shift`+click | Take it out. |
| Drag on empty ground | Select what the box holds. **A drag draws** in the card makes it a zone on the ground instead. |
| Double-click a building part | The whole building (the parts that touch). |
| `Ctrl+A` / `Alt+A` | Everything shown / nothing. |
| `Ctrl+I` | Invert the selection. |

**Same kind** selects every object of the kinds selected; **Replace with** swaps them for another kind; saved
selections keep a selection by name.

## Copy and paste

`Ctrl+C` copies the selection (with the data plugins keep on the objects), `Ctrl+X` cuts it, `Ctrl+V` starts pasting.
While pasting, click to put the copy down (again for more), `R` turns it 90°, `F` mirrors it, `,` and `.` turn it by
1° (`Shift`: 15°), `Esc` stops. Trees and the like keep their height above the ground where they land; building parts
keep the copy's shape.

## Settings

| Setting | What it does |
|---|---|
| Put each object on the ground when moving | Each moved object lands on what is under it: a floor, a rock, the ground. |
| Select through | A box also takes what is hidden behind others (`Alt+Z`). |
| Pick | Everything shown, or only nature, or only buildings. |

## Shortcuts

| Key | Action |
|---|---|
| `,` / `.` | Turn the selection by 1° (`Shift`: 15°). |
| `PgUp` / `PgDn` | Lift or lower it by 0.25 m (`Shift`: 1 m). |
| `End` | Drop it onto the ground. |
| `Del` | Delete it. |
| `Ctrl+H` / `Alt+H` | Hide it / show the hidden. |
| `Shift+F` | Frame it. |
| `Esc` | Cancel the move. |

## Tips and limits

- A move is a step: the moved objects are new copies, and undo brings the old ones back.
- Hidden objects are only hidden in the view; saving keeps them.
