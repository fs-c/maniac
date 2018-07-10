#include "osu.h"

#ifdef ON_LINUX

// Enable GNU extensions (process_vm_readv).
#define _GNU_SOURCE

#include <sys/uio.h>

// TODO: Where the hell is this defined usually?
ssize_t process_vm_readv(int, struct iovec *, int, struct iovec *, int, int);

#endif /* ON_LINUX */

#ifdef ON_WINDOWS
#include <tlhelp32.h>
#endif /* ON_WINDOWS */

pid_t game_process_id;

/**
 * Gets and stores the runtime of the currently playing song, internally
 * referred to as `maptime` in *val.
 * Returns the number of bytes read.
 */
int32_t get_maptime(pid_t pid)
{
	int32_t time;

#ifdef ON_LINUX
	size_t size = sizeof(int32_t);

	struct iovec local[1];
	struct iovec remote[1];

	local[0].iov_len = size;
	local[0].iov_base = &time;

	remote[0].iov_len = size;
	remote[0].iov_base = (void *)time_address;

	process_vm_readv(game_process_id, local, 1, remote, 1, 0);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	ReadProcessMemory(game_proc, (LPCVOID)time_address, &time,
		sizeof(int32_t), NULL);
#endif /* ON_WINDOWS */

	return time;
}