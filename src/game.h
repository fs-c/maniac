#ifndef GAME_H
#define GAME_H

#include "common.h"

#include <time.h>
#include <string.h>
#include <stdarg.h>
#include <dirent.h>

#ifdef ON_LINUX
Display *display;
Window game_window;
#endif /* ON_LINUX */

#ifdef ON_WINDOWS

  #include <tlhelp32.h>

HWND game_window;
HANDLE game_proc;
#endif /* ON_WINDOWS */

void *time_address;
pid_t game_proc_id;

/**
 * Performs operating system specific setup.
 * Windows: Open handle to game process.
 * Linux: Open X11 display. 
 */
void do_setup(void);

/**
 * Sends a keypress to the currently active window.
 */
hot void send_keypress(int key, int down);

/**
 * Convenience function to send a keydown and keyup event with a 10ms interval.
 */
void tap_key(int key);

/**
 * Fetches the absolute path to the main osu! directory, using HOME_ENV
 * and DEFAULT_OSU_PATH, and returns it through *out_path.
 * Returns the length of the path stored or zero on failure.
 */
size_t get_osu_path(char **out_path);

#endif /* GAME_H */
