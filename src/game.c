#include "game.h"

static int get_env_var(char *name, char **out_var);
static void path_get_last(char *path, char **last);

__hot inline void send_keypress(int key, int down)
{
#ifdef ON_LINUX
	int keycode = XKeysymToKeycode(display, key);

	if (!keycode)
		return;

	XTestFakeKeyEvent(display, keycode, down, CurrentTime);

	XFlush(display);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	INPUT in;

	in.type = INPUT_KEYBOARD;

	in.ki.time = 0;
	in.ki.wScan = 0;
	in.ki.dwExtraInfo = 0;
	in.ki.dwFlags = down ? 0 : KEYEVENTF_KEYUP;
	in.ki.wVk = VkKeyScanEx(key, GetKeyboardLayout(0)) & 0xFF;

	SendInput(1, &in, sizeof(INPUT));
#endif /* ON_WINDOWS */
}

void tap_key(int key)
{
	send_keypress(key, 1);
	nanosleep((struct timespec[]){{ 0, 10000000L }}, NULL);
	send_keypress(key, 0);
}

int get_osu_path(char **out_path)
{
	if (!out_path) {
		debug("received null pointer");
		return 0;
	}

	char *home;
	int home_len;

	if (!(home_len = get_env_var(HOME_ENV, &home))) {
		debug("failed fetching home path");
		return 0;
	}

	// Subtract one since sizeof includes the terminating null.
	int path_len = home_len + (sizeof(DEFAULT_OSU_PATH) - 1);
	*out_path = malloc(path_len + 1);

	strcpy(*out_path, home);
	// We overwrite the old terminating null here.
	strcpy(*out_path + home_len, DEFAULT_OSU_PATH);

	free(home);

	return path_len;
}

void do_setup()
{
#ifdef ON_LINUX
	if (!(display = XOpenDisplay(NULL))) {
		printf("failed to open X display\n");

		return;
	}
	
	debug("opened X display (%#x)", (unsigned)(intptr_t)display);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		printf("failed to get handle to game process\n");
		return;
	}
	
	debug("got handle to game process with ID %d",
		(int)game_proc_id);
#endif /* ON_WINDOWS */
}

static void path_get_last(char *path, char **last)
{
	int i, l = 0;
	*last = path;
	for (i = 0; *path; path++, i++)
		if (*path == SEPERATOR)
			l = i + 1;

	*last += l;
}

static int get_env_var(char *name, char **out_var)
{
	if (!out_var || !name) {
		debug("received null pointer");
		return 0;
	}

	int var_len = 0;
	char *var = getenv(name);

	// getenv() returns NULL if the variable could not be found.
	if (!var || !(var_len = strlen(var))) {
		debug("environmental variable (%s) does not exist", name);

		return 0;
	}

	*out_var = malloc(var_len + 1);
	// Copy because getenv returns a pointer to internal memory.
	strcpy(*out_var, var);

	return var_len;
}
