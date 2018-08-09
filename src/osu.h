#ifndef OSU_H
#define OSU_H

#include <stdlib.h>
#include <stdbool.h>
#include <inttypes.h>
#include <sys/types.h>

#ifdef _WIN32
  #define ON_WINDOWS

  #define HOME_ENV "USERPROFILE"
  #define SIGNATURE "\xDB\x5D\xE8\x8B\x45\xE8\xA3"
  #define DEFAULT_OSU_PATH "\\AppData\\Local\\osu!\\Songs\\"

  #include <windows.h>
  
  extern HWND game_window;
  extern HANDLE game_proc;
#endif /* _WIN32 */

#ifdef __linux__
  #define ON_LINUX

  #define HOME_ENV "HOME"
  #define LINUX_TIME_ADDRESS 0x36e59ec

  #include <X11/Xlib.h>
  #include <X11/extensions/XTest.h>

  extern Display *display;
#endif /* __linux__ */

#define NUM_COLS 4
#define COL_WIDTH 512

#define MAX_LINE_LENGTH 1024

#define TAPTIME_MS 15

extern void *time_address;
extern pid_t game_proc_id;

struct hitpoint {
	int column;
	int end_time;
	int start_time;
};

typedef struct hitpoint hitpoint;

struct action {
	int time;
	int down;
	char key;
};

typedef struct action action;

/**
 * Searches for a beatmap in DEFAULT_OSU_PATH + base given a part of the
 * file name, and stores the absolute path to it in **map.
 * Returns zero on success (1 == couldn't open dir, 2 == couldn't find file).
 */
int find_beatmap(char *base, char *partial, char **map);

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
int32_t get_maptime();

/**
 * Sends a keypress to the currently active window.
 */
void send_keypress(char key, int down);

/**
 * Performs operating system specific setup.
 * Windows: Open handle to game process.
 * Linux: Open X11 display. 
 */
void do_setup();

/**
 * Windows only:
 * Fetches the title of the game window. Memory for the string title will
 * be allocated and it's guaranteed to have less than 128 characters.
 */
int get_window_title(char **title);

/**
 * Windows only:
 * Returns the process id of the given process or zero if it was not found.
 */
unsigned long get_process_id(const char *name);

/**
 * Add a randomized delay of magnitude level to the hitpoints.
 */
void humanize_hitpoints(int total, hitpoint **points, int level);

/**
 * Returns the address of the playback time in the address space of the game
 * process.
 * Windows: Scans memory for the address using a signature.
 * Linux: Returns static address (LINUX_TIME_ADDRESS).
 */
void *get_time_address();

/**
 * Given a base, returns the number of concurrent characters which match
 * partial.
 */
int partial_match(char *base, char *partial);

#endif /* OSU_H */