# Paths and ramps

**Path** on the rail makes a path, a road or a ramp along a line you click.

Click points on the ground along it. Drag a point to move it, `Shift`+click one to delete it, `Backspace` takes the
last one off and `Esc` clears the line. **Make it** (`Enter`) then does, in one step of History:

- **The ground**: levelled across the path's **Width**, cut where it is higher and filled where it is lower. Its
  **Shoulder** on each side blends it into the ground round it.
- **The slope**: **Follow the ground, smoothed** keeps the ground's own rise and fall, evened out over **Smoothing**
  metres (the ends meet the ground as it is). **Even between the points** makes straight slopes from point to point:
  two points make a ramp.
- **The paint**: one of the map's layers on the path (**Paint**), or nothing.
- **Along the edges**: an object of the catalog every so many metres (**Every**), a little **Past the edge**, on both
  sides or the right one only (walking from the first point), turned along the path: posts, a fence, a row of stones.

- **Bridges**: a model of the catalog put where the line crosses a river, turned along the road at its height on the
  banks; the river's channel under it is left as it is.

## Kept roads

With **Keep it as a road** on (it is, at first), the line and how it was made are kept in the map's roads file
(`roads.json`), with the ground as it was before the road in `roads.bin` beside it. **Kept roads** lists them:

- Pick one to see its line again: drag its points, add or delete some, change its width, paint or bridges, then
  **Make it** again. Its old ground is put back first and its edge objects and bridges are replaced, all in one step.
- **Take this road away** puts back the ground it changed and removes its objects.

Putting the old ground back covers the points the road changed: what you sculpted or painted there since is put back
too. After the map grows the old ground is not known any more; the road can still be made again over the ground as it
is. Turn **Keep it as a road** off for a path that is made once and not kept (as before).

The kept roads are drawn on the [picture of the map](map.md#a-picture-of-the-map).
