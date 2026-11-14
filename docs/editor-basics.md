# The window

Groundwork starts with a small loading window that says what it is doing, opening the last map's models and building
the window, and gives way to the start page. Opening a map keeps the window drawing: a veil says what is read (the
ground, the paint, the objects, the water), and the models stream in after.

The top bar holds **Start** (back to your maps), the map's name and size, undo and redo, **Live**, **Save**, and the
panels: **Layers**, **Checks**, **Objects**, **History**, **View** and **?**. The rail on the left holds the tools; the objects
drawer runs along the bottom; the minimap sits in the corner.

## The tool rail

| Section | Tools |
|---|---|
| Select | **Select** (`E`): pick, move, turn, copy, delete. |
| Ground | **Sculpt** (`1`) and **Paint** (`6`): see [Sculpt and paint](terrain.md). |
| Place | **Object**, **Brush**, **Biome**, **Stamp**: see [Objects](objects.md). |
| Shape | **Shape** and **Mountain**: whole shapes put into the ground with one click, see [Shapes and mountains](shapes.md). |
| Water | **Rivers**: see [Rivers and lakes](water.md). |
| Paths | **Path**: paths, roads and ramps, see [Paths and ramps](paths.md). |
| Game | **Areas**: boxes that become Unreal volumes, see [Areas](areas.md). |
| Tools | **Measure** (`M`) and **View** (`Esc` or `H`). |

Plugins add their own tools below (see [Plugins](plugins.md)).

## Camera

| Mouse or key | Action |
|---|---|
| Right drag | Turn the view. |
| Wheel | Zoom. |
| Middle drag, or `Space` + left drag | Slide the view. |
| `W` `A` `S` `D` or the arrows | Move around. |

**Orbit**, **Top** and **Walk** over the view switch the camera: orbiting a point, straight down, or at eye height.
Click the minimap to look there.

## History

Every change is a step: a stroke, a placement, a move, a paste, a river point. **History** (`L`) lists them, newest
first. Click one to go back to it; **Remove** takes one step out and keeps what came after. Steps Claude made carry a
chip, and **Take back Claude's changes** removes them all.

## View panel

**View** (`V`) shows or hides each kind of object (from the map's catalog: Trees, Rocks, Bushes, Plants, Props,
Buildings, Markers, Other) and the water. Hidden objects cannot be picked. Plants and Markers start hidden. **Slopes over** draws the
ground steeper than its angle red: where a player cannot walk up, where the cliffs are.

## Settings

**Settings** (from the start page) holds the language, the maps folder, how many steps History keeps, which plugins
may run, and whether Claude may connect.
