# Rivers and lakes

**Rivers** on the rail edits the map's water, kept in its water file.

A river is a line of points, each with a width, a depth and a current; its surface runs from its first point's height
to its last. A lake is an outline with a surface height and a depth.

Pick a river or lake (or make one), then click to add a point, drag a point to move it, `Shift`+click a point to
delete it. The sliders change the picked point and the whole river or lake. **Carve** cuts the bed into the ground.
Every change is a step in History.

With [Live](unreal.md) on, Unreal rebuilds its water from each change.
