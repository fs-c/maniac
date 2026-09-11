# maniac-lazer

Auto-player for [osu!lazer](https://github.com/ppy/osu).

## Build

osu!lazer must be installed. The plugin compiles against those shipped DLLs.

| Platform | Default install |
|---|---|
| Windows | `%LOCALAPPDATA%\osulazer\current` |
| macOS | `/Applications/osu!.app/Contents/MacOS` |
| Linux | `~/.local/share/osulazer/current` |

```
dotnet build Maniac.sln -c Release
dotnet test tests/Maniac.Core.Tests -c Release
dotnet test tests/Maniac.Launcher.Tests -c Release
```

Optional: `-p:LazerDir=/path/to/osulazer`

That drops a `plugin/` folder next to the launcher for local iteration. CI publishes a single-file binary per platform (plugin payload embedded, extracted on first run).

## Run

1. Start osu!lazer.
2. Run the launcher build output (`maniac.exe` on Windows, `maniac` elsewhere), or a CI artifact.

On macOS, a downloaded binary may be quarantined; right-click the file and choose Open the first time.

During development, running the launcher again will reload the plugin.

## Layout

| Project | Role |
|---|---|
| `src/Maniac.Launcher` | Finds osu!, injects the plugin |
| `src/Maniac.Bootstrap` | Entry point, sets up initial hook |
| `src/Maniac.Plugin` | osu!-specific bindings, overlay, session, input |
| `src/Maniac.Core` | Humanization, mode specific drivers, settings (in principle osu!-independent) |
| `experiments/` | Initial exploration archive, humanization/replay analysis tools, etc. |
