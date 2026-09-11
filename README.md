# maniac-lazer

Auto-player for [osu!lazer](https://github.com/ppy/osu).

## Build

osu!lazer binaries must be available, we compile against them. See `Directory.Build.props` for paths that are checked by default, you can override the path with `-p:LazerDir=/path/to/osulazer`.

```
dotnet build Maniac.sln -c Release
dotnet test tests/Maniac.Core.Tests -c Release
dotnet test tests/Maniac.Launcher.Tests -c Release
```

## Run

1. Start osu!lazer.
2. Run `maniac`

On macOS, a downloaded binary may be quarantined; right-click the file and choose Open the first time.

## Development

Re-running maniac will reload the plugin, there's a bootstrap layer that handles this - if you touch that you have to restart osu!.

| Project | Role |
|---|---|
| `src/Maniac.Launcher` | Finds osu!, injects the plugin |
| `src/Maniac.Bootstrap` | Entry point, sets up initial hook |
| `src/Maniac.Plugin` | osu!-specific bindings, overlay, session, input |
| `src/Maniac.Core` | Humanization, mode specific drivers, settings (in principle osu!-independent) |
| `experiments/` | Initial exploration archive, humanization/replay analysis tools, etc. |
