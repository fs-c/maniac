#ifdef _WIN32
typedef int maniac_inject_diag_unused;
#else

#include "maniac.h"

#include <dirent.h>
#include <errno.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>

static char g_detail[512];

static void wr_u16(uint8_t *p, uint16_t v)
{
    p[0] = (uint8_t)v;
    p[1] = (uint8_t)(v >> 8);
}

static void wr_u32(uint8_t *p, uint32_t v)
{
    p[0] = (uint8_t)v;
    p[1] = (uint8_t)(v >> 8);
    p[2] = (uint8_t)(v >> 16);
    p[3] = (uint8_t)(v >> 24);
}

/* string = uint32 wchar-count including NUL, then UTF-16LE including NUL */
static uint8_t *wr_str(uint8_t *p, const char *utf8)
{
    size_t n = 0;
    while (utf8[n])
        n++;
    wr_u32(p, (uint32_t)(n + 1));
    p += 4;
    for (size_t i = 0; i < n; i++)
    {
        p[0] = (uint8_t)utf8[i];
        p[1] = 0;
        p += 2;
    }
    p[0] = 0;
    p[1] = 0;
    return p + 2;
}

static int write_all(int fd, const void *buf, size_t n)
{
    const uint8_t *p = buf;
    while (n)
    {
        ssize_t w = write(fd, p, n);
        if (w <= 0)
            return 0;
        p += (size_t)w;
        n -= (size_t)w;
    }
    return 1;
}

static int read_all(int fd, void *buf, size_t n)
{
    uint8_t *p = buf;
    while (n)
    {
        ssize_t r = read(fd, p, n);
        if (r <= 0)
            return 0;
        p += (size_t)r;
        n -= (size_t)r;
    }
    return 1;
}

static int send_cmd(int fd, uint8_t set, uint8_t id, const uint8_t *payload, uint16_t payload_len)
{
    uint8_t hdr[20];
    memset(hdr, 0, sizeof hdr);
    memcpy(hdr, "DOTNET_IPC_V1", 13);
    wr_u16(hdr + 14, (uint16_t)(20 + payload_len));
    hdr[16] = set;
    hdr[17] = id;
    if (!write_all(fd, hdr, 20))
        return 0;
    if (payload_len && !write_all(fd, payload, payload_len))
        return 0;

    uint8_t rh[20];
    if (!read_all(fd, rh, 20))
        return 0;
    uint16_t sz = (uint16_t)(rh[14] | (rh[15] << 8));
    if (sz < 20)
        return 0;
    uint16_t rest = (uint16_t)(sz - 20);
    uint8_t extra[64];
    if (rest)
    {
        if (rest > sizeof extra)
            return 0;
        if (!read_all(fd, extra, rest))
            return 0;
    }
    /* command_set 0xFF, command_id 0x00 = OK */
    if (rh[16] != 0xFF || rh[17] != 0x00)
        return 0;
    if (rest >= 4)
    {
        int32_t hr = (int32_t)(extra[0] | (extra[1] << 8) | (extra[2] << 16) | (extra[3] << 24));
        return hr == 0;
    }
    return 1;
}

static int connect_socket(const char *path)
{
    int fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (fd < 0)
        return -1;
    struct sockaddr_un addr;
    memset(&addr, 0, sizeof addr);
    addr.sun_family = AF_UNIX;
    snprintf(addr.sun_path, sizeof addr.sun_path, "%s", path);
    if (connect(fd, (struct sockaddr *)&addr, sizeof addr) != 0)
    {
        close(fd);
        return -1;
    }
    return fd;
}

static int find_socket(int pid, char *out, size_t cap)
{
    const char *dirs[2];
    dirs[0] = getenv("TMPDIR");
    dirs[1] = "/tmp";
    char prefix[64];
    snprintf(prefix, sizeof prefix, "dotnet-diagnostic-%d-", pid);
    for (int d = 0; d < 2; d++)
    {
        if (!dirs[d] || !dirs[d][0])
            continue;
        DIR *dir = opendir(dirs[d]);
        if (!dir)
            continue;
        struct dirent *e;
        while ((e = readdir(dir)))
        {
            if (strncmp(e->d_name, prefix, strlen(prefix)) != 0)
                continue;
            size_t n = strlen(e->d_name);
            if (n < 7 || strcmp(e->d_name + n - 7, "-socket") != 0)
                continue;
            snprintf(out, cap, "%s/%s", dirs[d], e->d_name);
            closedir(dir);
            return 1;
        }
        closedir(dir);
    }
    return 0;
}

static int one_command(int pid, uint8_t id, const uint8_t *payload, uint16_t payload_len)
{
    char path[MANIAC_PATH_MAX];
    if (!find_socket(pid, path, sizeof path))
    {
        snprintf(g_detail, sizeof g_detail,
            "pid %d has no .NET diagnostic port. Is osu!lazer running, and is DOTNET_EnableDiagnostics enabled?",
            pid);
        return 0;
    }
    int fd = connect_socket(path);
    if (fd < 0)
    {
        snprintf(g_detail, sizeof g_detail, "diagnostic inject failed: %s", strerror(errno));
        return 0;
    }
    int ok = send_cmd(fd, 0x04, id, payload, payload_len);
    close(fd);
    if (!ok)
        snprintf(g_detail, sizeof g_detail, "diagnostic inject failed");
    return ok;
}

int inject_diag(int pid, const char *dll, const char *command, const char **detail)
{
    verbose("method    diagnostic ipc");
    uint8_t env[512];
    uint8_t *p = env;
    p = wr_str(p, "MANIAC_COMMAND");
    p = wr_str(p, command);
    if (!one_command(pid, 0x03, env, (uint16_t)(p - env)))
    {
        *detail = g_detail;
        return 0;
    }

    uint8_t hook[MANIAC_PATH_MAX * 2];
    p = hook;
    p = wr_str(p, dll);
    if (!one_command(pid, 0x07, hook, (uint16_t)(p - hook)))
    {
        *detail = g_detail;
        return 0;
    }

    usleep(400 * 1000);
    if (kill(pid, 0) != 0)
    {
        snprintf(g_detail, sizeof g_detail, "osu! exited after inject");
        *detail = g_detail;
        return 0;
    }
    return 1;
}

int inject(int pid, const char *plugin_dir, const char *dll, const char *command, const char **detail)
{
    (void)plugin_dir;
    return inject_diag(pid, dll, command, detail);
}

#endif
