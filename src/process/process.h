#pragma once

#include "common.h"

#include <map>
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

	/**
	 * Reads `sizeof(T) * count` bytes of memory at the specified address and writes it to `*out`.
	 * Returns the number of bytes read. Generally a faster alternative to `T read_memory(...)`
	 * as it does not throw exceptions and is a bare ReadProcessMemory wrapper.
	 */
	template<typename T>
	inline size_t read_memory(uintptr_t address, T *out, size_t count = 1);

	/**
	 * Reads `sizeof(T)` bytes of memory at the specified address and returns it. *As
	 * opposed to reporting errors through the return value this function throws a runtime
	 * exception on failed read!*
	 */
	template<typename T>
	inline T read_memory(uintptr_t address);

	/**
	 * See `T read_memory`, except that it throws exceptions with the given name included in the
	 * error message.
	 */
	template<typename T>
	T read_memory_safe(const char *name, uintptr_t address);

	/**
	 * Expects `pattern` to be in "IDA-Style", i.e. to group bytes in pairs of two and to denote
	 * wildcards by a single question mark. Returns 0 if the pattern couldn't be found.
	 */
	uintptr_t find_pattern(const char *pattern);
};

template<typename T>
inline size_t Process::read_memory(uintptr_t address, T *out, size_t count) {
	size_t read = 0;

	if (!ReadProcessMemory(handle, reinterpret_cast<LPCVOID>(address),
		reinterpret_cast<LPVOID>(out), count * sizeof(T),
		reinterpret_cast<SIZE_T *>(&read))) {
		return 0;
	}

	return read;
}

template<typename T>
inline T Process::read_memory(uintptr_t address) {
	T out;

	if (!read_memory(address, &out, 1)) {
		throw std::runtime_error("failed reading memory");
	}

	return out;
}

template<typename T>
T Process::read_memory_safe(const char *name, uintptr_t address) {
	if (!address) {
		// TODO: Get rid of this ASAP once std::format is out.
		char msg[128];
		msg[127] = '\0';

		sprintf_s(msg, 128, "pointer to %s was invalid", name);

		throw std::runtime_error(msg);
	}

	T out;

	if (!read_memory(address, &out, 1)) {
		// TODO: See above.
		char msg[128];
		msg[127] = '\0';

		sprintf_s(msg, 128, "failed reading %s at %#x", name, address);

		throw std::runtime_error(msg);
	}

	return out;
}
