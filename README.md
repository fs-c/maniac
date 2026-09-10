# maniac-lazer

Auto-player for [osu!lazer](https://github.com/ppy/osu).

## Build

osu!lazer must be installed (Velopack default is `%LOCALAPPDATA%\osulazer\current`). The plugin compiles against those shipped DLLs.

```powershell
dotnet build Maniac.sln -c Release
dotnet test tests/Maniac.Core.Tests -c Release
dotnet test tests/Maniac.Launcher.Tests -c Release
```

Optional: `-p:LazerDir=C:\path\to\osulazer\current`

Pack a single self-contained `maniac.exe` (plugin payload is embedded and extracted on first run):

```powershell
powershell -File ./scripts/pack.ps1 -Configuration Release
```

That writes `artifacts/maniac/maniac.exe`. `dotnet build` still drops a `plugin/` folder next to the launcher for local iteration.

## Run

1. Start osu!lazer.
2. Run `maniac.exe` (packed artifact or the launcher build output):

```powershell
.\maniac.exe
```

During development, running the launcher again will reload the plugin.

## Layout

| Project | Role |
|---|---|
| `src/Maniac.Launcher` | Finds osu!, injects the plugin |
| `src/Maniac.Bootstrap` | Entry point, sets up initial hook |
| `src/Maniac.Plugin` | osu!-specific bindings, overlay, session, input |
| `src/Maniac.Core` | Humanization, mode specific drivers, settings (in principle osu!-independent) |
| `experiments/` | Initial exploration archive, humanization/replay analysis tools, etc. |
