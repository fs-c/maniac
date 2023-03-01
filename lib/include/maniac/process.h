#pragma once

#include <map>
#include <string>
#include <vector>
#include <stdexcept>
#include <functional>
#include <maniac/common.h>
#include <maniac/signature.h>
// maniac/common.h includes windows.h, win32 headers can't be included beforehand
#include <TlHelp32.h>

class Process {
	HANDLE handle;
    unsigned int id;

public:
	explicit Process(const std::string &name);

	~Process();

	/**
	 * Reads `sizeof(T) * count` bytes of memory at the specified address and writes it to `*out`.
	 * Returns the number of bytes read. Generally a faster alternative to `T read_memory(...)`
	 * as it does not throw exceptions and is a bare `ReadProcessMemory` wrapper.
	 */
	template<typename T>
	inline size_t read_memory(uintptr_t address, T *out, size_t count = 1) const;

	/**
	 * Reads `sizeof(T)` bytes of memory at the specified address and returns it. *As
	 * opposed to reporting errors through the return value this function throws a runtime
	 * exception on failed read!*
	 */
	template<typename T>
	inline T read_memory(uintptr_t address) const;

	/**
	 * See `T read_memory`, except that it throws exceptions with the given name included in the
	 * error message.
	 */
	template<typename T, typename Any = uintptr_t>
	T read_memory_safe(const char *name, Any address) const;

    /**
     * Searches for the given signature in the whole readable process address space.
     */
    uintptr_t find_signature(const Signature& signature) const;

    /**
     * Searches for the given signature in the given module address space.
     */
    uintptr_t find_signature(const Signature& signature, std::string_view module_name) const;

	/**
	 * Sends either a key down or a key up event (depending on `down`) of the specified
	 * character. `key` is expected to literally be a character, i.e. `a` not `0x1E`.
	 * Currently independent of the current process, the recipient must be in focus.
	 */
	static void send_keypress(char key, bool down);

    /**
     * See `send_keypress`. Expects a scan code instead of a literal character.
     */
    static void send_scan_code(short code, bool down);

private:
    static unsigned int find_process_id(const std::string &name);

    HANDLE get_handle();

    /**
     * Executes the given function for every loaded module, stops if it returns false.
     */
    void for_each_module(const std::function<bool(MODULEENTRY32 *)>& continue_predicate) const;

    /**
     * Searches for the given signature between initial and max address.
     */
    uintptr_t find_signature(const Signature &signature, uintptr_t initial_address, uintptr_t max_address) const;
};

template<typename T>
inline size_t Process::read_memory(uintptr_t address, T *out, size_t count) const {
	size_t read = 0;

	if (!ReadProcessMemory(handle, reinterpret_cast<LPCVOID>(address),
		reinterpret_cast<LPVOID>(out), count * sizeof(T),
		reinterpret_cast<SIZE_T *>(&read))) {
        debug("failed reading memory at %x (%d)", address, GetLastError());

		return 0;
	}

	return read;
}

template<typename T>
inline T Process::read_memory(uintptr_t address) const {
	T out;

	if (!read_memory(address, &out, 1)) {
		throw std::runtime_error("failed reading memory");
	}

	return out;
}

template<typename T, typename Any>
T Process::read_memory_safe(const char *name, Any addr) const {
	// TODO: So much for "safe".
	auto address = (uintptr_t)(void *)addr;

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

	debug("%s: %#x", name, (unsigned int)address);

	return out;
}

inline void Process::send_keypress(char key, bool down) {
    static auto layout = GetKeyboardLayout(0);

    send_scan_code(VkKeyScanEx(key, layout) & 0xFF, down);
}

inline void Process::send_scan_code(short code, bool down) {
	static INPUT in;

	in.type = INPUT_KEYBOARD;
	in.ki.time = 238423874;
	in.ki.wScan = 0;
	in.ki.dwExtraInfo = 0;
	in.ki.dwFlags = down ? 0 : KEYEVENTF_KEYUP;
	in.ki.wVk = code;

	if (!SendInput(1, &in, sizeof(INPUT))) {
		debug("failed sending input: %lu", GetLastError());
	}
}
