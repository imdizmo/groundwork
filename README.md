# Groundwork

**A world editor for Unreal Engine.** Open a map, shape its ground, paint its layers, place and move objects, draw
rivers and lakes, and watch every change land in the Unreal editor as you make it.

Groundwork is built from [Valheim World Editor](https://github.com/Shuiei/valheim-world-editor) by **Thibault Bene**:
the 3D view, the brushes, selection, copy and paste, history, help, plugins and the Claude connection are his work,
kept under his MIT copyright (see [NOTICE](NOTICE.md)). Groundwork takes out everything tied to Valheim and makes it an
editor for any map.

## What you can do

- **Sculpt and paint the ground** with brushes that raise, lower, flatten, smooth, roughen, erode or restore it, and
  paint up to four named layers, each in its own colour.
- **Place objects** from the map's catalog: one by one, dragged from the drawer, scattered with a brush, or as a biome
  with its ground layer; save groups as stamps.
- **Select, move, turn, copy and delete** objects, whole buildings included; land them on floors, rocks or the ground.
- **Draw rivers and lakes** and carve their beds.
- **Edit live**: with the map's Unreal project open and the [Groundwork Bridge](https://github.com/imdizmo/groundwork-bridge)
  plugin in it, every change goes to the level at once, and changes made in Unreal come back.
- **Extend it** with plugins: tools on the rail, meshes and shapes drawn from objects' data, cuts in the ground.
- **Let Claude help** through its MCP connection, every change a step you can see and undo.

## A map project

A map is a folder with a `map.json`: its origin, sea level, layers and files (a float32 heightmap, an RGBA layers
image, an objects list, a water file), and optionally its catalog, model folder (glTF files) and Unreal project. See
[docs/map.md](docs/map.md).

To try it, open `samples/Meadow` with **Open map folder…**: a 252 m map with hills, a stream and a lake, and woods of
[Quaternius](https://quaternius.com)'s CC0 models (glTF). The SDK's sample plugin is `sdk/samples/Fences`.

## Build

```sh
dotnet build editor/Desktop/Groundwork.Desktop.csproj -c Release
dotnet editor/Desktop/bin/Release/net10.0/Groundwork.dll
```

.NET 10 on Linux, Windows or macOS. See [docs/development.md](docs/development.md) for the layout and the tests.

## Documentation

The [docs](docs/README.md) are also the editor's Help (`F1`).

## License

MIT: see [LICENSE](LICENSE) and [NOTICE](NOTICE.md).
