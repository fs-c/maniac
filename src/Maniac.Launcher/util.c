#include "maniac.h"

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <shlobj.h>
#include <knownfolders.h>
#else
#include <errno.h>
#include <sys/stat.h>
#include <unistd.h>
#endif

int g_verbose;

void verbose(const char *fmt, ...)
{
    if (!g_verbose)
        return;
    va_list ap;
    va_start(ap, fmt);
    vprintf(fmt, ap);
    va_end(ap);
    printf("\n");
}

void path_join(char *out, size_t cap, const char *a, const char *b)
{
    size_t n = strlen(a);
    while (n > 0 && (a[n - 1] == '/' || a[n - 1] == '\\'))
        n--;
    snprintf(out, cap, "%.*s/%s", (int)n, a, b);
#ifdef _WIN32
    for (char *p = out; *p; p++)
    {
        if (*p == '/')
            *p = '\\';
    }
#endif
}

int path_exists(const char *path)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return 0;
    DWORD a = GetFileAttributesW(w);
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
#else
    return access(path, F_OK) == 0;
#endif
}

int dir_exists(const char *path)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return 0;
    DWORD a = GetFileAttributesW(w);
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
#else
    struct stat st;
    return stat(path, &st) == 0 && S_ISDIR(st.st_mode);
#endif
}

static int mkdir_one(const char *path)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return 0;
    return CreateDirectoryW(w, NULL) || GetLastError() == ERROR_ALREADY_EXISTS;
#else
    return mkdir(path, 0755) == 0 || errno == EEXIST;
#endif
}

int make_dirs(const char *path)
{
    char tmp[MANIAC_PATH_MAX];
    snprintf(tmp, sizeof tmp, "%s", path);
    size_t n = strlen(tmp);
    if (n == 0)
        return 0;
    for (size_t i = 1; i < n; i++)
    {
        if (tmp[i] == '/' || tmp[i] == '\\')
        {
            char c = tmp[i];
            tmp[i] = 0;
            if (!dir_exists(tmp) && !mkdir_one(tmp))
                return 0;
            tmp[i] = c;
        }
    }
    return dir_exists(tmp) || mkdir_one(tmp);
}

#ifdef _WIN32
int utf8_to_wide(const char *utf8, wchar_t *wide, size_t wide_cap)
{
    int n = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, wide, (int)wide_cap);
    return n > 0;
}

int wide_to_utf8(const wchar_t *wide, char *utf8, size_t utf8_cap)
{
    int n = WideCharToMultiByte(CP_UTF8, 0, wide, -1, utf8, (int)utf8_cap, NULL, NULL);
    return n > 0;
}

void local_app_data(char *out, size_t cap)
{
    PWSTR w = NULL;
    if (SUCCEEDED(SHGetKnownFolderPath(&FOLDERID_LocalAppData, 0, NULL, &w)))
    {
        wide_to_utf8(w, out, cap);
        CoTaskMemFree(w);
        return;
    }
    snprintf(out, cap, ".");
}

void temp_dir(char *out, size_t cap)
{
    wchar_t w[MANIAC_PATH_MAX];
    DWORD n = GetTempPathW(MANIAC_PATH_MAX, w);
    if (n == 0 || n >= MANIAC_PATH_MAX)
    {
        snprintf(out, cap, ".");
        return;
    }
    wide_to_utf8(w, out, cap);
    size_t len = strlen(out);
    while (len > 0 && (out[len - 1] == '\\' || out[len - 1] == '/'))
        out[--len] = 0;
}
#else
int utf8_to_wide(const char *utf8, wchar_t *wide, size_t wide_cap)
{
    (void)utf8;
    (void)wide;
    (void)wide_cap;
    return 0;
}

int wide_to_utf8(const wchar_t *wide, char *utf8, size_t utf8_cap)
{
    (void)wide;
    (void)utf8;
    (void)utf8_cap;
    return 0;
}

void local_app_data(char *out, size_t cap)
{
    const char *home = getenv("HOME");
    if (home && home[0])
    {
#ifdef __APPLE__
        snprintf(out, cap, "%s/Library/Application Support", home);
#else
        snprintf(out, cap, "%s/.local/share", home);
#endif
        return;
    }
    snprintf(out, cap, ".");
}

void temp_dir(char *out, size_t cap)
{
    const char *t = getenv("TMPDIR");
    if (!t || !t[0])
        t = "/tmp";
    snprintf(out, cap, "%s", t);
}
#endif

void bootstrap_log_path(char *out, size_t cap)
{
    char tmp[MANIAC_PATH_MAX];
    temp_dir(tmp, sizeof tmp);
    snprintf(out, cap, "%s/maniac-lazer/bootstrap.log", tmp);
#ifdef _WIN32
    for (char *p = out; *p; p++)
        if (*p == '/')
            *p = '\\';
#endif
}

void payload_root_path(char *out, size_t cap)
{
    char local[MANIAC_PATH_MAX];
    local_app_data(local, sizeof local);
    path_join(out, cap, local, "maniac-lazer/payload");
}
