# The Mask

The Mask limits the ground tools to ground that matches it. Turn it on in the brush card, then set any of:

| Setting | Ground that passes |
|---|---|
| Height | Between the lowest and highest height given (`Alt+Shift`+click takes a band of 2 m around a spot). |
| Slope | Between the smallest and largest slope given, in degrees. |
| Paint | Any, unpainted, painted, or mostly one of the map's layers. |
| Map | Where a terrain map is between the two values given (0 to 1): flow, wetness, ridges (convexity: 0.5 is even ground, more on ridges, less in hollows), facing north, facing east, or snow. They are the maps [Export the terrain maps](map.md#exporting-the-terrain-maps) writes, worked out on the ground as it is. |

An empty box means no limit. The Mask is judged on the ground as it was when the stroke began, so a stroke does not
stop itself.

**Save as picture…** writes the Mask over the whole map as a black-and-white PNG, north up, one pixel a metre: white
where the ground passes, black where it does not. Check a slope or height band with it before painting the whole map
by it ([Paint the whole map](terrain.md)); the status bar says how much of the ground passes.

## Placing by the terrain

With a map in the Mask, the brushes, **Brush**, **Biome** and the area actions follow the ground's shape:

| To put | Mask |
|---|---|
| Woods in the wet valley floors | **Wetness** from 0.6, slope up to 25° |
| Rocks and scree on the ridges | **Ridges** from 0.6, slope from 25° |
| Reeds and wet plants along the streams | **Flow** from 0.4 |
| Moss and ferns on the shaded side | **Facing north** from 0.7 |
| Snow paint, or snow plants | **Snow** from 0.2 |

A map is worked out once and kept while the ground stays as it is, so the first stroke after the ground changes takes a
moment on a big map.

