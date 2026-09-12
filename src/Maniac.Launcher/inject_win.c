#ifdef _WIN32

#include "maniac.h"
#include "load_args.h"

#include <psapi.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>

static char g_detail[512];

static const char *describe_host(int code)
{
    switch ((uint32_t)code)
    {
    case 0x00000001: return "bootstrap threw (not hostfxr Success_HostAlreadyInitialized)";
    case 0x00000002: return "Success_DifferentRuntimeProperties";
    case 0x80008081: return "InvalidArgFailure";
    case 0x80008082: return "CoreHostLibLoadFailure";
    case 0x80008083: return "CoreHostLibMissingFailure";
    case 0x80008084: return "CoreHostEntryPointFailure";
    case 0x80008093: return "InvalidConfigFile (self-contained runtimeconfig cannot be used for component load)";
    case 0x80008096: return "FrameworkMissingFailure";
    case 0x800080a5: return "HostIncompatibleConfig (component frameworks incompatible with the loaded runtime)";
    case 0xC0000005: return "STATUS_ACCESS_VIOLATION (entry point signature must be int Method(IntPtr, int))";
    default: return "unknown";
    }
}

static int set_detail(int code)
{
    snprintf(g_detail, sizeof g_detail, "bootstrap returned %d (0x%08X) %s", code, (unsigned)code, describe_host(code));
    return 0;
}

static void *remote_alloc(HANDLE proc, size_t n, DWORD prot)
{
    return VirtualAllocEx(proc, NULL, n, MEM_COMMIT | MEM_RESERVE, prot);
}

static int remote_write(HANDLE proc, void *dest, const void *src, size_t n)
{
    SIZE_T put = 0;
    return WriteProcessMemory(proc, dest, src, n, &put) && put == n;
}

static int remote_call(HANDLE proc, void *fn, void *arg, DWORD *exit_code)
{
    HANDLE th = CreateRemoteThread(proc, NULL, 0, (LPTHREAD_START_ROUTINE)fn, arg, 0, NULL);
    if (!th)
        return 0;
    WaitForSingleObject(th, INFINITE);
    DWORD code = 0;
    int ok = GetExitCodeThread(th, &code);
    CloseHandle(th);
    if (!ok)
        return 0;
    *exit_code = code;
    return 1;
}

static HMODULE remote_module(HANDLE proc, const wchar_t *leaf)
{
    HMODULE mods[1024];
    DWORD needed = 0;
    if (!EnumProcessModulesEx(proc, mods, sizeof mods, &needed, LIST_MODULES_ALL))
        return NULL;
    int count = (int)(needed / sizeof mods[0]);
    wchar_t name[MAX_PATH];
    for (int i = 0; i < count; i++)
    {
        if (!GetModuleBaseNameW(proc, mods[i], name, MAX_PATH))
            continue;
        if (_wcsicmp(name, leaf) == 0)
            return mods[i];
    }
    return NULL;
}

static int module_path(HANDLE proc, HMODULE mod, wchar_t *out, size_t cap)
{
    DWORD n = GetModuleFileNameExW(proc, mod, out, (DWORD)cap);
    return n > 0 && n < cap;
}

static void *export_rva(const wchar_t *dll_path, const char *name)
{
    HMODULE local = LoadLibraryW(dll_path);
    if (!local)
        return NULL;
    FARPROC p = GetProcAddress(local, name);
    ptrdiff_t rva = p ? ((char *)p - (char *)local) : 0;
    FreeLibrary(local);
    return p ? (void *)rva : NULL;
}

static int kernel32_remote(HANDLE proc, const char *name, void **out)
{
    HMODULE remote_k = remote_module(proc, L"kernel32.dll");
    if (!remote_k)
        return 0;
    wchar_t path[MAX_PATH];
    if (!module_path(proc, remote_k, path, MAX_PATH))
        return 0;
    void *rva = export_rva(path, name);
    if (!rva)
        return 0;
    *out = (char *)remote_k + (ptrdiff_t)rva;
    return 1;
}

int inject_win(int pid, const char *plugin_dir, const char *dll, const char *command, const char **detail)
{
    char stub_path[MANIAC_PATH_MAX];
    char runtimeconfig[MANIAC_PATH_MAX];
    path_join(stub_path, sizeof stub_path, plugin_dir, MANIAC_STUB_DLL);
    path_join(runtimeconfig, sizeof runtimeconfig, plugin_dir, "Maniac.runtimeconfig.json");
    if (!path_exists(runtimeconfig) || !path_exists(dll) || !path_exists(stub_path))
    {
        *detail = MSG_INCOMPLETE;
        return 0;
    }
    verbose("runtime   %s", runtimeconfig);
    verbose("method    native stub");

    HANDLE proc = OpenProcess(
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
            PROCESS_VM_WRITE | PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION,
        FALSE, (DWORD)pid);
    if (!proc)
    {
        snprintf(g_detail, sizeof g_detail, "OpenProcess failed (%lu)", GetLastError());
        *detail = g_detail;
        return 0;
    }

    BOOL wow = FALSE;
    if (IsWow64Process(proc, &wow) && wow)
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "osu! is 32-bit; maniac is x64");
        *detail = g_detail;
        return 0;
    }

    wchar_t wstub[1024], wdll[1024], wcfg[1024];
    if (!utf8_to_wide(stub_path, wstub, 1024) || !utf8_to_wide(dll, wdll, 1024) || !utf8_to_wide(runtimeconfig, wcfg, 1024))
    {
        CloseHandle(proc);
        *detail = MSG_INCOMPLETE;
        return 0;
    }

    size_t stub_bytes = (wcslen(wstub) + 1) * sizeof(wchar_t);
    void *remote_stub_path = remote_alloc(proc, stub_bytes, PAGE_READWRITE);
    if (!remote_stub_path || !remote_write(proc, remote_stub_path, wstub, stub_bytes))
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "VirtualAllocEx/WriteProcessMemory failed");
        *detail = g_detail;
        return 0;
    }

    void *loadlibrary = NULL;
    if (!kernel32_remote(proc, "LoadLibraryW", &loadlibrary))
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "LoadLibraryW not found");
        *detail = g_detail;
        return 0;
    }

    DWORD ignored = 0;
    if (!remote_call(proc, loadlibrary, remote_stub_path, &ignored))
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "CreateRemoteThread LoadLibraryW failed (%lu)", GetLastError());
        *detail = g_detail;
        return 0;
    }
    VirtualFreeEx(proc, remote_stub_path, 0, MEM_RELEASE);

    HMODULE remote_stub = remote_module(proc, L"maniac-stub.dll");
    if (!remote_stub)
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "maniac-stub.dll did not load in osu!");
        *detail = g_detail;
        return 0;
    }

    void *rva = export_rva(wstub, "ManiacLoad");
    if (!rva)
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "ManiacLoad export missing");
        *detail = g_detail;
        return 0;
    }
    void *remote_load = (char *)remote_stub + (ptrdiff_t)rva;

    ManiacLoadArgs args;
    memset(&args, 0, sizeof args);
    HMODULE hostfxr = remote_module(proc, L"hostfxr.dll");
    if (hostfxr)
        module_path(proc, hostfxr, args.hostfxr, 1024);
    wcsncpy(args.runtimeconfig, wcfg, 1023);
    wcsncpy(args.dll, wdll, 1023);
    wcsncpy(args.type_name, L"Maniac.Entry, Maniac", 255);
    wcsncpy(args.method_name, L"Bootstrap", 63);
    wchar_t wcmd[32];
    utf8_to_wide(command, wcmd, 32);
    wcsncpy(args.command, wcmd, 31);

    void *remote_args = remote_alloc(proc, sizeof args, PAGE_READWRITE);
    if (!remote_args || !remote_write(proc, remote_args, &args, sizeof args))
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "failed to write load args");
        *detail = g_detail;
        return 0;
    }

    DWORD code = 0;
    if (!remote_call(proc, remote_load, remote_args, &code))
    {
        CloseHandle(proc);
        snprintf(g_detail, sizeof g_detail, "CreateRemoteThread ManiacLoad failed (%lu)", GetLastError());
        *detail = g_detail;
        return 0;
    }
    VirtualFreeEx(proc, remote_args, 0, MEM_RELEASE);
    CloseHandle(proc);

    if (code != 0)
    {
        set_detail((int)code);
        *detail = g_detail;
        return 0;
    }
    return 1;
}

int inject(int pid, const char *plugin_dir, const char *dll, const char *command, const char **detail)
{
    return inject_win(pid, plugin_dir, dll, command, detail);
}

#endif
