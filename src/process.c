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

void *find_pattern(const unsigned char *signature);

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
#ifdef ON_WINDOWS
	DWORD proc_id = NULL;
	HANDLE proc_list = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

	PROCESSENTRY32 entry = { 0 };
	entry.dwSize = sizeof(PROCESSENTRY32);

	if (!Process32First(proc_list, &entry)) {
		goto end;
	}

	while (Process32Next(proc_list, &entry)) {
		if (_stricmp((char *)entry.szExeFile, name) == 0) {
			proc_id = entry.th32ProcessID;

			goto end;
		}
	}

end:
	CloseHandle(proc_list);

	return proc_id;
#endif /* ON_WINDOWS */
	// Remove warning.
	return name ? 0 : 0;
}

void *find_pattern(const unsigned char *signature)
{
#ifdef ON_WINDOWS
	bool hit = false;
	const size_t read_size = 4096;
	const size_t signature_size = sizeof(signature);

	unsigned char chunk[read_size];

	for (size_t i = 0; i < INT_MAX; i += read_size - signature_size) {
		if (!ReadProcessMemory(game_proc, (LPCVOID)i,
			&chunk, read_size, NULL))
		{
			continue;
		}

		for (size_t j = 0; j < read_size; j++) {
			hit = true;

			for (size_t k = 0; k < signature_size && hit; k++) {
				if (chunk[j + k] != signature[k]) {
					hit = false;
				}
			}

			if (hit) {
				return (void *)(i + j + sizeof(signature) - 1);
			}
		}
	}
#endif /* ON_WINDOWS */
	// Remove warning.
	return signature ? NULL : NULL;
}

void *get_time_address()
{
#ifdef ON_WINDOWS
	DWORD time_address = NULL;
	void *time_ptr = find_pattern((PBYTE)SIGNATURE);

	if (!ReadProcessMemory(game_proc, (LPCVOID)time_ptr, &time_address,
		sizeof(DWORD), NULL))
	{
		return NULL;
	}

	return (void *)(intptr_t)time_address;
#endif

#ifdef ON_LINUX
	return TIME_ADDRESS;
#endif
}