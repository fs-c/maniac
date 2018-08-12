#include "osu.h"

#include <stdio.h>
#include <string.h>
#include <dirent.h>

#define RNG_ROUNDS 50
#define RNG_BOUNDARY 0.5

/**
 * Parses a raw beatmap line into a hitpoint struct pointed to by *point.
 * Returns the number of tokens read.
 */
int parse_beatmap_line(char *line, struct hitpoint *point);

/**
 * Populates *start and *end with data from hitpoint *point.
 */
void hitpoint_to_action(struct hitpoint *point, struct action *start,
	struct action *end);

/**
 * Returns a randomly generated number in the range of [0, range], while
 * attemting to constrain it outside of a bound(ary) given in percent (]0, 1[),
 * in a given number of rounds.
 */
int generate_number(int range, int rounds, float bound);

const char col_keys[] = { 'd', 'f', 'j', 'k' };

int find_beatmap(char *base, char *partial, char **map)
{
	DIR *dp;
	struct dirent *ep;

	if (!(dp = opendir(base))) {
		printf("couldn't open directory %s\n", base);
		return 1;
	}

	// Iterate over all files (in this case only folders) of the osu
	// beatmap directory.
	char *folder = NULL;
	while ((ep = readdir(dp)) != NULL) {
		char *dir = ep->d_name;
		// partial is missing map ID, allow for large-ish discrepancy.
		if (strlen(dir) * 0.5 < partial_match(dir, partial)) {
			folder = dir;
			break;
		}
	}

	if (!folder) {
		printf("couldn't find beatmap folder name (%s)\n", partial);
		return 2;
	}

	// Absolute path to the folder of our beatmap.
	char absolute[256];
	strcpy(absolute, base);
	strcpy(absolute + strlen(absolute), folder);
	strcpy(absolute + strlen(absolute), "\\");

	if (!(dp = opendir(absolute))) {
		printf("couldn't open directory %s\n", absolute);
		return 1;
	}

	// Iterate over all files in the beatmap folder, ...
	char *beatmap = NULL;
	while ((ep = readdir(dp)) != NULL) {
		char *file = ep->d_name;
		// ... , and check which one matches the one we are looking for.
		// Allow for discrepancy since author note is omitted in our
		// partial.
		if (strlen(partial) * 0.6 < partial_match(file, partial)) {
			beatmap = file;
			break;
		}
	}

	if (!beatmap) {
		printf("couldn't find beatmap file (%s)\n", partial);
		return 2;
	}

	// This is now the absolute path to our beatmap.
	strcpy(absolute + strlen(absolute), beatmap);

	*map = absolute;

	return 0;
}

int parse_beatmap(char *file, struct hitpoint **points)
{
	FILE *stream;
	char line[MAX_LINE_LENGTH];

	if (!(stream = fopen(file, "r"))) {
		return 0;
	}

	int parsed = 0;		// Number of hitpoints parsed.
	int in_section = 0;	// Currently in the hitobjects section?
	size_t hp_size = sizeof(struct hitpoint);
	while (fgets(line, sizeof(line), stream)) {
		if (!in_section && strstr(line, "[HitObjects]")) {
			in_section = 1;

			*points = malloc(hp_size);

			continue;
		} else if (!in_section) continue;

		struct hitpoint point;
		parse_beatmap_line(line, &point);

		*points = realloc(*points, ++parsed * hp_size);
		(*points)[parsed - 1] = point;
	}

	return parsed;
}

// Note that this function is not thread safe. (TODO?)
int parse_beatmap_line(char *line, struct hitpoint *point)
{
	int end_time, secval = 0;
	char *token, *ln = strdup(line), i = 0;

	// Line is expexted to follow the following format:
	// x, y, time, type, hitSound, extras (= a:b:c:d:)
	token = strtok(ln, ",");
	while (token != NULL) {
		secval = strtol(token, NULL, 10);

		switch (i++) {
		// X
		case 0: point->column = secval / (COL_WIDTH / NUM_COLS);
			break;
		// Start time
		case 2: point->start_time = secval;
			break;
		// Extra string, first element is either 0 or end time
		case 5:
			end_time = strtol(strtok(token, ":"), NULL, 10);

			// If end is 0 this is not a hold note, just tap.
			point->end_time = end_time ? end_time :
				point->start_time + TAPTIME_MS;

			break;
		}

		token = strtok(NULL, ",");
	}

	free(ln);
	free(token);

	return i;
}

int parse_hitpoints(int count, struct hitpoint **points,
	struct action **actions)
{
	// Allocate enough memory for all actions at once.
	*actions = malloc((2 * count) * sizeof(struct action));

	int num_actions = 0, i = 0;
	struct hitpoint *cur_point;

	while (i < count) {
		cur_point = (*points) + i++;

		// Don't care about the order here.
		struct action *end = *actions + num_actions++;
		struct action *start = *actions + num_actions++;

		hitpoint_to_action(cur_point, start, end);
	}

	// TODO: Check if all memory was used and free() if applicable.

	return num_actions;
}

void hitpoint_to_action(struct hitpoint *point, struct action *start,
	struct action *end)
{
	end->time = point->end_time;
	start->time = point->start_time;

	end->down = 0;		// Keyup.
	start->down = 1;	// Keydown.

	const char key = col_keys[point->column];

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

int generate_number(int range, int rounds, float bound)
{
	int rn = rand() % range;

	// Min and max percentage of the range we will use with our constraint.
	float minr = 0.5 - (bound / 2);
	float maxr = 0.5 + (bound / 2);

	for (int i = 0; i < rounds; i++) {
		int in = rn > (range * minr) && rn < (range * maxr);

		rn += (in ? (rand() % (int)(range * minr)) : 0)
			* (rn < (range * 0.5) ? -1 : 1);
	}

	return rn;
}