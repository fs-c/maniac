# maniac-lazer

Auto-player for [osu!lazer](https://github.com/ppy/osu).

## Build

osu!lazer binaries must be available, we compile against them. See `Directory.Build.props` for paths that are checked by default, you can override the path with `-p:LazerDir=/path/to/osulazer`.

Windows needs the Visual Studio C++ x64 tools (`cl.exe`). Linux and macOS need `cc` (clang or gcc).

```
dotnet build Maniac.sln -c Release
dotnet test tests/Maniac.Core.Tests -c Release
```

`dotnet build` also compiles the native launcher and runs its tests. The exe is `src/Maniac.Launcher/bin/Release/net8.0/maniac.exe` (or `maniac` on Linux and macOS), with a `plugin/` sidecar for local iteration.

## Run

1. Start osu!lazer.
2. Run `maniac`

Packed CI artifacts are Windows, Linux, and macOS (Apple Silicon). The packed binary embeds the plugin; a local `dotnet build` uses the `plugin/` sidecar instead.

On macOS, a downloaded binary may be quarantined; right-click the file and choose Open the first time.

## Development

Re-running maniac will reload the plugin, there's a bootstrap layer that handles this - if you touch that you have to restart osu!.

| Project | Role |
|---|---|
| `src/Maniac.Launcher` | Native finder/injector (`maniac.exe`) |
| `src/Maniac.Bootstrap` | Entry point, sets up initial hook |
| `src/Maniac.Plugin` | osu!-specific bindings, overlay, session, input |
| `src/Maniac.Core` | Humanization, mode specific drivers, settings (in principle osu!-independent) |
| `experiments/` | Initial exploration archive, humanization/replay analysis tools, etc. |
