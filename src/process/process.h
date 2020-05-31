#pragma once

#include "common.h"

#include <string>
#include <vector>
#include <stdexcept>
#include <tlhelp32.h>

class Process {
	HANDLE handle;

	static HANDLE get_handle(const std::string &name);

public:
	explicit Process(const std::string &name);

	~Process();

	/*template<typename T>
	inline T read_memory(T *address);

	template<typename T>
	size_t read_memory(T *address, T *out);

	template<size_t count, typename T>
	inline size_t read_memory(T *address, T *out);*/

	/**
	 * Reads `sizeof(T) * count` bytes of memory at the specified address and writes it to `*out`.
	 * Returns the number of bytes read.
	 */
	template<typename T>
	inline size_t read_memory(T *address, T *out, size_t count = 1);

	/**
	 * Reads `sizeof(T)` bytes of memory at the specified address and returns it. The return
	 * value is `INT_MIN` on failure.
	 */
	template<typename T>
	inline T read_memory(T *address);

	uintptr_t find_pattern(const char *pattern);

	void send_keypress(int key, bool down);
};

template<typename T>
inline size_t Process::read_memory(T *address, T *out, size_t count) {
	size_t read;

	ReadProcessMemory(handle, reinterpret_cast<LPCVOID>(address),
		reinterpret_cast<LPVOID>(out), count * sizeof(T),
		reinterpret_cast<SIZE_T *>(&read));

	return read;
}

template<typename T>
inline T Process::read_memory(T *address) {
	T out;

	auto read = read_memory(address, &out, 1);

	return read != sizeof(T) ? INT_MIN : out;
}

inline void Process::send_keypress(int key, bool down) {
	debug("stub");
}
