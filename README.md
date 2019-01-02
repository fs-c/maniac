<h2 align="center">maniac</h2>

<p align="center">Cheating in osu!mania.</p>

This is a very simple external cheat for the game [osu!](https://osu.ppy.sh/), more specifically for the [osu!mania](https://osu.ppy.sh/help/wiki/Game_Modes/osu!mania) gamemode (where this project also got its name from). It attempts to be a __simple__, __lightweight__ and __portable__ application, and it can be compiled on many Linux distributions (where it depends on the X Window System) and Windows versions.

```
  Usage: maniac [options]

  Options:

    -p         id of game process (optional)
    -l         humanization level (default: 0)
    -a         address to read time from (optional)
    -m         path to beatmap (optional)
    -r         replay humanization level delta (optional)
    -e         toggle exit checks in game loop (default: on)
    -h         print this message

```

### Humanization

_Please note that this is very far from actual and effective humanization. This feature is in an early alpha stage, at best._

The switch `-l` allows the passing of a range in which hitpoint time offsets will be generated.

For further fine-tuning, refer to the `#define RNG_*` defines and the comments of the number generation function in `beatmap.c`.

### Map fetching

If no `-m` switch is passed, `maniac` will attempt to read the beatmap from the window title, and idle while you're in the menus.

Beatmaps are read from the `DEFAULT_OSU_PATH` defined in `osu.h`, and it is assumed that the folder structure has not been tampered with (eg that `Songs/` contains only folders, etc).

The default osu! path on Windows is `C:\Users\<username>\AppData\local\osu!\Songs`, and on Linux it's `/home/<username>/osufolder/Songs`. Since you will likely have gone through some hoops to get osu! running on Linux, it's probably easiest to just symlink `~/osufolder` to wherever you keep your osu! files. 

### Replay functionality

The `-r` switch enables replays, meaning that, after the end of the current map has been reached, `maniac` will restart the current map and play it again.

To (very crudely) simulate human progression, `-r` accepts a value that will be subtracted from the current humanization level at every replay.

## Compilation

```bash
$ sudo apt-get install build-essential
$ git clone https://github.com/LW2904/maniac.git
$ cd maniac
$ ./build.sh
```

If you are on Windows, use [MinGW](http://www.mingw.org/) to compile.

To enable debug logging, tell your compiler to add the `DEBUG` define (e.g. `-D DEBUG` for GCC).

## Scripts

`scripts/find.js` will search for beatmaps by a number of tags which will be matched against the individual maps names. The best match will be copied to `./map.osu`.

This is a relic from when `-m` was not yet implemented, but it can still be useful for when beatmap searching is failing (and it is, in some edge cases).

Usage is as follows, and requires [NodeJS](https://nodejs.org/en/).

```bash
$ node scripts/find.js Chroma,Heaven
copied ..\..\AppData\Local\osu!\Songs\738673 Chroma - I\Chroma - I (Lude) [Heavenly].osu to ./map.osu
```

By default the script will search for folders in `%USERPROFILE%\AppData\Local\osu!\Songs\`, but you may provide an alternative path by setting the `songsPath` property in `scripts/settings.json` (you will have to create this file).

Example `scripts/settings.json`:

```JavaScript
{
	"songsPath": "~/osufolder/Songs/"
}
```