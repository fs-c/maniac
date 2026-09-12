#include "maniac.h"

#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <tlhelp32.h>
#include <psapi.h>

static int is_osu_name(const wchar_t *name)
{
    return _wcsicmp(name, L"osu!") == 0 || _wcsicmp(name, L"osu") == 0;
}

static int *g_pids;
static int g_cap;
static int *g_count;

static BOOL CALLBACK enum_wnd(HWND hwnd, LPARAM lp)
{
    (void)lp;
    if (!IsWindowVisible(hwnd))
        return TRUE;
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (!pid)
        return TRUE;
    HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!h)
        return TRUE;
    wchar_t path[MANIAC_PATH_MAX];
    DWORD n = MANIAC_PATH_MAX;
    int ok = QueryFullProcessImageNameW(h, 0, path, &n);
    CloseHandle(h);
    if (!ok)
        return TRUE;
    wchar_t *slash = wcsrchr(path, L'\\');
    wchar_t *name = slash ? slash + 1 : path;
    wchar_t *dot = wcsrchr(name, L'.');
    wchar_t saved = 0;
    if (dot)
    {
        saved = *dot;
        *dot = 0;
    }
    int match = is_osu_name(name);
    if (dot)
        *dot = saved;
    if (!match)
        return TRUE;
    for (int i = 0; i < *g_count; i++)
    {
        if (g_pids[i] == (int)pid)
            return TRUE;
    }
    if (*g_count < g_cap)
        g_pids[(*g_count)++] = (int)pid;
    return TRUE;
}

static int window_pids(int *pids, int cap, int *count)
{
    g_pids = pids;
    g_cap = cap;
    g_count = count;
    *count = 0;
    EnumWindows(enum_wnd, 0);
    return 1;
}

static int snapshot_pids(int *pids, int cap, int *count)
{
    *count = 0;
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE)
        return 0;
    PROCESSENTRY32W pe;
    pe.dwSize = sizeof pe;
    if (Process32FirstW(snap, &pe))
    {
        do
        {
            wchar_t name[MAX_PATH];
            wcsncpy(name, pe.szExeFile, MAX_PATH);
            name[MAX_PATH - 1] = 0;
            wchar_t *dot = wcsrchr(name, L'.');
            if (dot)
                *dot = 0;
            if (!is_osu_name(name))
                continue;
            if (*count < cap)
                pids[(*count)++] = (int)pe.th32ProcessID;
        } while (Process32NextW(snap, &pe));
    }
    CloseHandle(snap);
    return 1;
}

int osu_image_path(int pid, char *out, size_t cap)
{
    HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!h)
    {
        out[0] = 0;
        return 0;
    }
    wchar_t w[MANIAC_PATH_MAX];
    DWORD n = MANIAC_PATH_MAX;
    int ok = QueryFullProcessImageNameW(h, 0, w, &n);
    CloseHandle(h);
    if (!ok)
    {
        out[0] = 0;
        return 0;
    }
    return wide_to_utf8(w, out, cap);
}

int osu_find(int requested_pid, int *pid_out, char *name_out, size_t name_cap, const char **error)
{
    if (requested_pid)
    {
        HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, (DWORD)requested_pid);
        if (!h)
        {
            *error = NULL; /* caller formats process-not-found */
            return 0;
        }
        CloseHandle(h);
        *pid_out = requested_pid;
        char path[MANIAC_PATH_MAX];
        if (osu_image_path(requested_pid, path, sizeof path))
        {
            const char *slash = strrchr(path, '\\');
            snprintf(name_out, name_cap, "%s", slash ? slash + 1 : path);
        }
        else
            snprintf(name_out, name_cap, "pid-%d", requested_pid);
        return 1;
    }

    int pids[64];
    int count = 0;
    window_pids(pids, 64, &count);
    if (count == 1)
    {
        *pid_out = pids[0];
    }
    else if (count > 1)
    {
        *error = MSG_MULTIPLE_OSU;
        return 0;
    }
    else
    {
        snapshot_pids(pids, 64, &count);
        if (count == 0)
        {
            *error = MSG_OSU_NOT_RUNNING;
            return 0;
        }
        if (count > 1)
        {
            *error = MSG_MULTIPLE_OSU;
            return 0;
        }
        *pid_out = pids[0];
    }

    char path[MANIAC_PATH_MAX];
    if (osu_image_path(*pid_out, path, sizeof path))
    {
        const char *slash = strrchr(path, '\\');
        snprintf(name_out, name_cap, "%s", slash ? slash + 1 : path);
    }
    else
        snprintf(name_out, name_cap, "osu!");
    return 1;
}

#else
#include <ctype.h>
#include <dirent.h>
#include <signal.h>
#include <unistd.h>
#ifdef __APPLE__
#include <libproc.h>
#include <sys/sysctl.h>
#include <sys/types.h>
#endif

static int is_osu_name(const char *name)
{
    return strcmp(name, "osu!") == 0 || strcmp(name, "osu") == 0;
}

int osu_image_path(int pid, char *out, size_t cap)
{
#ifdef __APPLE__
    int n = proc_pidpath(pid, out, (uint32_t)cap);
    if (n <= 0)
    {
        out[0] = 0;
        return 0;
    }
    if ((size_t)n >= cap)
        out[cap - 1] = 0;
    return 1;
#else
    char link[64];
    snprintf(link, sizeof link, "/proc/%d/exe", pid);
    ssize_t n = readlink(link, out, cap - 1);
    if (n <= 0)
    {
        out[0] = 0;
        return 0;
    }
    out[n] = 0;
    return 1;
#endif
}

#ifdef __APPLE__
static int unix_scan(int *pids, int cap, int *count)
{
    *count = 0;
    int mib[4] = { CTL_KERN, KERN_PROC, KERN_PROC_ALL, 0 };
    size_t len = 0;
    if (sysctl(mib, 4, NULL, &len, NULL, 0) < 0)
        return 0;
    len += sizeof(struct kinfo_proc) * 32;
    struct kinfo_proc *procs = malloc(len);
    if (!procs)
        return 0;
    if (sysctl(mib, 4, procs, &len, NULL, 0) < 0)
    {
        free(procs);
        return 0;
    }
    int n = (int)(len / sizeof *procs);
    for (int i = 0; i < n; i++)
    {
        if (!is_osu_name(procs[i].kp_proc.p_comm))
            continue;
        if (*count < cap)
            pids[(*count)++] = (int)procs[i].kp_proc.p_pid;
    }
    free(procs);
    return 1;
}
#else
static int unix_scan(int *pids, int cap, int *count)
{
    *count = 0;
    DIR *d = opendir("/proc");
    if (!d)
        return 0;
    struct dirent *e;
    while ((e = readdir(d)))
    {
        if (!isdigit((unsigned char)e->d_name[0]))
            continue;
        int pid = atoi(e->d_name);
        char comm_path[64];
        snprintf(comm_path, sizeof comm_path, "/proc/%d/comm", pid);
        FILE *f = fopen(comm_path, "r");
        if (!f)
            continue;
        char comm[256];
        if (!fgets(comm, sizeof comm, f))
        {
            fclose(f);
            continue;
        }
        fclose(f);
        size_t n = strlen(comm);
        while (n > 0 && (comm[n - 1] == '\n' || comm[n - 1] == '\r'))
            comm[--n] = 0;
        if (!is_osu_name(comm))
            continue;
        if (*count < cap)
            pids[(*count)++] = pid;
    }
    closedir(d);
    return 1;
}
#endif

int osu_find(int requested_pid, int *pid_out, char *name_out, size_t name_cap, const char **error)
{
    if (requested_pid)
    {
        char path[MANIAC_PATH_MAX];
        if (!osu_image_path(requested_pid, path, sizeof path) && kill(requested_pid, 0) != 0)
        {
            *error = NULL;
            return 0;
        }
        *pid_out = requested_pid;
        const char *slash = strrchr(path, '/');
        snprintf(name_out, name_cap, "%s", slash ? slash + 1 : (path[0] ? path : "osu!"));
        return 1;
    }
    int pids[64];
    int count = 0;
    unix_scan(pids, 64, &count);
    if (count == 0)
    {
        *error = MSG_OSU_NOT_RUNNING;
        return 0;
    }
    if (count > 1)
    {
        *error = MSG_MULTIPLE_OSU;
        return 0;
    }
    *pid_out = pids[0];
    char path[MANIAC_PATH_MAX];
    if (osu_image_path(*pid_out, path, sizeof path))
    {
        const char *slash = strrchr(path, '/');
        snprintf(name_out, name_cap, "%s", slash ? slash + 1 : path);
    }
    else
        snprintf(name_out, name_cap, "osu!");
    return 1;
}
#endif

int osu_install_from_pid(int pid, char *out, size_t cap)
{
    char path[MANIAC_PATH_MAX];
    if (!osu_image_path(pid, path, sizeof path))
        return 0;
    char *slash = strrchr(path, '/');
#ifdef _WIN32
    char *bslash = strrchr(path, '\\');
    if (!slash || (bslash && bslash > slash))
        slash = bslash;
#endif
    if (!slash)
        return 0;
    *slash = 0;
    char game[MANIAC_PATH_MAX];
    path_join(game, sizeof game, path, "osu.Game.dll");
    if (!path_exists(game))
        return 0;
    snprintf(out, cap, "%s", path);
    return 1;
}

int osu_find_install(char *out, size_t cap)
{
#ifdef _WIN32
    char local[MANIAC_PATH_MAX];
    char cand[MANIAC_PATH_MAX];
    local_app_data(local, sizeof local);
    path_join(cand, sizeof cand, local, "osulazer\\current");
    path_join(out, cap, cand, "osu.Game.dll");
    if (path_exists(out))
    {
        snprintf(out, cap, "%s", cand);
        return 1;
    }
    path_join(cand, sizeof cand, local, "osu!lazer\\current");
    path_join(out, cap, cand, "osu.Game.dll");
    if (path_exists(out))
    {
        snprintf(out, cap, "%s", cand);
        return 1;
    }
#else
    char home[MANIAC_PATH_MAX];
    const char *h = getenv("HOME");
    snprintf(home, sizeof home, "%s", h ? h : "");
    char mac_user[MANIAC_PATH_MAX];
    snprintf(mac_user, sizeof mac_user, "%s/Applications/osu!.app/Contents/MacOS", home);
    char linux_a[MANIAC_PATH_MAX];
    char linux_b[MANIAC_PATH_MAX];
    snprintf(linux_a, sizeof linux_a, "%s/.local/share/osulazer/current", home);
    snprintf(linux_b, sizeof linux_b, "%s/.local/share/osu!lazer/current", home);
    const char *list[] = {
        "/Applications/osu!.app/Contents/MacOS",
        mac_user,
        linux_a,
        linux_b,
    };
    for (size_t i = 0; i < sizeof list / sizeof list[0]; i++)
    {
        char game[MANIAC_PATH_MAX];
        path_join(game, sizeof game, list[i], "osu.Game.dll");
        if (path_exists(game))
        {
            snprintf(out, cap, "%s", list[i]);
            return 1;
        }
    }
#endif
    out[0] = 0;
    return 0;
}

int osu_read_version(const char *game_dir, char *out, size_t cap)
{
    char deps[MANIAC_PATH_MAX];
    path_join(deps, sizeof deps, game_dir, "osu!.deps.json");
    FILE *f = fopen(deps, "r");
    if (f)
    {
        char buf[4096];
        while (fgets(buf, sizeof buf, f))
        {
            char *p = strstr(buf, "ppy.osu.Game/");
            if (!p)
                continue;
            p += strlen("ppy.osu.Game/");
            size_t n = 0;
            while (p[n] && (isalnum((unsigned char)p[n]) || p[n] == '.' || p[n] == '-'))
                n++;
            if (n)
            {
                if (n >= cap)
                    n = cap - 1;
                memcpy(out, p, n);
                out[n] = 0;
                fclose(f);
                return 1;
            }
        }
        fclose(f);
    }
    out[0] = 0;
    return 0;
}
