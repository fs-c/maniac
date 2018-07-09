#include "osu.h"

// Enable GNU extensions (process_vm_readv).
#define _GNU_SOURCE

#include <sys/uio.h> // process_vm_readv()

ssize_t process_vm_readv(int, struct iovec *, int, struct iovec *, int, int);

/**
 * Gets and stores the runtime of the currently playing song, internally
 * referred to as `maptime` in *val.
 * Returns the number of bytes read.
 */
int32_t get_maptime(pid_t pid)
{
	int32_t buf;
	size_t size = sizeof(int32_t);

	struct iovec local[1];
	struct iovec remote[1];

	local[0].iov_len = size;
	local[0].iov_base = &buf;

	remote[0].iov_len = size;
	remote[0].iov_base = (void *)TIME_ADDRESS;

	process_vm_readv(pid, local, 1, remote, 1, 0);

	return buf;
}