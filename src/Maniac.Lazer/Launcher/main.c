#include "maniac.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef _WIN32
#include <unistd.h>
#endif

#ifdef _WIN32
#include <shellapi.h>

static char **utf8_args(int *argc)
{
    int wargc = 0;
    LPWSTR *wargv = CommandLineToArgvW(GetCommandLineW(), &wargc);
    if (!wargv)
        return NULL;
    char **argv = calloc((size_t)wargc + 1, sizeof *argv);
    if (!argv)
    {
        LocalFree(wargv);
        return NULL;
    }
    for (int i = 0; i < wargc; i++)
    {
        int n = WideCharToMultiByte(CP_UTF8, 0, wargv[i], -1, NULL, 0, NULL, NULL);
        argv[i] = malloc((size_t)n);
        if (!argv[i])
        {
            LocalFree(wargv);
            return NULL;
        }
        WideCharToMultiByte(CP_UTF8, 0, wargv[i], -1, argv[i], n, NULL, NULL);
    }
    LocalFree(wargv);
    *argc = wargc;
    return argv;
}

static int process_alive(int pid)
{
    HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, (DWORD)pid);
    if (!h)
        return 0;
    DWORD code = 0;
    int alive = GetExitCodeProcess(h, &code) && code == STILL_ACTIVE;
    CloseHandle(h);
    return alive;
}
#endif

static void print_error(const char *user, const char *detail, int show_log)
{
    fprintf(stderr, "%s\n", user);
    if (g_verbose && detail && detail[0] && strcmp(detail, user) != 0)
        fprintf(stderr, "%s\n", detail);
    if (show_log)
    {
        char log[MANIAC_PATH_MAX];
        bootstrap_log_path(log, sizeof log);
        if (path_exists(log))
            fprintf(stderr, "Details: %s\n", log);
    }
}

int main(int argc, char **argv)
{
#ifdef _WIN32
    argv = utf8_args(&argc);
    if (!argv)
    {
        fprintf(stderr, "%s\n", MSG_INCOMPLETE);
        return 1;
    }
#endif

    CliArgs cli = cli_parse(argc, argv);
    if (cli.error)
    {
        fprintf(stderr, "%s\n", cli.error);
        return 1;
    }
    if (cli.help)
    {
        fputs(MSG_HELP, stdout);
        return 0;
    }
    g_verbose = cli.verbose;

    if (strcmp(cli.command, "unload") != 0)
    {
        char msg[256];
        if (expiry_is_expired(NULL, msg, sizeof msg) && msg[0])
        {
            fprintf(stderr, "%s\n", msg);
            return 1;
        }
    }

    int pid = 0;
    char proc_name[256];
    const char *err = NULL;
    if (!osu_find(cli.pid, &pid, proc_name, sizeof proc_name, &err))
    {
        if (!err && cli.pid)
        {
            char buf[64];
            cli_process_not_found(cli.pid, buf, sizeof buf);
            fprintf(stderr, "%s\n", buf);
            return 1;
        }
        fprintf(stderr, "%s\n", err ? err : MSG_OSU_NOT_RUNNING);
        return 1;
    }

    char base[MANIAC_PATH_MAX];
#ifdef _WIN32
    wchar_t wexe[MANIAC_PATH_MAX];
    if (!GetModuleFileNameW(NULL, wexe, MANIAC_PATH_MAX))
    {
        print_error(MSG_INCOMPLETE, NULL, 0);
        return 1;
    }
    wchar_t *slash = wcsrchr(wexe, L'\\');
    if (slash)
        *slash = 0;
    wide_to_utf8(wexe, base, sizeof base);
#else
    if (!osu_image_path(getpid(), base, sizeof base) || !base[0])
        snprintf(base, sizeof base, ".");
    else
    {
        char *s = strrchr(base, '/');
        if (s)
            *s = 0;
    }
#endif

    const char *payload_err = NULL;
    char plugin_dir[MANIAC_PATH_MAX];
    if (!payload_resolve(base, plugin_dir, sizeof plugin_dir, &payload_err))
    {
        print_error(MSG_INCOMPLETE, payload_err, 0);
        return 1;
    }

    char dll[MANIAC_PATH_MAX];
    path_join(dll, sizeof dll, plugin_dir, "Maniac.dll");
    if (!path_exists(dll))
    {
        print_error(MSG_INCOMPLETE, dll, 0);
        return 1;
    }

    char install[MANIAC_PATH_MAX];
    char version[128];
    int have_install = osu_install_from_pid(pid, install, sizeof install) || osu_find_install(install, sizeof install);
    version[0] = 0;
    if (have_install)
        osu_read_version(install, version, sizeof version);

    verbose("pid %d (%s)", pid, proc_name);
    if (have_install)
        verbose("lazer     %s", install);
    if (version[0])
        verbose("version   %s", version);
    verbose("plugin    %s", plugin_dir);
    verbose("command   %s", cli.command);

    const char *detail = NULL;
    if (!inject(pid, plugin_dir, dll, cli.command, &detail))
    {
#ifdef _WIN32
        if (!process_alive(pid))
        {
            char log[MANIAC_PATH_MAX];
            bootstrap_log_path(log, sizeof log);
            char buf[MANIAC_PATH_MAX + 64];
            snprintf(buf, sizeof buf, "osu! exited after inject. See %s", log);
            print_error(MSG_INJECT_FAILED, detail ? detail : buf, 1);
            return 1;
        }
#endif
        print_error(MSG_INJECT_FAILED, detail, 1);
        return 1;
    }

#ifndef _WIN32
    char log[MANIAC_PATH_MAX];
    bootstrap_log_path(log, sizeof log);
    if (path_exists(log))
        verbose("details   %s", log);
#endif

    char ok[80];
    cli_success(cli.command, pid, ok, sizeof ok);
    printf("%s\n", ok);
    return 0;
}
