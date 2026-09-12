#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include "load_args.h"

typedef void *hostfxr_handle;

struct hostfxr_initialize_parameters
{
    size_t size;
    const wchar_t *host_path;
    const wchar_t *dotnet_root;
};

typedef int32_t (__cdecl *hostfxr_initialize_for_runtime_config_fn)(
    const wchar_t *runtime_config_path,
    const struct hostfxr_initialize_parameters *parameters,
    hostfxr_handle *host_context_handle);

typedef int32_t (__cdecl *hostfxr_get_runtime_delegate_fn)(
    hostfxr_handle context,
    int type,
    void **delegate);

typedef int32_t (__cdecl *hostfxr_close_fn)(hostfxr_handle context);

typedef int32_t (__cdecl *load_assembly_and_get_function_pointer_fn)(
    const wchar_t *assembly_path,
    const wchar_t *type_name,
    const wchar_t *method_name,
    const wchar_t *delegate_type_name,
    void *reserved,
    void **delegate);

typedef int (__cdecl *component_entry_point_fn)(void *arg, int32_t arg_size_in_bytes);

enum
{
    hdt_load_assembly_and_get_function_pointer = 5
};

static size_t wlen(const wchar_t *s)
{
    size_t n = 0;
    while (s[n])
        n++;
    return n;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)inst;
    (void)reason;
    (void)reserved;
    return TRUE;
}

__declspec(dllexport) DWORD WINAPI ManiacLoad(ManiacLoadArgs *a)
{
    if (!a)
        return 0x80008081; /* InvalidArgFailure */

    HMODULE fxr = GetModuleHandleW(L"hostfxr.dll");
    if (!fxr)
        fxr = LoadLibraryW(a->hostfxr);
    if (!fxr)
        return 0x80008083; /* CoreHostLibMissingFailure */

    hostfxr_initialize_for_runtime_config_fn init =
        (hostfxr_initialize_for_runtime_config_fn)GetProcAddress(fxr, "hostfxr_initialize_for_runtime_config");
    hostfxr_get_runtime_delegate_fn get_del =
        (hostfxr_get_runtime_delegate_fn)GetProcAddress(fxr, "hostfxr_get_runtime_delegate");
    hostfxr_close_fn close =
        (hostfxr_close_fn)GetProcAddress(fxr, "hostfxr_close");
    if (!init || !get_del || !close)
        return 0x80008084; /* CoreHostEntryPointFailure */

    hostfxr_handle ctx = NULL;
    int32_t rc = init(a->runtimeconfig, NULL, &ctx);
    /* 0 success, 1 already initialized, 2 different runtime properties */
    if ((rc != 0 && rc != 1 && rc != 2) || !ctx)
        return (DWORD)rc;

    void *load = NULL;
    rc = get_del(ctx, hdt_load_assembly_and_get_function_pointer, &load);
    close(ctx);
    if (rc != 0 || !load)
        return rc ? (DWORD)rc : 0x80008084;

    void *method = NULL;
    rc = ((load_assembly_and_get_function_pointer_fn)load)(
        a->dll, a->type_name, a->method_name, NULL, NULL, &method);
    if (rc != 0 || !method)
        return rc ? (DWORD)rc : 0x80008084;

    int32_t bytes = (int32_t)(wlen(a->command) * sizeof(wchar_t));
    return (DWORD)((component_entry_point_fn)method)(a->command, bytes);
}
