<h2 align="center">maniac</h2>

<p align="center">Cheating in osu!mania.</p>

This is a very simple external cheat for the game [osu!](https://osu.ppy.sh/), more specifically for the [osu!mania](https://osu.ppy.sh/help/wiki/Game_Modes/osu!mania) gamemode (where this project also got its name from). It attempts to be a __simple__, __lightweight__ and __portable__ application, and it can be compiled on many Linux distributions (where it depends on the X Window System) and Windows versions.

```
  Usage: maniac [options]

  Options:

    -m	path to .osu beatmap file
        (optional, linux: defaults to ./map.osu, windows: refer to Map fetching section)
    -d  humanization level
        (optional, defaults to 0)
    -p  PID of osu! process
        (optional, depracated)
    -a  address of the playback time in memory
        (optional, supply this if the pattern scanning fails)
```

#### Humanization

The option `-d` (short for delay) allows the passing of a range in which hitpoint time offsets will be generated.

For further fine-tuning, refer to the `#define RNG_*` defines and the comments of the number generation function in `beatmap.c`.

#### Map fetching

On Windows, if no `-m` option is passed, `maniac` will attemt to read the beatmap from the window title, and idle while you're in the menus.

The beatmaps are read from the `DEFAULT_OSU_PATH` defined in `osu.h`, and it is assumed that the folder structure has not been tampered with (eg that Songs/ contains only folders, ...).

The default osu! path on Windows is `C:\Users\<username>\AppData\local\osu!\Songs`, and on Linux it's `/home/<username>/osufolder/Songs`. Since you will likely have gone through some hoops to get osu! running on Linux, it's probably easiest to just symlink `~/osufolder` to wherever you keep your osu! files. 

### Compilation

```bash
$ sudo apt-get install build-essential
$ git clone https://github.com/LW2904/maniac.git
$ cd maniac
$ ./build.sh
```

If you are on Windows, use [MinGW](http://www.mingw.org/) to compile.

### Scripts

Since it can be inconvenient to search for, and to always provide, the full path to osu! beatmap files `scripts/find.js` will search for beatmaps by a number of tags which will be matched against the individual maps names. The best match will be copied to the `./map.osu`.

Usage is as follows, and requires [NodeJS](https://nodejs.org/en/).

```bash
$ node scripts/find.js Chroma,Heaven
copied ..\..\AppData\Local\osu!\Songs\738673 Chroma - I\Chroma - I (Lude) [Heavenly].osu to ./map.osu
```

By default the script will search for folders in `~/AppData/Local/osu!/Songs/`, but you may provide an alternative path by setting the `songsPath` property in `scripts/settings.json`.

### TODO

- Improve humanization especially in regards to (in)accuracy spread (in osu! terms: unstable rate).
  - Partly done, the unstable rate is still rather high, but comparable to many other legit players.
- ~~Implement pattern scanning for Windows builds.~~
- Implement reading the current map/song name from memory.