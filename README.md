Cheating in osu!mania.

Compile with `-lX11` and `-lXtst` flags. This depends on the X Window System and requires a Linux kernel version >= 3.2.

```bash
$ osu -m <path to .osu beatmap file> -p <PID of osu! process>
```

For convenience, this repository also contains `scripts/find.js`, which can find and copy beatmap files to the current directory. It searches for beatmaps given a number of keywords and selects the best match.

```bash
$ node scripts/find.js Chroma,Heaven
copied /.../Songs/738673 Chroma - I/Chroma - I (Lude) [Heavenly].osu to ./map.osu
```

This is done seperately mostly because I don't feel like it belongs in the main application or that C is the right tool for the job.