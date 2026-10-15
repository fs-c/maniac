## About

maniac is an auto-player for osu!.

### Context

osu! has two versions: osu!stable (the legacy version, closed-source) and osu!lazer (the new version, open-source).

All versions of osu! have 4 game modes: "osu!" (often called classic or osu!standard), taiko, catch, and mania.

Maniac primarily targets osu!lazer + mania, but also supports osu!stable + mania. (There's experimental support for osu!lazer + standard as well.)

### Codebase

- prefer to fail early and loudly if something is not as expected
- avoid introducing fallbacks, prefer to fix/clarify the root cause
- there's no expectation of strong stableness, this is experimental code that interfaces with osu! in a way that's inherently not stable
