<h2 align="center">maniac</h2>

<p align="center">
  Simple external cheat for <a href="https://osu.ppy.sh/help/wiki/Game_Modes/osu!mania">osu!mania</a>.<br>
</p>

If you have a question or have encountered a bug please feel free to [open an issue
](https://github.com/fs-c/maniac/issues), I usually respond quickly. There's also a
 [Discord server](https://discord.gg/aARF7KbTuj), if that's your thing.

- [Usage](#usage)
  - [Options](#options)
- [Building](#building)
- [Thanks](#thanks)

## Usage

1. Download the latest build from the [releases tab](https://github.com/LW2904/maniac/releases).
2. Start osu, start playing any beatmap for a couple of seconds and then either exit or
 pause. (You only have to do this once for every time you start osu, the map or mode is
 irrelevant.)
3. Open a terminal and run maniac (e.g. by dragging-and-dropping the executable onto `cmd.exe`).

Maniac will now automatically play any beatmap you open in osu.

### Options

Maniac accepts options which have to be passed through the command line when running the executable.

```
$ ./maniac.exe -h

Usage: maniac [options]

Options:
    -h / --help                Show this message and exit.
    -r / --randomization [a,b] Add milliseconds in the range [a,b] to all k
                               ey presses. If only `a` is provided, `b` imp
                               licitly equals `-a`. (default: 0,0, implicit
                               : -5,5)
    -u / --humanization [a]    For every key press, an offset calculated th
                               rough (density at that point * (a / 100)) is
                                added to the time. (default: 0, implicit: 1
                               00)
    -c / --compensation [a]    Add static offset in milliseconds to every a
                               ction to compensate for the time it takes ma
                               niac to send a keypress. (default: -20)
    -m / --mirror-mod          Mirror the keys pressed (i.e.: mirror mod su
                               pport). (default: false, implicit: true)

    Note that all options have both a default and an implicit value. The di
    fference is best illustrated through an example:

    command                       humanization
    $ ./maniac                    0
    $ ./maniac --humanization     100
    $ ./maniac --humanization 50  50
```

## Building

_See also the CI build steps in `.github/workflows`._

```
# Get the code
git clone https://github.com/fs-c/maniac.git

# Building out-of-source is preferred
mkdir build
cd build

# Generate build files from the CMakeLists file in the parent directory
# Other generators would probably work too, but this is the supported one
cmake .. -G "Visual Studio 16 2019" -A Win32
# OR, if you want a release build
cmake .. -G "Visual Studio 16 2019" -A Win32 -DCMAKE_BUILD_TYPE="Release"

# Run the build!
cmake --build . -- /p:Configuration=Debug
# OR, if you specified "Release" earlier
cmake --build . -- /p:Configuration=Release
```

`Release` builds are optimized and don't contain debug information, unlike `Debug
` builds which also have significantly more verbose logging.

## Thanks
 
- n0b453c0d3r on UC and [mrflashstudio](https://github.com/mrflashstudio) for
 providing up to date signatures and offsets
- [Asi Shavit](https://github.com/adishavit) for the [Argh](https://github.com/adishavit/argh) library
- to everyone who reported bugs and provided feedback
