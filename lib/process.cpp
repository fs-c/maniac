#include <maniac/process.h>

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

		HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
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

	HANDLE handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, FALSE, process_id);
	if (!handle || handle == INVALID_HANDLE_VALUE) {
		throw std::runtime_error("failed opening handle to process");
	}

	debug("%s '%s' (%#x)", "got handle to process", name.c_str(), (uintptr_t)handle);

	return handle;
}

uintptr_t Process::find_pattern(const char *pattern) {
    const auto pattern_to_bytes = [](const char *pattern) {
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

        return pattern_bytes;
    };

    const auto pattern_bytes = pattern_to_bytes(pattern);

	uintptr_t cur_address = 0;
    // this is an osu-specific optimization
    uintptr_t max_address = 0x7fffffff;

    while (cur_address < max_address) {
        MEMORY_BASIC_INFORMATION info;

        if (!VirtualQueryEx(handle, reinterpret_cast<void *>(cur_address), &info, sizeof(info))) {
            debug("couldn't query at %x", cur_address);

            return 0;
        }

        bool invalidType = (info.Type != MEM_IMAGE && info.Type != MEM_PRIVATE);
        bool invalidProtection = (info.Protect != PAGE_WRITECOPY && info.Protect != PAGE_EXECUTE_WRITECOPY
                && info.Protect != PAGE_EXECUTE_READWRITE && info.Protect != PAGE_READWRITE);

        if (info.RegionSize == 0 || info.State != MEM_COMMIT || invalidType || invalidProtection) {
            cur_address = reinterpret_cast<uintptr_t>(info.BaseAddress) + info.RegionSize;

            continue;
        }

        cur_address = reinterpret_cast<uintptr_t>(info.BaseAddress);

        auto buffer = std::vector<std::byte>(info.RegionSize);
        read_memory<std::byte>(cur_address, buffer.data(), buffer.size());

        for (size_t j = 0; j < buffer.size() - pattern_bytes.size(); j++) {
			bool hit = true;

			for (size_t k = 0; k < pattern_bytes.size(); k++) {
				if (pattern_bytes[k] == -1) {
					continue;
				}

				if (buffer.at(j + k) != static_cast<std::byte>(pattern_bytes[k])) {
					hit = false;
					break;
				}
			}

			if (hit) {
				return cur_address + j;
			}
		}

        cur_address += info.RegionSize;
    }

	return 0;
}
