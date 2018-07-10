const { join } = require('path');
const { homedir } = require('os');
const { readdirSync, readFileSync, writeFileSync } = require('fs');

const searchTags = process.argv[2].split(',');
const songsPath = '~/osufolder/Songs/'.replace('~', homedir());
const beatmap = readdirSync(songsPath)
    .map((song) => readdirSync(join(songsPath, song))
        .map((el) => join(songsPath, song, el)))
    .reduce((ac, cu) => { ac.push(...cu); return ac; }, [])
    .filter((el) => searchTags.map((tg) => el.includes(tg))
        .reduce((ac, cu) => !cu ? ac = cu : ac, true))[0];

writeFileSync('map.osu', readFileSync(beatmap, 'utf8'), 'utf8');