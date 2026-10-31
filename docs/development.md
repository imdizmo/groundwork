# Development

Groundwork is C# on .NET 10 with Avalonia and OpenGL (Silk.NET).

| Folder | What it holds |
|---|---|
| `sdk/Groundwork.Sdk` | The plugin API. |
| `editor/Core` | What needs no window: settings, language, log, plugins, PNG reading. |
| `editor/Desktop` | The program: the window, the 3D view, the map project, the tools. |
| `editor/tests` | Core.Tests, Desktop.Tests (headless Avalonia) and the fuzz target. |

## Build and test

```sh
dotnet build editor/Desktop/Groundwork.Desktop.csproj -c Release
dotnet test --project editor/tests/Core.Tests/Core.Tests.csproj -c Release
dotnet test --project editor/tests/Desktop.Tests/Desktop.Tests.csproj -c Release
```

The build treats every analyzer warning as an error. The Desktop tests open windows headless; on Linux run them under
a display server (`xvfb-run`).

## Docs

`docs/` is carried in the program as its Help: every page there is, but this one. Links must lead to a page and a
heading that exist (the tests check them).
