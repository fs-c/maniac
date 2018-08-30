#ifndef OSU_H
#define OSU_H

#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <inttypes.h>
#include <sys/types.h>

#ifdef _WIN32
  #define ON_WINDOWS

  #define HOME_ENV "USERPROFILE"
  #define SIGNATURE "\xDB\x5D\xE8\x8B\x45\xE8\xA3"
 
  #define SEPERATOR '\\'
  #define DEFAULT_OSU_PATH "\\AppData\\Local\\osu!\\Songs\\"

  #define KEY_RETURN 0xFF0D
  #define KEY_ESCAPE 0xFF1B

  #include <windows.h>
  
  extern HWND game_window;
  extern HANDLE game_proc;
#endif /* _WIN32 */

#ifdef __linux__
  #define ON_LINUX

  #define HOME_ENV "HOME"
  #define LINUX_TIME_ADDRESS 0x36e5bf4
  // Probably incorrect.
  #define SIGNATURE "\xDB\x5D\xE8\x8B\x45\xE8\xA3"  

  #define SEPERATOR '/'
  #define DEFAULT_OSU_PATH "/osufolder/Songs/"

  #define KEY_RETURN 0x0D
  #define KEY_ESCAPE 0x1B

  #include <X11/Xlib.h>
  #include <X11/extensions/XTest.h>

  extern Display *display;
  extern Window game_window;
#endif /* __linux__ */

#ifdef DEBUG

  #define debug(...)\
      printf("[debug] [%s:%s] ", __FILE__, __func__);\
      printf(__VA_ARGS__);\
      putchar('\n');\

#elif !DEBUG

  #define debug(...)\
      0;\

#endif

#define COLS_WIDTH 512

#define MAX_LINE_LENGTH 1024

#define TAPTIME_MS 15

#define __hot __attribute__((__hot__))

extern void *time_address;
extern pid_t game_proc_id;

struct beatmap_meta {
	int set_id;
	int map_id;
	int columns;
	char title[256];
	char artist[256];
	char version[256];
};

struct hitpoint {
	int column;
	int end_time;
	int start_time;
};

struct action {
	int down;
	char key;
	int time;
};

/**
 * Searches for a beatmap in DEFAULT_OSU_PATH + base given a part of the
 * file name, and stores the absolute path to it in *map.
 * Returns the length of the path stored, or zero on failure.
 */
int find_beatmap(char *base, char *partial, char **map);

/**
 * Parse a beatmap file (*.osu) into an array of hitpoint structs pointed to by 
 * **points and a metadata struct.
 * Returns the number of points parsed and stored.
 */
int parse_beatmap(char *file, struct hitpoint **points,
	struct beatmap_meta **meta);

/**
 * Parses a total of count hitmapts from **points into **actions.
 * Returns the number of actions parsed and stored, which should be count * 2.
 */
int parse_hitpoints(int count, int columns, struct hitpoint **points,
	struct action **actions);

/**
 * Sort the array of actions given through **actions by time.
 * Returns nonzero on failure.
 */
int sort_actions(int count, struct action **actions);

/**
 * Gets and returns the runtime of the currently playing song, internally
 * referred to as `maptime`.
 */
__hot int32_t get_maptime();

/**
 * Copies game memory starting at `base` for `size` bytes into `buffer`.
 * Internally, this is a wrapper for _read_game_memory, with argument checking.
 * Returns number of bytes read and copied.
 */
ssize_t read_game_memory(void *base, void *buffer, size_t size);

/**
 * Sends a keypress to the currently active window.
 */
__hot void send_keypress(int key, int down);

/**
 * Convenience function to send a keydown and keyup event with a 10ms interval.
 */
void tap_key(int key);

/**
 * Performs operating system specific setup.
 * Windows: Open handle to game process.
 * Linux: Open X11 display. 
 */
void do_setup();

/**
 * Fetches the title of the game window. *title is expected to point to a
 * region of memory that is writable.
 */
__hot int get_window_title(char **title, int title_len);

/**
 * Returns the process id of the given process or zero if it was not found.
 */
unsigned long get_process_id(const char *name);

/**
 * Add a randomized delay of magnitude level to the hitpoints.
 */
void humanize_hitpoints(int total, struct hitpoint **points, int level);

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

/**
 * Store the address of the beginning of the last segment of `path` in `last`.
 * If no seperator was found, `path` is equal to `*last`.
 */
void path_get_last(char *path, char **last);

/**
 * Attempts to read an environmental variable named `name` and returns it
 * through *out_var.
 * Returns the length of the variable stored or zero on failure.
 */
int get_env_var(char *name, char **out_var);

/**
 * Fetches the absolute path to the main osu! directory, using HOME_ENV
 * and DEFAULT_OSU_PATH, and returns it through *out_path.
 * Returns the length of the path stored or zero on failure.
 */
int get_osu_path(char **out_path);

/**
 * Searches for a file or folder in `base`, matching all directory entries
 * against `partial`. The best match is returned through *out_file.
 * Returns the length of the matched path or zero on failure.
 */
int find_partial_file(char *base, char *partial, char **out_file);

/**
 * Returns a randomly generated number in the range of [0, range], while
 * attemting to constrain it outside of a bound(ary) given in percent (]0, 1[),
 * in a given number of rounds.
 */
int generate_number(int range, int rounds, float bound);

/**
 * Searches for a signature (sequence of bytes) in the process, returning the
 * address of the end (!) of the first occurence.
 */
void *find_pattern(const unsigned char *signature, unsigned int sig_len);

/**
 * Finds the main window of a process with a given ID and stores the
 * OS-appropiate handle to it in *out_window.
 */
int find_window(unsigned long process_id, void **out_window);

#endif /* OSU_H */