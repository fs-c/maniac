#ifndef COMMON_H
#define COMMON_H

#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <inttypes.h>
#include <sys/types.h>

#define KEY_RETURN 0xFF0D
#define KEY_ESCAPE 0xFF1B

#ifdef _WIN32
  #define ON_WINDOWS

  #include "Windows.h"

  #define HOME_ENV "USERPROFILE"
  #define SIGNATURE "\xDB\x5D\xE8\x8B\x45\xE8\xA3"
 
  #define SEPERATOR '\\'
  #define DEFAULT_OSU_PATH "\\AppData\\Local\\osu!\\Songs\\"
#endif /* _WIN32 */

#ifdef __linux__
  #define ON_LINUX

  #include <X11/Xlib.h>
  #include <X11/extensions/XTest.h>

  #define HOME_ENV "HOME"
  #define LINUX_TIME_ADDRESS 0x36e5ba4
  // Probably incorrect.
  #define SIGNATURE "\xDB\x5D\xE8\x8B\x45\xE8\xA3"  

  #define SEPERATOR '/'
  #define DEFAULT_OSU_PATH "/osufolder/Songs/"
#endif /* __linux__ */

#ifdef DEBUG

  #define debug(...)\
      printf("[debug] [%s:%s] ", __FILE__, __func__);\
      printf(__VA_ARGS__);\
      putchar('\n');\

#else

  #define debug(...)\
      ;\

#endif /* DEBUG */

#define TAPTIME_MS 15

#define COLS_WIDTH 512

#define MAX_LINE_LENGTH 1024

#ifdef __GNUC__
	#define hot __attribute__((__hot__))
#else
	#define hot
#endif /* __GNUC */

#endif /* COMMON_H */
