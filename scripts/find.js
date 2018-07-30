const { homedir } = require('os');
const { join, relative } = require('path');
const { readdirSync, readFileSync, writeFileSync, existsSync } = require('fs');

const configPath = join(__dirname, 'config.json');
const config = existsSync(configPath) ? (
    JSON.parse(readFileSync(configPath, 'utf8'))
) : { notFound: true };

if (process.argv.length < 2) {
    return console.error('error: no arguments provided');
}

const searchTags = process.argv[2].split(',');
const songsPath = config.songsPath ? (
    config.songsPath.replace('~', homedir())
) : relative(process.cwd(),join(homedir(), '/AppData/Local/osu!/Songs'));

const allTrue = (arr) => {
    return arr.reduce((ac, cu) => !cu ? ac = false : ac, true);
}

if (!(existsSync(songsPath))) {
    console.error('error: %s does not exist', songsPath);
}

const beatmap = readdirSync(songsPath)
    // Get an array of arrays for all filenames of all song folders.
    .map((song) => readdirSync(join(songsPath, song))
        .map((el) => join(songsPath, song, el)))
    // Reduce array of arrays to single array, we don't care about song folders.
    .reduce((ac, cu) => { ac.push(...cu); return ac; }, [])
    // Filter out songs which don't match all tags, choose the first one.
    .filter((el) => allTrue(searchTags.map((tg) => el.includes(tg))))[0];

if (!beatmap) {
    return console.error('error: no matching beatmap found');
}

writeFileSync('map.osu', readFileSync(beatmap, 'utf8'), 'utf8');

console.log(`copied ${beatmap} to ./map.osu`);  