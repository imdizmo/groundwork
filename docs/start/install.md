# Install the editor

Groundwork runs on Linux, Windows and macOS with .NET 10. Until there are releases, build it from the repository:

```sh
dotnet build editor/Desktop/Groundwork.Desktop.csproj -c Release
dotnet editor/Desktop/bin/Release/net10.0/Groundwork.dll
```

The editor keeps its settings and log in its data folder: `~/.local/share/Groundwork` on Linux,
`%LOCALAPPDATA%\Groundwork` on Windows, `~/Library/Application Support/Groundwork` on macOS. The start page shows
where.

## Command line

| Option | What it does |
|---|---|
| `--data <folder>` | Another data folder (settings, log, plugins), for a second setup beside your own. |
| `--window <w>x<h>` | The window's size when it opens. |
| `--driver` | Reads commands on standard input and answers on standard output (tests and scripted runs). |

## Next

Open or make a map: see [Map projects](../map.md).
