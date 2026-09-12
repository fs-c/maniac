#include "maniac.h"
#include "sha256.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#else
#include <dirent.h>
#endif

#ifdef MANIAC_EMBED
#ifndef _WIN32
extern const unsigned char maniac_payload[];
extern const unsigned int maniac_payload_size;
#endif
#endif

static int cmp_name(const void *a, const void *b)
{
    const PayloadFile *fa = a;
    const PayloadFile *fb = b;
    return strcmp(fa->name, fb->name);
}

int payload_hash(const PayloadFile *files, int count, char hex[65])
{
    PayloadFile *sorted = malloc((size_t)count * sizeof *sorted);
    if (!sorted)
        return 0;
    memcpy(sorted, files, (size_t)count * sizeof *sorted);
    qsort(sorted, (size_t)count, sizeof *sorted, cmp_name);

    Sha256 s;
    sha256_init(&s);
    for (int i = 0; i < count; i++)
    {
        uint32_t len = sorted[i].size;
        uint8_t le[4] = {(uint8_t)len, (uint8_t)(len >> 8), (uint8_t)(len >> 16), (uint8_t)(len >> 24)};
        sha256_update(&s, sorted[i].name, strlen(sorted[i].name));
        sha256_update(&s, le, 4);
        sha256_update(&s, sorted[i].bytes, sorted[i].size);
    }
    uint8_t dig[32];
    sha256_final(&s, dig);
    free(sorted);
    static const char *hexd = "0123456789abcdef";
    for (int i = 0; i < 32; i++)
    {
        hex[i * 2] = hexd[dig[i] >> 4];
        hex[i * 2 + 1] = hexd[dig[i] & 0xf];
    }
    hex[64] = 0;
    return 1;
}

static int write_file(const char *path, const uint8_t *bytes, uint32_t size)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return 0;
    HANDLE h = CreateFileW(w, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return 0;
    DWORD put = 0;
    BOOL ok = WriteFile(h, bytes, size, &put, NULL);
    CloseHandle(h);
    return ok && put == size;
#else
    FILE *f = fopen(path, "wb");
    if (!f)
        return 0;
    size_t put = fwrite(bytes, 1, size, f);
    fclose(f);
    return put == size;
#endif
}

static uint32_t file_size(const char *path)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return (uint32_t)-1;
    WIN32_FILE_ATTRIBUTE_DATA d;
    if (!GetFileAttributesExW(w, GetFileExInfoStandard, &d) || d.nFileSizeHigh)
        return (uint32_t)-1;
    return d.nFileSizeLow;
#else
    FILE *f = fopen(path, "rb");
    if (!f)
        return (uint32_t)-1;
    if (fseek(f, 0, SEEK_END) != 0)
    {
        fclose(f);
        return (uint32_t)-1;
    }
    long n = ftell(f);
    fclose(f);
    return n < 0 ? (uint32_t)-1 : (uint32_t)n;
#endif
}

static int skip_sidecar_name(const char *name)
{
    const char *dot = strrchr(name, '.');
    if (!dot)
        return 0;
    return strcmp(dot, ".exp") == 0 || strcmp(dot, ".lib") == 0 ||
           strcmp(dot, ".ilk") == 0 || strcmp(dot, ".obj") == 0;
}

static uint8_t *read_file(const char *path, uint32_t *size)
{
#ifdef _WIN32
    wchar_t w[MANIAC_PATH_MAX];
    if (!utf8_to_wide(path, w, MANIAC_PATH_MAX))
        return NULL;
    HANDLE h = CreateFileW(w, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return NULL;
    DWORD hi = 0;
    DWORD lo = GetFileSize(h, &hi);
    if (lo == INVALID_FILE_SIZE || hi)
    {
        CloseHandle(h);
        return NULL;
    }
    uint8_t *buf = malloc(lo ? lo : 1);
    if (!buf)
    {
        CloseHandle(h);
        return NULL;
    }
    DWORD got = 0;
    BOOL ok = ReadFile(h, buf, lo, &got, NULL);
    CloseHandle(h);
    if (!ok || got != lo)
    {
        free(buf);
        return NULL;
    }
    *size = lo;
    return buf;
#else
    FILE *f = fopen(path, "rb");
    if (!f)
        return NULL;
    if (fseek(f, 0, SEEK_END) != 0)
    {
        fclose(f);
        return NULL;
    }
    long n = ftell(f);
    if (n < 0 || fseek(f, 0, SEEK_SET) != 0)
    {
        fclose(f);
        return NULL;
    }
    uint8_t *buf = malloc(n ? (size_t)n : 1);
    if (!buf)
    {
        fclose(f);
        return NULL;
    }
    size_t got = fread(buf, 1, (size_t)n, f);
    fclose(f);
    if (got != (size_t)n)
    {
        free(buf);
        return NULL;
    }
    *size = (uint32_t)n;
    return buf;
#endif
}

static void free_files(PayloadFile *files, int count, int free_bytes)
{
    if (!files)
        return;
    for (int i = 0; i < count; i++)
    {
        free((void *)files[i].name);
        if (free_bytes)
            free((void *)files[i].bytes);
    }
    free(files);
}

static int load_dir(const char *dir, PayloadFile **out_files, int *out_count)
{
    PayloadFile *files = calloc(64, sizeof *files);
    if (!files)
        return 0;
    int count = 0;
#ifdef _WIN32
    char pattern[MANIAC_PATH_MAX];
    path_join(pattern, sizeof pattern, dir, "*");
    wchar_t wpat[MANIAC_PATH_MAX];
    if (!utf8_to_wide(pattern, wpat, MANIAC_PATH_MAX))
    {
        free(files);
        return 0;
    }
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(wpat, &fd);
    if (h == INVALID_HANDLE_VALUE)
    {
        free(files);
        return 0;
    }
    do
    {
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
            continue;
        char name[MAX_PATH];
        if (!wide_to_utf8(fd.cFileName, name, sizeof name) || skip_sidecar_name(name))
            continue;
        if (count >= 64)
        {
            FindClose(h);
            free_files(files, count, 1);
            return 0;
        }
        char path[MANIAC_PATH_MAX];
        path_join(path, sizeof path, dir, name);
        uint32_t size = 0;
        uint8_t *bytes = read_file(path, &size);
        if (!bytes)
        {
            FindClose(h);
            free_files(files, count, 1);
            return 0;
        }
        char *owned = malloc(strlen(name) + 1);
        if (!owned)
        {
            free(bytes);
            FindClose(h);
            free_files(files, count, 1);
            return 0;
        }
        memcpy(owned, name, strlen(name) + 1);
        files[count].name = owned;
        files[count].bytes = bytes;
        files[count].size = size;
        count++;
    } while (FindNextFileW(h, &fd));
    FindClose(h);
#else
    DIR *d = opendir(dir);
    if (!d)
    {
        free(files);
        return 0;
    }
    struct dirent *e;
    while ((e = readdir(d)))
    {
        if (e->d_name[0] == '.')
            continue;
        if (skip_sidecar_name(e->d_name))
            continue;
        char path[MANIAC_PATH_MAX];
        path_join(path, sizeof path, dir, e->d_name);
        if (!path_exists(path))
            continue;
        if (count >= 64)
        {
            closedir(d);
            free_files(files, count, 1);
            return 0;
        }
        uint32_t size = 0;
        uint8_t *bytes = read_file(path, &size);
        if (!bytes)
        {
            closedir(d);
            free_files(files, count, 1);
            return 0;
        }
        char *owned = malloc(strlen(e->d_name) + 1);
        if (!owned)
        {
            free(bytes);
            closedir(d);
            free_files(files, count, 1);
            return 0;
        }
        memcpy(owned, e->d_name, strlen(e->d_name) + 1);
        files[count].name = owned;
        files[count].bytes = bytes;
        files[count].size = size;
        count++;
    }
    closedir(d);
#endif
    *out_files = files;
    *out_count = count;
    return count > 0;
}

static int complete(const PayloadFile *files, int count, const char *dest)
{
    for (int i = 0; i < count; i++)
    {
        char path[MANIAC_PATH_MAX];
        path_join(path, sizeof path, dest, files[i].name);
        if (file_size(path) != files[i].size)
            return 0;
    }
    return count > 0;
}

static int extract(const PayloadFile *files, int count, const char *dest)
{
    if (!make_dirs(dest))
        return 0;
    if (complete(files, count, dest))
        return 1;
    for (int i = 0; i < count; i++)
    {
        char path[MANIAC_PATH_MAX];
        path_join(path, sizeof path, dest, files[i].name);
        if (!write_file(path, files[i].bytes, files[i].size))
            return 0;
    }
    return 1;
}

static int install_payload(PayloadFile *files, int count, char *out, size_t cap, int free_bytes)
{
    char hex[65];
    if (!payload_hash(files, count, hex))
    {
        free_files(files, count, free_bytes);
        return 0;
    }
    char root[MANIAC_PATH_MAX];
    payload_root_path(root, sizeof root);
    path_join(out, cap, root, hex);
    int ok = extract(files, count, out);
    free_files(files, count, free_bytes);
    return ok;
}

#ifdef MANIAC_EMBED
static uint32_t rd_u32(const uint8_t *p)
{
    return (uint32_t)p[0] | ((uint32_t)p[1] << 8) | ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24);
}

static uint16_t rd_u16(const uint8_t *p)
{
    return (uint16_t)(p[0] | (p[1] << 8));
}

static int parse_archive(const uint8_t *data, uint32_t size, PayloadFile **out_files, int *out_count)
{
    if (size < 8 || memcmp(data, "MNP1", 4) != 0)
        return 0;
    uint32_t count = rd_u32(data + 4);
    const uint8_t *p = data + 8;
    const uint8_t *end = data + size;
    PayloadFile *files = calloc(count ? count : 1, sizeof *files);
    if (!files)
        return 0;
    for (uint32_t i = 0; i < count; i++)
    {
        if (p + 6 > end)
        {
            free(files);
            return 0;
        }
        uint16_t name_len = rd_u16(p);
        uint32_t data_len = rd_u32(p + 2);
        p += 6;
        if (p + name_len + data_len > end)
        {
            free(files);
            return 0;
        }
        char *name = malloc(name_len + 1);
        if (!name)
        {
            free(files);
            return 0;
        }
        memcpy(name, p, name_len);
        name[name_len] = 0;
        p += name_len;
        files[i].name = name;
        files[i].bytes = p;
        files[i].size = data_len;
        p += data_len;
    }
    *out_files = files;
    *out_count = (int)count;
    return 1;
}

#ifdef _WIN32
static int load_embedded(uint8_t **data, uint32_t *size)
{
    HRSRC r = FindResourceW(NULL, MAKEINTRESOURCEW(1), RT_RCDATA);
    if (!r)
        return 0;
    HGLOBAL g = LoadResource(NULL, r);
    if (!g)
        return 0;
    DWORD n = SizeofResource(NULL, r);
    void *p = LockResource(g);
    if (!p || !n)
        return 0;
    *data = p;
    *size = n;
    return 1;
}
#else
static int load_embedded(uint8_t **data, uint32_t *size)
{
    if (maniac_payload_size < 8)
        return 0;
    *data = (uint8_t *)maniac_payload;
    *size = maniac_payload_size;
    return 1;
}
#endif
#endif

int payload_resolve(const char *base_dir, char *out, size_t cap, const char **error)
{
    char sidecar[MANIAC_PATH_MAX];
    char dll[MANIAC_PATH_MAX];
    path_join(sidecar, sizeof sidecar, base_dir, "plugin");
    path_join(dll, sizeof dll, sidecar, "Maniac.dll");
    if (path_exists(dll))
    {
        PayloadFile *files = NULL;
        int count = 0;
        if (!load_dir(sidecar, &files, &count) || !install_payload(files, count, out, cap, 1))
        {
            *error = MSG_INCOMPLETE;
            return 0;
        }
        return 1;
    }

#ifdef MANIAC_EMBED
    uint8_t *blob = NULL;
    uint32_t blob_size = 0;
    if (!load_embedded(&blob, &blob_size))
    {
        *error = MSG_INCOMPLETE;
        return 0;
    }
    PayloadFile *files = NULL;
    int count = 0;
    if (!parse_archive(blob, blob_size, &files, &count) || count == 0)
    {
        *error = MSG_INCOMPLETE;
        return 0;
    }
    if (!install_payload(files, count, out, cap, 0))
    {
        *error = MSG_INCOMPLETE;
        return 0;
    }
    return 1;
#else
    (void)out;
    (void)cap;
    *error = MSG_INCOMPLETE;
    return 0;
#endif
}
