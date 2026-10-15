#include "maniac.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

CliArgs cli_parse(int argc, char **argv)
{
    CliArgs a = {0, 0, "reload", 0, NULL};
    for (int i = 1; i < argc; i++)
    {
        const char *arg = argv[i];
        if (strcmp(arg, "-h") == 0 || strcmp(arg, "--help") == 0)
            a.help = 1;
        else if (strcmp(arg, "-v") == 0 || strcmp(arg, "--verbose") == 0)
            a.verbose = 1;
        else if (strcmp(arg, "--unload") == 0)
            a.command = "unload";
        else if (strcmp(arg, "--reload") == 0)
            a.command = "reload";
        else if (strcmp(arg, "--pid") == 0)
        {
            if (i + 1 >= argc)
            {
                a.error = MSG_PID_REQUIRED;
                return a;
            }
            char *end = NULL;
            long v = strtol(argv[++i], &end, 10);
            if (!end || *end || v <= 0)
            {
                a.error = MSG_PID_REQUIRED;
                return a;
            }
            a.pid = (int)v;
        }
        else if (strncmp(arg, "--pid=", 6) == 0)
        {
            char *end = NULL;
            long v = strtol(arg + 6, &end, 10);
            if (!end || *end || v <= 0)
            {
                a.error = MSG_PID_REQUIRED;
                return a;
            }
            a.pid = (int)v;
        }
    }
    return a;
}

void cli_success(const char *command, int pid, char *out, size_t cap)
{
    if (strcmp(command, "unload") == 0)
        snprintf(out, cap, "Removed from osu! (pid %d).", pid);
    else
        snprintf(out, cap, "Loaded into osu! (pid %d).", pid);
}

void cli_process_not_found(int pid, char *out, size_t cap)
{
    snprintf(out, cap, "No process with id %d.", pid);
}
