#pragma once

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wchar.h>
#endif

#define MANIAC_PATH_MAX 4096
#define MANIAC_TYPE_NAME "Maniac.Entry, Maniac"
#define MANIAC_METHOD_NAME "Bootstrap"
#define MANIAC_STUB_DLL "maniac-stub.dll"

#define MSG_INCOMPLETE "This copy of maniac is incomplete."
#define MSG_OSU_NOT_RUNNING "osu!lazer is not running."
#define MSG_MULTIPLE_OSU "Several osu! instances are running. Close the extras, or pass --pid <id>."
#define MSG_INJECT_FAILED "Could not load into osu!."
#define MSG_PID_REQUIRED "maniac --pid needs a process id."
#define MSG_HELP \
    "maniac - load into a running osu!lazer\n" \
    "\n" \
    "Usage:\n" \
    "  maniac              load or reload\n" \
    "  maniac --unload     remove\n" \
    "  maniac --pid <id>   pick a process\n" \
    "  maniac --verbose    extra detail\n"

extern int g_verbose;

void verbose(const char *fmt, ...);
void path_join(char *out, size_t cap, const char *a, const char *b);
int path_exists(const char *path);
int dir_exists(const char *path);
int make_dirs(const char *path);
void local_app_data(char *out, size_t cap);
void temp_dir(char *out, size_t cap);
void bootstrap_log_path(char *out, size_t cap);
void payload_root_path(char *out, size_t cap);
int utf8_to_wide(const char *utf8, wchar_t *wide, size_t wide_cap);
int wide_to_utf8(const wchar_t *wide, char *utf8, size_t utf8_cap);

typedef struct {
    int help;
    int verbose;
    const char *command; /* "reload" or "unload" */
    int pid;             /* 0 = unset */
    const char *error;   /* MSG_* if parse failed */
} CliArgs;

CliArgs cli_parse(int argc, char **argv);
void cli_success(const char *command, int pid, char *out, size_t cap);
void cli_process_not_found(int pid, char *out, size_t cap);

typedef struct {
    const char *name;
    const uint8_t *bytes;
    uint32_t size;
} PayloadFile;

int payload_hash(const PayloadFile *files, int count, char hex[65]);
int payload_resolve(const char *base_dir, char *out, size_t cap, const char **error);

int expiry_is_expired(const char *stamp, char *message, size_t cap);

int osu_find(int requested_pid, int *pid_out, char *name_out, size_t name_cap, const char **error);
int osu_image_path(int pid, char *out, size_t cap);
int osu_install_from_pid(int pid, char *out, size_t cap);
int osu_find_install(char *out, size_t cap);
int osu_read_version(const char *game_dir, char *out, size_t cap);

int inject(int pid, const char *plugin_dir, const char *dll, const char *command, const char **detail);

#ifdef _WIN32
int inject_win(int pid, const char *plugin_dir, const char *dll, const char *command, const char **detail);
#else
int inject_diag(int pid, const char *dll, const char *command, const char **detail);
#endif
