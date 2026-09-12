#pragma once

#ifdef _WIN32
#include <wchar.h>

typedef struct ManiacLoadArgs
{
    wchar_t hostfxr[1024];
    wchar_t runtimeconfig[1024];
    wchar_t dll[1024];
    wchar_t type_name[256];
    wchar_t method_name[64];
    wchar_t command[32];
} ManiacLoadArgs;
#endif
