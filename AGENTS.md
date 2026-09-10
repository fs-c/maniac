## About

maniac is an auto-player for osu! (lazer, not stable), it's sometimes referred to as maniac-lazer to distinguish it from the defunct legacy version that targeted osu!stable.

### Codebase

- prefer to fail early and loudly if something is not as expected
- avoid introducing fallbacks, prefer to fix/clarify the root cause
- there's no expectation of strong stableness, this is experimental code that interfaces with osu! in a way that's inherently not stable

## References

To reduce the dependence on (slow) GitHub queries to research the osu! codebase, some references are provided:

- `references/osu` - osu! codebase
- `references/osu-framework` - osu! framework
- `references/tosu` - tosu (example of an application reading osu! memory)
