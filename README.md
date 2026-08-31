# maniac-lazer

In-process auto-player for [osu!lazer](https://github.com/ppy/osu). Windows/x64. Early development.

This is **not** a memory-reader. It injects a small bootstrap into a running osu!lazer process, hooks `GameHost.UpdateFrame`, and drives mania/osu!standard through the game's own `KeyBindingContainer` input path.

## Build

osu!lazer must be installed (Velopack default is `%LOCALAPPDATA%\osulazer\current`). The plugin compiles against those shipped DLLs, not against `references/osu`.

```powershell
dotnet build Maniac.sln -c Release
dotnet test tests/Maniac.Core.Tests -c Release
```

Optional: `-p:LazerDir=C:\path\to\osulazer\current`

Pack a runnable launcher folder:

```powershell
powershell -File ./scripts/pack.ps1 -Configuration Release
```

## Run

1. Start osu!lazer.
2. From the launcher output directory (or `artifacts/maniac`):

```powershell
.\maniac.exe            # inject / reload
.\maniac.exe --unload
.\maniac.exe --pid 1234
```

The in-game overlay attaches to the top-most osu! layer. `F7` toggles the panel, `F8` toggles autoplay (configurable). Collapsed, the overlay is a compact bar that does not eat gameplay input.

Settings are stored at `%APPDATA%\maniac-lazer\settings.json`.

## Layout

| Project | Role |
|---|---|
| `src/Maniac.Launcher` | Finds osu!, injects the bootstrap |
| `src/Maniac.Bootstrap` | Durable Harmony trampoline + collectible plugin load |
| `src/Maniac.Plugin` | Game bindings, overlay, session, input sink |
| `src/Maniac.Core` | Play plans, drivers, settings (no osu! types) |

`experiments/` still has the original exploration (`lazer-internal`, `lazer-inspect`). The main version lives in `src/`.

## CI

GitHub Actions compiles Core tests on Linux, and on Windows downloads the latest stable `-lazer` GitHub release, silently installs it, and builds/tests the full solution against those binaries.

Local inject/reload/unload and the startup member probe were checked against shipped **2026.804.2-lazer** (`%LOCALAPPDATA%\osulazer\current`, Windows x64).

## Troubleshooting

- Injection errors are described by the launcher; details go to `%TEMP%\maniac-lazer\bootstrap.log`.
- If autoplay stays off after a game update, open **Diag** in the overlay. A failed startup probe means a required private member moved; autoplay will not send input.
- Multiple osu! processes: pass `--pid`.
