#include "beatmap.h"

/**
 * Parses a raw beatmap line into a beatmap_meta struct pointed to by *meta.
 * Returns the number of tokens read.
 */
static int parse_beatmap_line(char *line, struct beatmap_meta *meta);

/**
 * Parses a key:value set into *meta.
 */
static void parse_beatmap_token(char *key, char *value,
	struct beatmap_meta *meta);

/**
 * Parses a raw hitobject line into a hitpoint struct pointed to by *point.
 * Returns the number of tokens read.
 */
static int parse_hitobject_line(char *line, int columns,
	struct hitpoint *point);

/**
 * Populates *start and *end with data from hitpoint *point.
 */
static void hitpoint_to_action(char *keys, struct hitpoint *point,
	struct action *start, struct action *end);

/**
 * Returns a randomly generated number in the range of [0, range], while
 * attemting to constrain it outside of a bound(ary) given in percent (]0, 1[),
 * in a given number of rounds.
 */
static int generate_number(int range, int rounds, double bound);

/**
 * Searches for a file or folder in `base`, matching all directory entries
 * against `partial`. The best match is returned through *out_file.
 * Returns the length of the matched path or zero on failure.
 */
static size_t find_partial_file(char *base, char *partial, char **out_file);

/**
 * Given a base, returns the number of concurrent characters which match
 * partial.
 */
static int partial_match(char *base, char *partial);

const char col_keys[9] = "asdfjkl[";

size_t find_beatmap(char *base, char *partial, char **map)
{
	if (!base || !partial || !map) {
		debug("received null pointer");
		return 0;
	}

	char *folder = NULL;
	size_t folder_len = 0;

	if (!(folder_len = find_partial_file(base, partial, &folder))) {
		debug("couldn't find folder (%s)", partial);
		return 0;
	}

	size_t map_len = 256, base_len = strlen(base);
	*map = malloc(map_len);

	// Absolute path to our base.
	strcpy(*map, base);
	// A.p. to the beatmap folder.
	strcpy(*map + base_len, folder);
	// Add a trailing seperator and terminating zero.
	strcpy(*map + base_len + folder_len, (char[2]){(char)SEPERATOR, '\0'});

	free(folder);

	char *beatmap = NULL;
	size_t beatmap_len = 0;

	if (!(beatmap_len = find_partial_file(*map, partial, &beatmap))) {
		debug("couldn't find beatmap in %s", *map);
		return 0;
	}

	// This is now the absolute path to our beatmap.
	strcpy(*map + base_len + folder_len + 1, beatmap);

	free(beatmap);

	map_len = base_len + folder_len + 1 + beatmap_len;

	// Change block size to fit what we really need.
	*map = realloc(*map, map_len + 1);

	// Verify that the file we found is a beatmap.
	if (strcmp(*map + map_len - 4, ".osu") != 0) {
		debug("%s is not a beatmap", *map);
		
		free(*map);

		return 0;
	}

	return map_len;
}

// TODO: Inefficient as it calls realloc() for every parsed line. Allocate
// 	 memory in chunks and copy it to adequately sized buffer once done.
size_t parse_beatmap(char *file, struct hitpoint **points,
	struct beatmap_meta **meta)
{
	if (!points || !meta || !file) {
		debug("received null pointer");
		return 0;
	}

	FILE *stream;

	if (!(stream = fopen(file, "r"))) {
		debug("couldn't open file %s", file);
		return 0;
	}

	*points = NULL;
	*meta = calloc(1, sizeof(struct beatmap_meta));

	const size_t line_len = 256;
	char *line = malloc(line_len);

	struct hitpoint cur_point;
	size_t hp_size = sizeof(struct hitpoint), num_parsed = 0;

	char cur_section[128];

	// TODO: This loop body is all kinds of messed up.
	while (fgets(line, (int)line_len, stream)) {
		if (!(strcmp(cur_section, "[Metadata]\n"))) {
			parse_beatmap_line(line, *meta);
		} else if (!(strcmp(cur_section, "[Difficulty]\n"))) {
			parse_beatmap_line(line, *meta);
		} else if (!(strcmp(cur_section, "[HitObjects]\n"))) {
			parse_hitobject_line(line, meta[0]->columns,
				&cur_point);

			*points = realloc(*points, ++num_parsed * hp_size);
			points[0][num_parsed - 1] = cur_point;
		}
	
		if (line[0] == '[')
			strcpy(cur_section, line);
	}

	free(line);
	fclose(stream);

	return num_parsed;
}

// TODO: This function is not thread safe.
static int parse_beatmap_line(char *line, struct beatmap_meta *meta)
{
	int i = 0;
	// strtok() modfies its arguments, work with a copy.
	char *ln = strdup(line);
	char *token = NULL, *key = NULL, *value = NULL;

	// Metadata lines come in key:value pairs.
	token = strtok(ln, ":");
	while (token != NULL) {
		switch (i++) {
		case 0: key = strdup(token);
			break;
		case 1: value = strdup(token);

			parse_beatmap_token(key, value, meta);

			break;
		}

		token = strtok(NULL, ":");
	}

	free(ln);
	free(key);
	free(token);
	free(value);

	return i;
}

static void parse_beatmap_token(char *key, char *value,
	struct beatmap_meta *meta)
{
	if (!key || !value || !meta) {
		debug("received null pointer");
		return;
	}

	// Always ignore last two characters since .osu files are CRLF by
	// default.
	if (!(strcmp(key, "Title"))) {
		value[strlen(value) - 2] = '\0';

		strcpy(meta->title, value);
	} else if (!(strcmp(key, "Artist"))) {
		value[strlen(value) - 2] = '\0';

		strcpy(meta->artist, value);
	} else if (!(strcmp(key, "Version"))) {
		value[strlen(value) - 2] = '\0';

		strcpy(meta->version, value);
	} else if (!(strcmp(key, "BeatmapID"))) {
		meta->map_id = atoi(value);
	} else if (!(strcmp(key, "BeatmapSetID"))) {
		meta->set_id = atoi(value);
	} else if (!(strcmp(key, "CircleSize"))) {
		meta->columns = atoi(value);
	}
}

// TODO: This function is not thread safe.
static int parse_hitobject_line(char *line, int columns, struct hitpoint *point)
{
	int secval = 0, end_time = 0, slider = 0, i = 0;
	char *ln = strdup(line), *token = NULL;

	// Line is expected to follow the following format:
	// x, y, time, type, hitSound, extras (= a:b:c:d:)
	token = strtok(ln, ",");
	while (token != NULL) {
		secval = (int)strtol(token, NULL, 10);

		switch (i++) {
		// X
		case 0: point->column = secval / (COLS_WIDTH / columns);
			break;
		// Start time
		case 2: point->start_time = secval;
			break;
		// Type
		case 3: slider = secval & TYPE_SLIDER;
			break;
		// Extra string, first element is either 0 or end time
		case 5:
			end_time = (int)strtol(strtok(token, ":"), NULL, 10);

			point->end_time = slider ? end_time :
				point->start_time + TAPTIME_MS;

			break;
		}

		token = strtok(NULL, ",");
	}

	free(ln);
	free(token);

	return i;
}

int parse_hitpoints(size_t count, size_t columns, struct hitpoint **points,
	struct action **actions)
{
	// Allocate enough memory for all actions at once.
	*actions = malloc((2 * count) * sizeof(struct action));

	struct hitpoint *cur_point;
	size_t num_actions = 0, i = 0;

	char *key_subset = malloc(columns + 1);
	key_subset[columns] = '\0';

	const size_t col_size = sizeof(col_keys) - 1;
	const size_t subset_offset = (col_size / 2) - (columns / 2);

	memmove(key_subset, col_keys + subset_offset,
		col_size - (subset_offset * 2));

	if (columns % 2) {
		memmove(key_subset + columns / 2 + 1, key_subset + columns / 2,
			columns / 2);
		key_subset[columns / 2] = ' ';
	}

	while (i < count) {
		cur_point = (*points) + i++;

		// Don't care about the order here.
		struct action *end = *actions + num_actions++;
		struct action *start = *actions + num_actions++;

		hitpoint_to_action(key_subset, cur_point, start, end);
	}

	free(key_subset);

	// TODO: Check if all *actions memory was used and free() if applicable.

	return num_actions;
}

static void hitpoint_to_action(char *keys, struct hitpoint *point,
	struct action *start, struct action *end)
{
	end->time = point->end_time;
	start->time = point->start_time;

	end->down = 0;		// Keyup.
	start->down = 1;	// Keydown.

	char key = keys[point->column];

	end->key = key;
	start->key = key;
}

// TODO: Implement a more efficient sorting algorithm than selection sort.
int sort_actions(int total, struct action **actions)
{
	int min, i, j;
	struct action *act = *actions, tmp;

	// For every element but the last, which will end up to be the biggest.
	for (i = 0; i < (total - 1); i++) {
		min = i;

		// In the subarray starting at a[j]...
		for (j = i; j < total; j++)
			// ...find the smallest element.
			if ((act + j)->time < (act + min)->time) min = j;

		// Swap current element with the smallest element of subarray.
		tmp = act[i];
		act[i] = act[min];
		act[min] = tmp;
	}

	return i - total + 1;
}

// TODO: This function is retarded, fix it and add actual humanization.
void humanize_hitpoints(int total, struct hitpoint **points, int level)
{
	if (!level) {
		return;
	}

	int i, offset;
	struct hitpoint *p = NULL;
	for (i = 0; i < total; i++) {
		p = *points + i;

		// [0, level]
		offset = generate_number(level, RNG_ROUNDS, RNG_BOUNDARY);
		
		// [-(level / 2), (level / 2)]
		offset -= (level / 2);

		p->end_time += offset;
		p->start_time += offset;
	}
}

static int generate_number(int range, int rounds, double bound)
{
	int rn = rand() % range;

	// Min and max percentage of the range we will use with our constraint.
	double minr = 0.5 - (bound / 2);
	double maxr = 0.5 + (bound / 2);

	for (int i = 0; i < rounds; i++) {
		int in = rn > (range * minr) && rn < (range * maxr);

		rn += (in ? (rand() % (int)(range * minr)) : 0)
			* (rn < (range * 0.5) ? -1 : 1);
	}

	return rn;
}

static size_t find_partial_file(char *base, char *partial, char **out_file)
{
	if (!base || !partial || !out_file) {
		debug("received null pointer");
		return 0;
	}

	DIR *dp;
	struct dirent *ep;

	if (!(dp = opendir(base))) {
		debug("couldn't open directory %s", base);
		return 0;
	}

	const int file_len = 256;
	*out_file = malloc(file_len);

	int best_match = 0;

	while((ep = readdir(dp))) {
		char *name = ep->d_name;
		int score = partial_match(name, partial);

		if (score > best_match) {
			best_match = score;

			strcpy(*out_file, name);
		}
	}

	closedir(dp);

	return strlen(*out_file);
}

// TODO: I'm certain there's a more elegant way to go about this.
static int partial_match(char *base, char *partial)
{
	int i = 0;
	int m = 0;
	while (*base) {
		char c = partial[i];
		if (c == '.') {
			i++;
			continue;
		}

		if (*base++ == c) {
			i++;
			m++;
		}
	}

	return m;
}
