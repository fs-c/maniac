#ifndef PROCESS_H
#define PROCESS_H

#include "common.h"

#include <string.h>
#include <limits.h>

#ifdef ON_LINUX
// Enable GNU extensions (process_vm_readv).
// TODO: This is hacky and undocumented.
#define __USE_GNU

  #include <sys/uio.h>
#endif /* ON_LINUX */

#ifdef ON_WINDOWS

  #include <tlhelp32.h>

  #define TIME_SIG      "\xDB\x5D\xE8\x8B\x45\xE8\xA3"
  #define OSU_PATH_SIG  "\xF2\x60\x28\xB8\x00\x0C\x22"

HANDLE game_proc;
#endif /* ON_WINDOWS */

void *time_address;
pid_t game_proc_id;

/**
 * Gets and returns the runtime of the currently playing song, internally
 * referred to as `maptime`.
 */
hot int32_t get_maptime(void);

/**
 * Returns the process id of the given process or zero if it was not found.
 */
unsigned long get_process_id(const char *name);

/**
 * Copies game memory starting at `base` for `size` bytes into `buffer`.
 * Internally, this is a wrapper for _read_game_memory, with argument checking.
 * Returns number of bytes read and copied.
 */
ssize_t read_game_memory(void *base, void *buffer, size_t size);

/**
 * Searches for a signature (sequence of bytes) in the process, returning the
 * address of the end (!) of the first occurence.
 */
void *find_pattern(const unsigned char *signature, unsigned int sig_len);

/**
 * Returns the address of the playback time in the address space of the game
 * process.
 * Windows: Scans memory for the address using a signature.
 * Linux: Returns static address (LINUX_TIME_ADDRESS).
 */
void *get_time_address(void);

/**
 */
size_t get_osu_path_exp(char **path);

#endif /* PROCESS_H */
