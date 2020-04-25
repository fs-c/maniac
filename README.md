<h2 align="center">maniac</h2>

<p align="center">Cheating in osu!mania.</p>

This is a very simple external cheat for the game [osu!](https://osu.ppy.sh/), more specifically for the [osu!mania](https://osu.ppy.sh/help/wiki/Game_Modes/osu!mania) gamemode (where this project also got its name from).

It attemps to be
- __simple__ to use through automation and reasonable defaults
- __lightweight__, in the sense that the binaries are small and fast
- __portable__ by offering binaries with equal functionality for Windows and many Linux distributions (where it depends on the X Window System)

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

_Please note that this is very far from actual and effective humanization. This feature is in an early alpha stage at best._

The switch `-l` allows the passing of a range in which hitpoint time offsets will be generated.

For further fine-tuning, refer to the `#define RNG_*` defines and the comments of the number generation function in `beatmap.c`.

### Map fetching

If no `-m` switch is passed, `maniac` will attempt to read the beatmap from the window title, and idle while you're in the menus.

Beatmaps are read from the `DEFAULT_OSU_PATH` defined in `osu.h`, and it is assumed that the folder structure has not been tampered with (eg that `Songs/` contains only folders, etc).

The default osu! path on Windows is `C:\Users\<username>\AppData\local\osu!\Songs`, and on Linux it's `/home/<username>/osufolder/Songs`. Since you will likely have gone through some hoops to get osu! running on Linux, it's probably easiest to just symlink `~/osufolder` to wherever you keep your osu! files.

### Replay functionality

The `-r` switch enables replays. After the end of the current map has been reached, `maniac` will restart the current map and play it again.

To (very crudely) simulate human progression, `-r` accepts a value which will be subtracted from the current humanization level at every replay.

## Usage

1. Download the latest build for your operating system by navigating to [releases](https://github.com/LW2904/maniac/releases) and downloading `maniac-win64-static.exe`.

2. In your terminal, run the executable you just downloaded. On Windows this most commonly means running `cmd`, navigating to the folder you downloaded the file to (usually `cd ../Downloads`) and running it through typing `maniac-v*.exe`.

## Building from Source

```bash
$ sudo apt-get install build-essential
$ git clone https://github.com/LW2904/maniac.git
$ cd maniac
$ ./build.sh
```

If you are on Windows, use [MinGW](http://www.mingw.org/) to compile. To enable debug logging, tell your compiler to add the `DEBUG` define (e.g. `-D DEBUG` for GCC).

If you know what you're doing, a basic CMake file is also provided.
