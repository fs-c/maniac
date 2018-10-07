#ifndef OSU_H
#define OSU_H

#include "common.h"

#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <inttypes.h>
#include <sys/types.h>

#ifdef ON_WINDOWS
  #include <windows.h>

  extern HWND game_window;
  extern HANDLE game_proc;
#endif /* ON_WINDOWS */

#ifdef ON_LINUX
  #include <X11/Xlib.h>
  #include <X11/extensions/XTest.h>

  extern Display *display;
  extern Window game_window;
#endif /* ON_LINUX */

extern void *time_address;
extern pid_t game_proc_id;

#include "beatmap.h"

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