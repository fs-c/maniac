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

	void *find_pattern(const char *pattern, int offset);

	void send_keypress(int key, bool down);

	/**
	 * Assumes that find_pattern(sig, off) is a pointer to a pointer containing
	 * a variable of type T. Returns the address of the pointer to said variable.
	 */
	template<typename T>
	T *get_address(const char *name, const char *sig, int off);
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

template<typename T>
T *Process::get_address(const char *name, const char *sig, int off) {
	auto address_ptr = reinterpret_cast<T **>(find_pattern(sig, off));

	if (!address_ptr) {
		// TODO: Replace this ASAP once std::format is a thing.
		char buffer[128];
		sprintf(buffer, "%s %s %s", "couldn't find the", name, "pointer");

		throw std::runtime_error(buffer);
	}

	debug("%s %s %s %#x", "found", name, "pointer at", (uintptr_t)address_ptr);

	T* address;
	read_memory<T *>(address_ptr, &address);

	if (!address) {
		// TODO: See above.
		char buffer[128];
		sprintf(buffer, "%s %s %s", "found invalid", name, "pointer");

		throw std::runtime_error(buffer);
	}

	return address;
}
