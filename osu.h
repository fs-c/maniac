#ifndef OSU_H
#define OSU_H 

#define NUM_COLS 4
#define COL_WIDTH 512

#define MAX_LINE_LENGTH 1024

#define TAPTIME_MS 12

#define TIME_ADDRESS 0x36e59ec

#include <stdlib.h>
#include <sys/types.h>

struct hitpoint {
	int column;
	int end_time;
	int start_time;
};

typedef struct hitpoint hitpoint;

struct action {
	int time;
	int down;
	int code;
	char key;
};

typedef struct action action;

/**
 * Parse a beatmap file (*.osu) into an array of hitpoint structs pointed to by 
 * **points.
 * Returns the number of points parsed and stored.
 */
int parse_beatmap(char *file, hitpoint **points);

/**
 * Parses a raw beatmap line into a hitpoint struct pointed to by *point.
 * Returns the number of tokens read.
 */
int parse_beatmap_line(char *line, hitpoint *point);

/**
 * Parses a total of count hitmapts from **points into **actions.
 * Returns the number of actions parsed and stored, which should be count * 2.
 */
int parse_hitpoints(int count, hitpoint **points, action **actions);

/**
 * Populates *start and *end with data from hitpoint *point.
 */
void hitpoint_to_action(hitpoint *point, action *start, action *end);

/**
 * Sort the array of actions given through **actions by time.
 * Returns nonzero on failure.
 */
int sort_actions(int count, action **actions);

/**
 * Gets and returns the runtime of the currently playing song, internally
 * referred to as `maptime`.
 */
int32_t get_maptime(pid_t pid);

#endif /* OSU_H */