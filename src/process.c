#include "osu.h"

#ifdef ON_LINUX
  // Enable GNU extensions (process_vm_readv).
  // TODO: This is hacky and undocumented.
  #define __USE_GNU

  #include <sys/uio.h>
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
  #include <tlhelp32.h>

  HANDLE game_proc;
#endif /* ON_WINDOWS */

void *time_address;
pid_t game_proc_id;

/**
 * Copies game memory at base for size bytes into buffer.
 * Inlined, hot version without argument validation.
 * Returns number of bytes read.
 */
static inline __hot ssize_t _read_game_memory(void *base, void *buffer,
	size_t size);

__hot int32_t get_maptime()
{
	int32_t time = 0;
	size_t size = sizeof(int32_t);

	// This function is called in tight loops, use the faster, insecure
	// read_game_memory since we know our arguments are valid.
	if (!(_read_game_memory(time_address, &time, size)))
		return 0;

	return time;
}

ssize_t read_game_memory(void *base, void *buffer, size_t size)
{
	if (!base || !buffer || !size)
		return 0;

	ssize_t read = 0;

	if (!(read = _read_game_memory(base, buffer, size)))
		return 0;
	
	return read;
}

static inline __hot ssize_t _read_game_memory(void *base, void *buffer,
	size_t size)
{
	ssize_t read = 0;
	
#ifdef ON_LINUX
	struct iovec local[1];
	struct iovec remote[1];

	local[0].iov_len = size;
	local[0].iov_base = buffer;

	remote[0].iov_len = size;
	remote[0].iov_base = base;

	read = process_vm_readv(game_proc_id, local, 1, remote, 1, 0);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	ReadProcessMemory(game_proc, (LPCVOID)base, buffer, size,
		(SIZE_T *)&read);
#endif /* ON_WINDOWS */

	return read;
}

unsigned long get_process_id(const char *name)
{
	unsigned long proc_id = 0;

#ifdef ON_LINUX
	char *cmd = (char *)calloc(1, 200);
	sprintf(cmd, "pidof %s", name);

	FILE *f = popen(cmd, "r");
	size_t read = fread(cmd , 1, 200, f);

	fclose(f);

	proc_id = read ? atoi(cmd) : 0;

	free(cmd);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	HANDLE proc_list = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

	PROCESSENTRY32 entry = { 0 };
	entry.dwSize = sizeof(PROCESSENTRY32);

	if (!Process32First(proc_list, &entry)) {
		goto end;
	}

	while (Process32Next(proc_list, &entry)) {
		if (_stricmp((char *)entry.szExeFile, name) == 0) {
			proc_id = (unsigned long)entry.th32ProcessID;

			goto end;
		}
	}

end:
	CloseHandle(proc_list);
#endif /* ON_WINDOWS */

	debug("process ID for %s is %ld", name, proc_id);

	return proc_id;
}
