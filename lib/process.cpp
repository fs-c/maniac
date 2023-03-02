#include <maniac/process.h>
#include <format>

Process::Process(const std::string &name) {
    id = find_process_id(name);
    debug("%s '%s' %s %lu", "found process", name.c_str(), "with id", id);

	handle = get_handle();
    debug("%s '%s' (%#x)", "got handle to process", name.c_str(), (uintptr_t)handle);
}

Process::~Process() {
	CloseHandle(handle);
}

HANDLE Process::get_handle() {
	HANDLE handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, FALSE, id);
	if (!handle || handle == INVALID_HANDLE_VALUE) {
		throw std::runtime_error("failed opening handle to process");
	}

	return handle;
}


uintptr_t Process::find_signature(const Signature &signature) const {
    return find_signature(signature, 0, UINTPTR_MAX);
}

uintptr_t Process::find_signature(const Signature &signature, std::string_view module_name) const {
    uintptr_t address = 0;

    for_each_module([&](MODULEENTRY32 *mod) -> bool {
        // continue if the module is not the one we are looking for
        if (mod->szModule != module_name) {
            return true;
        }

        const auto base = reinterpret_cast<uintptr_t>(mod->modBaseAddr);
        address = find_signature(signature, base, base + mod->modBaseSize);

        return false;
    });

    return address;
}

void Process::for_each_module(const std::function<bool(MODULEENTRY32 *)>& continue_predicate) const {
    MODULEENTRY32 entry;
    entry.dwSize = sizeof(MODULEENTRY32);

    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, id);
    if (!snapshot || snapshot == INVALID_HANDLE_VALUE) {
        throw std::runtime_error(std::format("couldn't enumerate modules of process {}", id));
    }

    Module32First(snapshot, &entry);
    if (!continue_predicate(&entry)) {
        CloseHandle(snapshot);
        return;
    }

    while (Module32Next(snapshot, &entry)) {
        if (!continue_predicate(&entry)) {
            CloseHandle(snapshot);
            return;
        }
    }

    CloseHandle(snapshot);
}

uintptr_t Process::find_signature(const Signature &signature, uintptr_t initial_address, const uintptr_t max_address) const {
    auto cur_address = initial_address;

    MEMORY_BASIC_INFORMATION info;

    auto buffer = std::vector<std::uint8_t>{};

    while (cur_address < max_address) {
        if (!VirtualQueryEx(handle, reinterpret_cast<void *>(cur_address), &info, sizeof(info))) {
            throw std::runtime_error(std::format("couldn't query at {}", cur_address));
        }

        const auto base = reinterpret_cast<uintptr_t>(info.BaseAddress);

        bool invalid_type = (info.Type != MEM_IMAGE && info.Type != MEM_PRIVATE);
        bool invalid_protection = (info.Protect == PAGE_EXECUTE || info.Protect == PAGE_NOACCESS || info.Protect == PAGE_TARGETS_INVALID || info.Protect & PAGE_GUARD);

        if (!info.RegionSize || info.State != MEM_COMMIT || invalid_type || invalid_protection) {
            cur_address = base + info.RegionSize;

            continue;
        }

        buffer.resize(info.RegionSize);
        read_memory<std::uint8_t>(base, buffer.data(), buffer.size());

        const auto offset = signature.scan(buffer.begin(), buffer.end());

        if (offset) {
            return base + offset;
        }

        cur_address = base + info.RegionSize;
    }

    return 0;
}

unsigned int Process::find_process_id(const std::string &name) {
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
    throw std::runtime_error(std::format("couldn't find process '{}'", name));
}
