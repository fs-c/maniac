#include "osu.h"

#include <stdio.h>
#include <string.h>

#define RNG_ROUNDS 80
#define RNG_BOUNDARY 0.5

/**
 * Returns a randomly generated number in the range of [0, range], while
 * attemting to constrain it outside of a bound(ary) given in percent (]0, 1[),
 * in a given number of rounds.
 */
int generate_number(int range, int rounds, float bound);

int parse_beatmap(char *file, hitpoint **points)
{
	FILE *stream;
	char line[MAX_LINE_LENGTH];

	if (!(stream = fopen(file, "r"))) {
		return 0;
	}

	int parsed = 0;		// Number of hitpoints parsed.
	int in_section = 0;	// Currently in the hitobjects section?
	while (fgets(line, sizeof(line), stream)) {
		if (!in_section && strstr(line, "[HitObjects]")) {
			in_section = 1;

			*points = malloc(sizeof(hitpoint));

			continue;
		} else if (!in_section) continue;

		hitpoint point;
		parse_beatmap_line(line, &point);

		*points = realloc(*points, ++parsed * sizeof(hitpoint));
		(*points)[parsed - 1] = point;
	}

	return parsed;
}

// Note that this function is not thread safe. (TODO?)
int parse_beatmap_line(char *line, hitpoint *point)
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

int parse_hitpoints(int count, hitpoint **points, action **actions)
{
	// Allocate enough memory for all actions at once.
	*actions = malloc((2 * count) * sizeof(action));

	hitpoint *cur_point;
	int num_actions = 0, i = 0;

	while (i < count) {
		cur_point = (*points) + i++;

		// Don't care about the order here.
		action *end = *actions + num_actions++;
		action *start = *actions + num_actions++;

		hitpoint_to_action(cur_point, start, end);
	}

	// TODO: Check if all memory was used and free() if applicable.

	return num_actions;
}

// TODO: This really shouldn't be here but it was causing weird errors.
const char COL_KEYS[] = { 'd', 'f', 'j', 'k' };

void hitpoint_to_action(hitpoint *point, action *start, action *end)
{
	end->time = point->end_time;
	start->time = point->start_time;

	end->down = 0;		// Keyup.
	start->down = 1;	// Keydown.

	const char key = COL_KEYS[point->column];

	end->key = key;
	start->key = key;
}

// TODO: Implement a more efficient sorting algorithm than selection sort.
int sort_actions(int total, action **actions)
{
	int min, i, j;
	action *act = *actions, tmp;

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
void humanize_hitpoints(int total, hitpoint **points, int level)
{
	if (!level) {
		return;
	}

	int i, offset;
	hitpoint *p = NULL;
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
	int rn;
	float minr = 0.5 - (bound / 2);
	float maxr = 0.5 + (bound / 2);

	rn = rand() % range;

	for (int i = 0; i < rounds; i++) {
		bool in = rn > (range * minr) && rn < (range * maxr);

		rn += (in ? (rand() % (int)(range * minr)) : 0)
			* (rn < (range * 0.5) ? -1 : 1);
	}

	return rn;
}