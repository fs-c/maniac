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