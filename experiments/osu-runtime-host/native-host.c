#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>

typedef wchar_t char_t;
typedef void *hostfxr_handle;

struct hostfxr_initialize_parameters
{
    size_t size;
    const char_t *host_path;
    const char_t *dotnet_root;
};

typedef int32_t (__cdecl *hostfxr_initialize_for_dotnet_command_line_fn)(
    int argc,
    const char_t **argv,
    const struct hostfxr_initialize_parameters *parameters,
    hostfxr_handle *host_context_handle);

typedef int32_t (__cdecl *hostfxr_run_app_fn)(const hostfxr_handle host_context_handle);
typedef int32_t (__cdecl *hostfxr_close_fn)(const hostfxr_handle host_context_handle);

static void die(const char *msg, int code)
{
    fprintf(stderr, "native-host: %s (0x%08X)\n", msg, (unsigned)code);
    ExitProcess(code == 0 ? 1 : (unsigned)code);
}

static int find_hostfxr(const wchar_t *dotnet_root, wchar_t *out, int out_cch)
{
    wchar_t fxr[MAX_PATH];
    lstrcpynW(fxr, dotnet_root, MAX_PATH);
    lstrcatW(fxr, L"\\host\\fxr");

    wchar_t pattern[MAX_PATH];
    lstrcpynW(pattern, fxr, MAX_PATH);
    lstrcatW(pattern, L"\\*");

    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
    {
        fwprintf(stderr, L"native-host: FindFirstFile failed for %s (err %u)\n", pattern, (unsigned)GetLastError());
        return 0;
    }

    wchar_t best[MAX_PATH] = {0};
    do
    {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            continue;
        if (fd.cFileName[0] == L'.')
            continue;
        if (best[0] == 0 || wcscmp(fd.cFileName, best) > 0)
            lstrcpynW(best, fd.cFileName, MAX_PATH);
    } while (FindNextFileW(h, &fd));
    FindClose(h);

    if (best[0] == 0)
        return 0;

    lstrcpynW(out, fxr, out_cch);
    lstrcatW(out, L"\\");
    lstrcatW(out, best);
    lstrcatW(out, L"\\hostfxr.dll");
    if (GetFileAttributesW(out) == INVALID_FILE_ATTRIBUTES)
        return 0;
    return 1;
}

int wmain(int argc, wchar_t **argv)
{
    if (argc < 3)
    {
        fwprintf(stderr, L"usage: native-host <dotnet_root> <app.dll> [args...]\n");
        return 2;
    }

    const wchar_t *dotnet_root = argv[1];
    wchar_t hostfxr_path[MAX_PATH];
    if (!find_hostfxr(dotnet_root, hostfxr_path, MAX_PATH))
        die("hostfxr.dll not found under <dotnet_root>\\host\\fxr\\<ver>\\", 0x80008083);

    fwprintf(stderr, L"native-host: loading %s\n", hostfxr_path);
    HMODULE fxr = LoadLibraryW(hostfxr_path);
    if (!fxr)
        die("LoadLibrary hostfxr.dll failed", (int)GetLastError());

    hostfxr_initialize_for_dotnet_command_line_fn init =
        (hostfxr_initialize_for_dotnet_command_line_fn)GetProcAddress(fxr, "hostfxr_initialize_for_dotnet_command_line");
    hostfxr_run_app_fn run = (hostfxr_run_app_fn)GetProcAddress(fxr, "hostfxr_run_app");
    hostfxr_close_fn close = (hostfxr_close_fn)GetProcAddress(fxr, "hostfxr_close");
    if (!init || !run || !close)
        die("hostfxr exports missing", 0);

    wchar_t host_path[MAX_PATH];
    GetModuleFileNameW(NULL, host_path, MAX_PATH);

    struct hostfxr_initialize_parameters params;
    params.size = sizeof(params);
    params.host_path = host_path;
    params.dotnet_root = dotnet_root;

    int app_argc = argc - 2;
    const char_t **app_argv = (const char_t **)(argv + 2);

    hostfxr_handle ctx = NULL;
    int32_t rc = init(app_argc, app_argv, &params, &ctx);
    if (rc != 0 || ctx == NULL)
        die("hostfxr_initialize_for_dotnet_command_line failed", rc);

    rc = run(ctx);
    close(ctx);
    return rc;
}
