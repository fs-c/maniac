const { homedir } = require('os');
const { join, relative } = require('path');
const { readdirSync, readFileSync, writeFileSync, existsSync } = require('fs');

const configPath = join(__dirname, 'config.json');
const config = existsSync(configPath) ? (
    JSON.parse(readFileSync(configPath, 'utf8'))
) : { notFound: true };

const searchTags = process.argv[2].split(',');
const songsPath = config.songsPath ? (
    config.songsPath.replace('~', homedir())
) : relative(process.cwd(),join(homedir(), '/AppData/Local/osu!/Songs'));

const allTrue = (arr) => {
    return arr.reduce((ac, cu) => !cu ? ac = false : ac, true);
}

const beatmap = readdirSync(songsPath)
    .map((song) => readdirSync(join(songsPath, song))
        .map((el) => join(songsPath, song, el)))
    .reduce((ac, cu) => { ac.push(...cu); return ac; }, [])
    .filter((el) => allTrue(searchTags.map((tg) => el.includes(tg))))[0];

if (!beatmap) {
    return console.error('no matching beatmap found');
}

writeFileSync('map.osu', readFileSync(beatmap, 'utf8'), 'utf8');

console.log(`copied ${beatmap} to ./map.osu`);
