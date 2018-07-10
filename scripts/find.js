const { join, relative } = require('path');
const { readdirSync, readFileSync, writeFileSync, existsSync } = require('fs');

const config = existsSync('config.json') ? (
	JSON.parse(readFileSync('config.json', 'utf8'))
) : {};

const searchTags = process.argv[2].split(',');
const songsPath = config.songsPath || relative(
	process.cwd(),
	join(require('os').homedir(), '/AppData/Local/osu!/Songs'),
);

const beatmap = readdirSync(songsPath)
    .map((song) => readdirSync(join(songsPath, song))
        .map((el) => join(songsPath, song, el)))
    .reduce((ac, cu) => { ac.push(...cu); return ac; }, [])
    .filter((el) => searchTags.map((tg) => el.includes(tg))
        .reduce((ac, cu) => !cu ? ac = cu : ac, true))[0];

writeFileSync('map.osu', readFileSync(beatmap, 'utf8'), 'utf8');

console.log(`copied ${beatmap} to ./map.osu`);
