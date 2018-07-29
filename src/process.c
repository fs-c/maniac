#include "osu.h"

#include <stdio.h>

#ifdef ON_LINUX
  // Enable GNU extensions (process_vm_readv).
  #define _GNU_SOURCE

  #include <sys/uio.h>

  // TODO: Where the hell is this defined usually?
  ssize_t process_vm_readv(int, struct iovec *, int, struct iovec *, int, int);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
  #include <tlhelp32.h>

  HANDLE game_proc;
#endif /* ON_WINDOWS */

void *time_address;
pid_t game_proc_id;

int32_t get_maptime()
{
	int32_t time;
	size_t size = sizeof(int32_t);

#ifdef ON_LINUX
	struct iovec local[1];
	struct iovec remote[1];

	local[0].iov_len = size;
	local[0].iov_base = &time;

	remote[0].iov_len = size;
	remote[0].iov_base = time_address;

	process_vm_readv(game_proc_id, local, 1, remote, 1, 0);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	ReadProcessMemory(game_proc, (LPCVOID)time_address, &time, size, NULL);
#endif /* ON_WINDOWS */

	return time;
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

	return proc_id;
}
