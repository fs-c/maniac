# stable-host

Throwaway trial: read osu!stable from outside the process and reuse `Maniac.Core` for mania.

Stable is 32-bit .NET Framework. This host is `net8.0` `win-x86` so ClrMD can attach. It does not inject.

## Commands

```
dotnet run --project experiments/stable-host -c Release -- selftest
dotnet run --project experiments/stable-host -c Release -- inspect
dotnet run --project experiments/stable-host -c Release -- clocks
dotnet run --project experiments/stable-host -c Release -- probe
dotnet run --project experiments/stable-host -c Release -- watch --seconds 20
dotnet run --project experiments/stable-host -c Release -- chart
dotnet run --project experiments/stable-host -c Release -- play --seconds 30
dotnet run --project experiments/stable-host -c Release -- play --keys sdf-jkl
dotnet run --project experiments/stable-host -c Release -- bindings
```

`watch` / `chart` / `play` / `bindings` need a running `osu!.exe`. `chart` builds a `ManiaPlayPlan` and ticks `ManiaDriver` without sending keys. `play` is a perfect mania autoplayer: it loads the live note list once, polls the audio clock, and sends `SendInput` (no humanization). It never steals focus; click osu! yourself. Keys are sent only while that window is already foreground. Mania column keys are read from the live playfield (the binds the game is using); `--keys` overrides (`-` = space). `bindings` dumps the live binding table (does not send input).

Rate mods (DT/HT/NC) share the same audio-clock vs object-time domain we already read. Chart-transforming mods (Random, Mirror, co-op, key mods) are in the live converted note list. Visual/difficulty mods do not change that path. Relax/Auto are the game playing itself.

## Recovered layout (this client build)

Confuser names live in `StableLayout.cs`. If a type or field is missing, fail; do not invent a replacement.

- Audio clock: static `Int32` `#=z1ownPGA=` on `#=zySBjyD2zKcTAsOEEDA==` (audio engine). Increments in milliseconds while a song or preview is playing.
- Screen: three static `osu.OsuModes` on `#=zLQ1lSsp1Sl649eje$Q==`. The one that is `2` during play is `#=zFX09SEA=` (the others stayed 10 and 5).
- Ruleset: static `osu_common.PlayModes` (mania is 3).
- Notes: `List<#=zu87$7AJCGiKbHMhV6JOStjSGnRsYcvmuog==>`. Times are `Int32` start at +0x0C and end at +0x10 (not the doubles at +0x4/+0x3C). Column is playfield X at +0x34 (`15 + 30 * column` on this 7K map) and also `Int32` at +0x98 on the mania subclass. Rice is `#=z1zg8JBfE4I8a...`; holds are `#=zj_xBP_TgnWye...` with end > start.

## Live check (7K mania map loaded)

- 1727 objects in the main list (1288 rice, 439 holds). First note 9226ms, last 168941ms. Clock was frozen at 8935 (intro, ~300ms before the first note).
- `playMode=3`, play-screen field `=2`.
- `ManiaPlayPlan` from that list: 7 columns, 3454 press/release events.

Those lists are allocated during play. Song select / lobby has no hit-object instances.

## Go / no-go so far

Clock, ruleset, and the live note list all read from the process. `play` caches that list and drives keys off the audio clock. DT was not loaded.

The remaining risks are OS input vs raw/exclusive fullscreen, and Confuser names moving on a new stable build.
