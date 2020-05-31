#pragma once

#include "common.h"

#include <string>
#include <vector>
#include <cstdlib>
#include <stdexcept>
#include <tlhelp32.h>

class Process {
	HANDLE handle;

	static HANDLE get_handle(const std::string &name);

public:
	explicit Process(const std::string &name);

	~Process();

	template<typename T>
	size_t read_memory(T *address, T *out);

	template<size_t count, typename T>
	inline size_t read_memory(T *address, T *out);

	void *find_pattern(const char *pattern);

	void send_keypress(int key, bool down);
};

template<typename T>
inline size_t Process::read_memory(T *address, T *out) {
	return read_memory<1, T>(address, out);
}

template<size_t count, typename T>
inline size_t Process::read_memory(T *address, T *out) {
	size_t read;

	ReadProcessMemory(handle, address, out, sizeof(T) * count,
		reinterpret_cast<SIZE_T *>(&read));

	return read;
}

inline void Process::send_keypress(int key, bool down) {
	debug("stub");
}
