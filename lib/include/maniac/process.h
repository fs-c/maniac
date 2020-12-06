#pragma once

#include <map>
#include <string>
#include <vector>
#include <stdexcept>
#include <maniac/common.h>
// maniac/common.h includes windows.h, win32 headers can't be included beforehand
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
	 * as it does not throw exceptions and is a bare `ReadProcessMemory` wrapper.
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
	template<typename T, typename Any = uintptr_t>
	T read_memory_safe(const char *name, Any address);

	/**
	 * Searches for the address of the _beginning_ of the given pattern in the process memory
	 * and returns it. Returns `0` if the pattern could not be found.
	 * Expects `pattern` to be in "IDA-Style", i.e. to write bytes in hexadecimal notation
	 * and to denote wildcards by a single question mark. Example: `EB 0A A1 ? ? ? ? A3`.
	 */
	uintptr_t find_pattern(const char *pattern);

	/**
	 * Sends either a key down or a key up event (depending on `down`) of the specified
	 * character. `key` is expected to literally be a character, i.e. `a` not `0x1E`.
	 * Currently independent of the current process, the recipient must be in focus.
	 */
	static void send_keypress(char key, bool down);
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

template<typename T, typename Any>
T Process::read_memory_safe(const char *name, Any addr) {
	// TODO: So much for "safe".
	uintptr_t address = (uintptr_t)(void *)addr;

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

	debug_short("%s: %#x", name, (unsigned int)address);

	return out;
}

inline void Process::send_keypress(char key, bool down) {
	// TODO: Look into KEYEVENTF_SCANCODE (see esp. KEYBDINPUT remarks section).

	static INPUT in;
	static auto layout = GetKeyboardLayout(0);

	in.type = INPUT_KEYBOARD;
	in.ki.time = 0;
	in.ki.wScan = 0;
	in.ki.dwExtraInfo = 0;
	in.ki.dwFlags = down ? 0 : KEYEVENTF_KEYUP;
	// TODO: Populate an array of scan codes for the keys that are going to be
	//	 pressed to avoid calculating them all the time.
	in.ki.wVk = VkKeyScanEx(key, layout) & 0xFF;

	if (!SendInput(1, &in, sizeof(INPUT))) {
		debug("failed sending input: %lu", GetLastError());
	}
}
