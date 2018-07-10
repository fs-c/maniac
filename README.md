<h2 align="center">maniac</h2>

<p align="center">Cheating in osu!mania.</p>

This is a very simple external cheat for the game [osu!](https://osu.ppy.sh/), more specifically for the [osu!mania](https://osu.ppy.sh/help/wiki/Game_Modes/osu!mania) gamemode (where this project also got its name from). It attempts to be a __simple__, __lightweight__ and __portable__ application, and it can be compiled on many Linux distributions (where it depends on the X Window System) and Windows versions.

```
  Usage: maniac [options]

  Options:

    -m	path to .osu beatmap file
    -p  PID of osu! process (optional on Windows)
    -a  address of the playback time in memory (optional on Linux)
```

### Compilation

```bash
$ sudo apt-get install build-essential
$ git clone https://github.com/LW2904/maniac.git
$ cd maniac
$ ./build.sh
```

If you are on Windows, use [MinGW](http://www.mingw.org/) to compile.