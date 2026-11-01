# Areas

**Areas** on the rail draws boxes on the map that become volumes in Unreal: triggers, blocking walls, sound and
post-process zones, kill zones, navigation modifiers. They are kept in the map's areas file (`areas.json`).

Pick a **Kind**, then drag on the ground to draw an area: its bottom starts a metre under the ground, its top **Height**
above that. Click an area to pick it and change its **Width**, **Depth**, **Height**, **Bottom** and **Turn** on the
card; drag it to move it; `Shift`+click deletes it. Every change is a step in History. The areas show while the tool is
chosen.

With [Live](unreal.md) on, each change goes into the open level: an area is a volume of its kind's class, labelled
`GW_Area_<name>` in the `Groundwork/Areas` folder and tagged `gw_area:<name>` (and its kind's tag), made, moved and
resized in place; a deleted area's volume is deleted.

## Kinds

Without a word in the catalog the kinds are Trigger (`TriggerVolume`), Blocking (`BlockingVolume`), Sound
(`AudioVolume`), Post process (`PostProcessVolume`), Kill zone (`KillZVolume`) and Navigation (`NavModifierVolume`).
The catalog's `areas` replaces them with the game's own:

```json
"areas": {
  "spawn": { "label": "Spawn zone", "unreal": "TriggerVolume", "tag": "spawn", "color": [1, 0.3, 0.25] },
  "safe": { "label": "Safe zone", "unreal": "TriggerVolume", "tag": "safe", "color": [0.3, 0.9, 0.4] }
}
```

`unreal` is any volume class (C++ or Blueprint, by name); `tag` is added to the actor so the game can find it.
