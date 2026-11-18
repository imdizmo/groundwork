# Rivers and lakes

**Rivers** on the rail edits the map's water, kept in its water file.

A river is a line of points, each with a width, a depth and a current; its surface runs from its first point's height
to its last. A lake is an outline with a surface height and a depth.

Pick a river or lake (or make one), then paint it: drag along the ground like a brush, a river from its source down.
When you let go, it becomes a row of dots: drag one to nudge it, click the line to add one, `Shift`+click one to
delete it. Painting on from either end of a river makes it longer, and a click adds a single point. The sliders change the picked point and the whole river or lake. **Carve** cuts the bed into the ground.
Every change is a step in History.

## Finding them in the ground

**Find rivers and lakes in the ground** adds the water the ground as it is now would hold, as one step in History:

- a lake in every hollow at least a tenth of a hectare wide and a metre deep, filled to its lip (the lowest point
  of its rim, where it would spill over);
- a river wherever at least **Find: rivers drain** of ground (0.1 km² at first) runs to the same line, down to a lake,
  the sea, the map's edge or a larger river; a lake's overflow runs on as a river. Each river is wider the more ground
  it drains, its surface just under the ground and falling all the way.

Water already on the map is kept, and nothing is found where there is water already, so it can be run again after
reshaping the ground. A smaller **Find: rivers drain** finds smaller streams. The found rivers' surfaces are just under
the ground: **Carve all** cuts their beds. On a map made over a heightmap from another tool, this is the quick way to
give its valleys their water.

With [Live](unreal.md) on, Unreal rebuilds its water from each change.
