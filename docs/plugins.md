# Plugins

A plugin is a .NET 10 DLL built against `Groundwork.Sdk`. Put it in the `plugins` folder of the data folder; it runs
once you allow it in **Settings** (a changed file must be allowed again).

## Writing one

`sdk/samples/Fences` is a whole plugin to start from: a **Fence** tool that sets posts with a click each, and the rails
the editor then draws between them. Its entry:

```csharp
using Groundwork.Sdk;

[assembly: GroundworkPlugin(typeof(Fences.Plugin))]

namespace Fences;

public sealed class Plugin : IGroundworkPlugin
{
    public const string Post = "FencePost";

    public void Register(IPluginRegistry registry)
    {
        registry.AddTool(new FenceTool());
        registry.AddShapes(new FenceShapes(), Post);
    }
}
```

Build it and put the DLL in the plugins folder:

```sh
dotnet build sdk/samples/Fences/Fences.csproj -c Release
cp sdk/samples/Fences/bin/Release/net10.0/Fences.dll <data folder>/plugins/
```

The project references `Groundwork.Sdk` without copying it (`Private="false"`): the editor brings its own.

## What a plugin can add

| Hook | What it does |
|---|---|
| `AddTool` | A tool on the rail: its controls on a card, its clicks in the 3D view, its edits as steps of History (`ToolEdit`). |
| `AddScene` | Meshes drawn over the map from its objects and ground (a canal's trench), with holes in the editor's ground. |
| `AddShapes` | More of an object than its model: shapes built from its data (a sail, a rope). |
| `AddGroundCuts` | Where an object opens the ground (a tunnel's mouth). |

## Objects and their data

Plugins see objects as `IObjectView`: the asset's name, its place, turn and scale, and `Data`, text by key. A tool adds
objects with data and changes it (`ToolEdit.Add`, `ToolEdit.Change`; a null value takes a key away). The data is saved
with the object in the map's objects file and goes with its copies.

## Rules

Hooks run on the editor's threads, possibly at once: keep no state between calls (or lock it) and be quick. A hook that
throws turns its plugin off until the editor starts again.
