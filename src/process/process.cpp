#include "process.h"

Process::Process(const std::string &name) {
	handle = get_handle(name);
}

Process::~Process() {
	CloseHandle(handle);
}

HANDLE Process::get_handle(const std::string &name) {
	auto find_process = [](const std::string &name) {
		PROCESSENTRY32 processInfo;
		processInfo.dwSize = sizeof(PROCESSENTRY32);

		HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, NULL);
		if (!snapshot || snapshot == INVALID_HANDLE_VALUE) {
			throw std::runtime_error("failed to get process list");
		}

		Process32First(snapshot, &processInfo);
		if (name == processInfo.szExeFile) {
			CloseHandle(snapshot);
			return processInfo.th32ProcessID;
		}

		while (Process32Next(snapshot, &processInfo)) {
			if (name == processInfo.szExeFile) {
				CloseHandle(snapshot);
				return processInfo.th32ProcessID;
			}
		}

		CloseHandle(snapshot);
		return 0ul;
	};

	auto process_id = find_process(name);
	if (!process_id) {
		throw std::runtime_error("failed finding process");
	}

	debug("%s '%s' %s %lu", "found process", name.c_str(), "with id", process_id);

	HANDLE handle = OpenProcess(PROCESS_VM_READ, FALSE, process_id);
	if (!handle || handle == INVALID_HANDLE_VALUE) {
		throw std::runtime_error("failed opening handle to process");
	}

	debug("%s '%s' (%#x)", "got handle to process", name.c_str(), (uintptr_t)handle);

	return handle;
}

/**
 * Expects `pattern` to be in "IDA-Style", i.e. to group bytes in pairs of two and to denote
 * wildcards by a single question mark.
 */
void *Process::find_pattern(const char *pattern, int offset) {
	auto pattern_bytes = std::vector<int>{ };

	for (auto cur = pattern; *cur; cur++) {
		if (*cur == '?') {
			// If the current byte is a wildcard push a dummy byte
			pattern_bytes.push_back(-1);
		} else if (*cur == ' ') {
			continue;
		} else {
			// This is somewhat hacky: strtol parses *as many characters as
			// possible* and sets cur to the first character it couldn't parse.
			// This is the reason why patterns *must* follow the structure
			// "AB CD EF ? ? FF"; "ABCDEF??FF" would cause it to fail later (!).
			pattern_bytes.push_back(strtol(cur, const_cast<char **>(&cur), 16));
		}
	}

	auto pattern_size = pattern_bytes.size();

	const size_t chunk_size = 4096;
	std::byte chunk_bytes[chunk_size];

	for (size_t i = 1; i < INT_MAX; i += chunk_size - pattern_size) {
		auto chunk_offset = reinterpret_cast<std::byte *>(i);
		if (!read_memory<chunk_size, std::byte>(chunk_offset, chunk_bytes)) {
			continue;
		}

		for (size_t j = 0; j < chunk_size; j++) {
			bool hit = true;

			for (size_t k = 0; k < pattern_size; k++) {
				if (pattern_bytes[k] == -1) {
					continue;
				}

				if (chunk_bytes[j + k] != static_cast<std::byte>(pattern_bytes[k])) {
					hit = false;
					break;
				} else {
					k;
				}
			}

			if (hit) {
				return reinterpret_cast<void *>(i + j + offset);
			}
		}
	}

	return nullptr;
}
