# maniac-lazer

maniac-lazer is an auto-player for osu! (lazer, not stable) that is in early development.

# Context

osu! is written in C# and open-source.

The game code is available in references/osu/ - use this for reference but don't necessesarily assume that it is exactly the same as the code used to build the game binaries.

Assuming osu!lazer is installed with defaults on Windows, game binaries are available in %LOCALAPPDATA%/osu!lazer/current/.

# Prior art

As an example of an application reading osu! memory, tosu is provided in references/tosu/. (Note that since this is written in TS, it shouldn't be taken as an 1-1 example.)

Note that many osu!-related projects are targeting osu!stable, the legacy application, which is completely different from osu!lazer.
