# Reuse osu!lazer's .NET runtime

The plugin already loads into osu!'s process and uses that CLR.
The size problem is the **launcher** shipping a second copy of .NET
(`pack.ps1` is self-contained single-file, ~36 MB).

osu! is a flat self-contained layout (`hostfxr.dll` / `coreclr.dll` /
`System.*.dll` next to `osu!.exe`). A framework-dependent apphost will
not pick that up: `DOTNET_ROOT` must look like a real install
(`host/fxr/<ver>/hostfxr.dll` + `shared/Microsoft.NETCore.App/<ver>/`).

## Run

```powershell
powershell -File experiments/osu-runtime-host/run.ps1
```

Needs a local osu!lazer install (`%LOCALAPPDATA%\osulazer\current`) and
clang or gcc to build `native-host.c`. Output is gitignored under `out/`.

## What worked

**C / D.** Build a fake `DOTNET_ROOT` of hardlinks into osu!'s folder,
write a synthesized `Microsoft.NETCore.App.deps.json` (extracted from
`osu!.deps.json`'s `runtimepack.Microsoft.NETCore.App.Runtime.win-x64`
section), then either:

- set `DOTNET_ROOT` and run the framework-dependent apphost, or
- a tiny native host that `LoadLibrary`s that `hostfxr.dll` and calls
  `hostfxr_initialize_for_dotnet_command_line` + `hostfxr_run_app`

`System.Private.CoreLib.dll` then resolves to the osu! inode
(ppy stamps its FileVersion `42.42.42.42424`). `maniac --help` ran on
that runtime. `net8.0-windows` did **not** require
`Microsoft.WindowsDesktop.App`.

## What did not

| Attempt | Result |
|---|---|
| `DOTNET_ROOT` = osu `current\` (flat SCD) | hostfxr looks for `host/fxr` + `shared/...` and fails |
| Fake root **without** `Microsoft.NETCore.App.deps.json` | `No frameworks were found` + Microsoft's install URL |
| Naked FDD apphost, no `DOTNET_ROOT`, no machine runtime | same install prompt (this machine has a global 8.0 so it silently used that instead) |

Do not write the synthesized deps json into the osu install. Hardlink
(or symlink) the runtime files into a maniac-owned directory and drop
the json there.

## Ship sketch (not implemented)

User-facing `maniac.exe` is a native stub (~tens of KB if not mingw):

1. Find `%LOCALAPPDATA%\osulazer\current` (fail with "start osu! first",
   never the Microsoft dialog).
2. Build `%LOCALAPPDATA%\maniac-lazer\dotnet\<ver>\` once per osu runtime
   version (hardlinks, same volume as osu).
3. Host `maniac.dll` via `hostfxr` from that fake root.

Keep the existing C# launcher as a framework-dependent `maniac.dll`.
CI already downloads the osu nupkg and can emit the ~26 KB deps json
there, or the stub can parse `osu!.deps.json` at runtime.
