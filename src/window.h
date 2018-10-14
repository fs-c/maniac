#ifndef WINDOW_H
#define WINDOW_H

#include "common.h"

#include <stdlib.h>

#ifdef ON_WINDOWS
  extern HWND game_window;
  extern HANDLE game_proc;
#endif /* ON_WINDOWS */

#ifdef ON_LINUX
  extern Display *display;
  extern Window game_window;
#endif /* ON_LINUX */

/**
 * Finds the main window of a process with a given ID and stores the
 * OS-appropiate handle to it in *out_window.
 */
int find_window(unsigned long process_id, void **out_window);

/**
 * Fetches the title of the game window. *title is expected to point to a
 * region of memory that is writable.
 */
hot size_t get_window_title(char **title, int title_len);

#endif /* WINDOW_H */
