#include "maniac.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static int g_fail;

static void expect(int cond, const char *msg)
{
    if (cond)
        return;
    fprintf(stderr, "FAIL %s\n", msg);
    g_fail++;
}

static void expect_str(const char *got, const char *want, const char *msg)
{
    if (got && want && strcmp(got, want) == 0)
        return;
    fprintf(stderr, "FAIL %s: got [%s] want [%s]\n", msg, got ? got : "(null)", want ? want : "(null)");
    g_fail++;
}

int main(void)
{
    char tmp[64];
    char *av0[] = {"maniac"};
    CliArgs a = cli_parse(1, av0);
    expect(a.help == 0 && a.verbose == 0 && a.pid == 0, "default flags");
    expect_str(a.command, "reload", "default command");
    expect(a.error == NULL, "default no error");

    char *av1[] = {"maniac", "--help"};
    a = cli_parse(2, av1);
    expect(a.help, "--help");
    char *av2[] = {"maniac", "-h"};
    a = cli_parse(2, av2);
    expect(a.help, "-h");
    char *av3[] = {"maniac", "-v", "--unload"};
    a = cli_parse(3, av3);
    expect(a.verbose && strcmp(a.command, "unload") == 0 && !a.help, "-v --unload");

    char *av4[] = {"maniac", "--pid", "1234"};
    a = cli_parse(3, av4);
    expect(a.pid == 1234, "--pid 1234");
    char *av5[] = {"maniac", "--pid=99"};
    a = cli_parse(2, av5);
    expect(a.pid == 99, "--pid=99");
    char *av6[] = {"maniac", "--pid"};
    a = cli_parse(2, av6);
    expect_str(a.error, MSG_PID_REQUIRED, "--pid missing");
    char *av7[] = {"maniac", "--pid", "nope"};
    a = cli_parse(3, av7);
    expect_str(a.error, MSG_PID_REQUIRED, "--pid nope");
    char *av8[] = {"maniac", "--pid", "0"};
    a = cli_parse(3, av8);
    expect_str(a.error, MSG_PID_REQUIRED, "--pid 0");

    cli_success("reload", 7, tmp, sizeof tmp);
    expect_str(tmp, "Loaded into osu! (pid 7).", "success reload");
    cli_success("unload", 7, tmp, sizeof tmp);
    expect_str(tmp, "Removed from osu! (pid 7).", "success unload");
    expect(strstr(MSG_HELP, "maniac --unload") != NULL, "help mentions unload");
    expect(strstr(MSG_HELP, "build the plugin") == NULL, "help has no build");

    const uint8_t b2[] = {2};
    const uint8_t b1[] = {1};
    PayloadFile afiles[2];
    afiles[0].name = "b.dll";
    afiles[0].bytes = b2;
    afiles[0].size = 1;
    afiles[1].name = "a.dll";
    afiles[1].bytes = b1;
    afiles[1].size = 1;
    PayloadFile bfiles[2];
    bfiles[0].name = "a.dll";
    bfiles[0].bytes = b1;
    bfiles[0].size = 1;
    bfiles[1].name = "b.dll";
    bfiles[1].bytes = b2;
    bfiles[1].size = 1;
    PayloadFile one[1];
    one[0].name = "a.dll";
    one[0].bytes = b1;
    one[0].size = 1;
    char ha[65], hb[65], h1[65];
    expect(payload_hash(afiles, 2, ha) && payload_hash(bfiles, 2, hb) && strcmp(ha, hb) == 0, "hash order");
    expect(payload_hash(one, 1, h1) && strcmp(ha, h1) != 0, "hash differs");

    char root[MANIAC_PATH_MAX];
    temp_dir(root, sizeof root);
    char app[MANIAC_PATH_MAX], sidecar[MANIAC_PATH_MAX], out[MANIAC_PATH_MAX];
    path_join(app, sizeof app, root, "maniac-lazer-tests/app");
    path_join(sidecar, sizeof sidecar, app, "plugin");
    make_dirs(sidecar);
    path_join(out, sizeof out, sidecar, "Maniac.dll");
    FILE *f = fopen(out, "wb");
    expect(f != NULL, "write sidecar");
    if (f)
    {
        fputs("sidecar", f);
        fclose(f);
    }
    const char *err = NULL;
    expect(payload_resolve(app, out, sizeof out, &err), "resolve sidecar");

    char empty[MANIAC_PATH_MAX];
    path_join(empty, sizeof empty, root, "maniac-lazer-tests/empty-app");
    make_dirs(empty);
    err = NULL;
    expect(!payload_resolve(empty, out, sizeof out, &err), "empty incomplete");
    expect_str(err, MSG_INCOMPLETE, "empty message");

    char msg[256];
    expect(!expiry_is_expired("", msg, sizeof msg), "no stamp");
    expect(expiry_is_expired("tomorrow", msg, sizeof msg), "invalid stamp");
    expect_str(msg, "This build has an invalid expiry stamp and will not run.", "invalid message");
    expect(expiry_is_expired("2000-01-01", msg, sizeof msg), "past date expired");
    expect(strstr(msg, "expired on") != NULL, "expired copy");
    expect(!expiry_is_expired("2099-01-01", msg, sizeof msg), "future date");
    expect(strstr(msg, "ends") != NULL, "future copy");

    if (g_fail)
    {
        fprintf(stderr, "%d failed\n", g_fail);
        return 1;
    }
    printf("ok\n");
    return 0;
}
