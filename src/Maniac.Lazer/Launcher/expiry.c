#include "maniac.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#ifndef MANIAC_BUILD_EXPIRY
#define MANIAC_BUILD_EXPIRY ""
#endif

static int is_digit(char c) { return c >= '0' && c <= '9'; }

static int parse_int(const char *s, int n)
{
    int v = 0;
    for (int i = 0; i < n; i++)
        v = v * 10 + (s[i] - '0');
    return v;
}

/* Returns 1 on success. stamp is UTC. */
static int parse_stamp(const char *raw, time_t *out)
{
    size_t n = strlen(raw);
    if (n < 10 || !is_digit(raw[0]) || raw[4] != '-' || raw[7] != '-')
        return 0;

    struct tm t;
    memset(&t, 0, sizeof t);
    t.tm_year = parse_int(raw, 4) - 1900;
    t.tm_mon = parse_int(raw + 5, 2) - 1;
    t.tm_mday = parse_int(raw + 8, 2);
    t.tm_isdst = 0;
    int offset_min = 0;

    if (n == 10)
    {
        /* yyyy-MM-dd → UTC midnight */
    }
    else if (n >= 16 && raw[10] == 'T')
    {
        if (!is_digit(raw[11]) || raw[13] != ':')
            return 0;
        t.tm_hour = parse_int(raw + 11, 2);
        t.tm_min = parse_int(raw + 14, 2);
        const char *tz = raw + 16;
        if (n >= 19 && raw[16] == ':')
        {
            t.tm_sec = parse_int(raw + 17, 2);
            tz = raw + 19;
        }
        if (*tz == 'Z' && tz[1] == 0)
            offset_min = 0;
        else if (*tz == '+' || *tz == '-')
        {
            int sign = *tz == '-' ? 1 : -1; /* apply opposite to reach UTC */
            tz++;
            if (!(is_digit(tz[0]) && is_digit(tz[1]) && tz[2] == ':' && is_digit(tz[3]) && is_digit(tz[4]) && tz[5] == 0))
                return 0;
            offset_min = sign * (parse_int(tz, 2) * 60 + parse_int(tz + 3, 2));
        }
        else if (*tz != 0)
            return 0;
    }
    else
        return 0;

#ifdef _WIN32
    time_t utc = _mkgmtime(&t);
#else
    time_t utc = timegm(&t);
#endif
    if (utc == (time_t)-1)
        return 0;
    *out = utc + offset_min * 60;
    return 1;
}

static void format_utc(time_t t, char *out, size_t cap)
{
    struct tm tm;
#ifdef _WIN32
    gmtime_s(&tm, &t);
#else
    gmtime_r(&t, &tm);
#endif
    snprintf(out, cap, "%04d-%02d-%02d %02d:%02d UTC",
        tm.tm_year + 1900, tm.tm_mon + 1, tm.tm_mday, tm.tm_hour, tm.tm_min);
}

int expiry_is_expired(const char *stamp, char *message, size_t cap)
{
    if (!stamp)
        stamp = MANIAC_BUILD_EXPIRY;
    while (*stamp == ' ' || *stamp == '\t')
        stamp++;
    if (!stamp[0])
    {
        message[0] = 0;
        return 0;
    }

    time_t end;
    if (!parse_stamp(stamp, &end))
    {
        snprintf(message, cap, "This build has an invalid expiry stamp and will not run.");
        return 1;
    }

    time_t now = time(NULL);
    char when[64];
    format_utc(end, when, sizeof when);
    if (now >= end)
    {
        snprintf(message, cap, "This build expired on %s.", when);
        return 1;
    }
    snprintf(message, cap, "This build ends %s.", when);
    return 0;
}
