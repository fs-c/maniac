#include "osu.h"

#include <time.h>
#include <stdio.h>
#include <string.h>
#include <stdarg.h>

#ifdef ON_LINUX
  Window window;
  Display *display;

  static Window find_window(pid_t pid);
  static pid_t get_window_pid(Window window);
  static int is_window_visible(Window window);
  static int is_window_match(Window window, pid_t pid);
  static void search_children(pid_t pid, Window window, Window *out);
  static int get_window_name(Window window, char *title, int title_len);
  static unsigned char *get_window_property(Window window, Atom atom,
	size_t *num_items);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
  #include <windows.h>
  #include <tlhelp32.h>

  HWND game_window;
  HANDLE game_proc;

  struct handle_data {
	HWND window_handle;
	unsigned long process_id;
  };

  __stdcall static int enum_windows_callback(HWND handle, void *param);

  /**
   * Returns a handle to the main window of the process with the given ID.
   */
  static HWND find_window(unsigned long process_id);
#endif /* ON_WINDOWS */

void *time_address;
pid_t game_proc_id;

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

void do_setup()
{
#ifdef ON_LINUX
	if (!(display = XOpenDisplay(NULL))) {
		printf("failed to open X display\n");

		return;
	} else debug("opened X display (%#x)", (unsigned)(intptr_t)display);

	if (!(window = find_window(game_proc_id))) {
		printf("failed to find game window\n");

		return;
	} else debug("found game window (%ld)", window);
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		printf("failed to get handle to game process\n");
		return;
	} else debug("got handle to game process with ID %d", game_proc_id);

	if (!(game_window = find_window(game_proc_id))) {
		printf("failed to find game window\n");
		return;
	} else debug("found game window");;
#endif /* ON_WINDOWS */
}

void *get_time_address()
{
#ifdef ON_WINDOWS
	void *time_address = NULL;
	void *time_ptr = find_pattern((unsigned char *)SIGNATURE,
		sizeof(SIGNATURE) - 1);

	if (!ReadProcessMemory(game_proc, (void *)time_ptr, &time_address,
		sizeof(DWORD), NULL))
	{
		return NULL;
	}

	return time_address;
#endif

#ifdef ON_LINUX
	return (void *)LINUX_TIME_ADDRESS;
#endif
}

__hot int get_window_title(char **title, int title_len)
{
#ifdef ON_WINDOWS
	return GetWindowText(game_window, *title, title_len);
#endif /* ON_WINDOWS */

#ifdef ON_LINUX
	return get_window_name(window, *title, title_len);
#endif /* ON_LINUX */

	return 0;
}

// I hate having to do this but I can't think of a cleaner solution. (TODO?)
#ifdef ON_WINDOWS
static HWND find_window(unsigned long process_id)
{
	struct handle_data data = { 0, process_id };
	EnumWindows((WNDENUMPROC)enum_windows_callback, (LPARAM)&data);

	return data.window_handle;
}

__stdcall static int enum_windows_callback(HWND handle, void *param)
{
	struct handle_data *data = (struct handle_data *)param;

	unsigned long process_id = 0;
	GetWindowThreadProcessId(handle, &process_id);

	if (process_id != data->process_id)
		return 1;

	data->window_handle = handle;
	return 0;
}
#endif /* ON_WINDOWS */

// See above, this seems like a horrible solution.
#ifdef ON_LINUX
static Window find_window(pid_t pid)
{
	Window root = RootWindow(display, 0), found = 0;

	search_children(pid, root, &found);

	if (found) {
		return found;
	}

	return 0;
}

static void search_children(pid_t pid, Window window, Window *out)
{
	size_t num_children = 0;
	Window dummy, *children = NULL;

	int success = XQueryTree(display, window, &dummy, &dummy, &children,
		(unsigned *)&num_children);
	
	if (!success) {
		if (children)
			XFree(children);

		printf("failed getting children of %ld\n", window);

		return;
	}

	for (size_t i = 0; i < num_children; i++) {
		Window child = children[i];

		if (is_window_match(child, pid)) {
			*out = child;			

			return;
		}
	}

	for (size_t i = 0; i < num_children; i++) {
		search_children(pid, children[i], out);
	}
}

static int is_window_match(Window window, pid_t pid)
{
	if (get_window_pid(window) != pid)
		return 0;

	if (!(is_window_visible(window)))
		return 0;

	return 1;
}

static pid_t get_window_pid(Window window)
{
	size_t num_items;
	unsigned char *data;

	static Atom pid_atom = -1;

	if (pid_atom == (Atom)-1) {
		pid_atom = XInternAtom(display, "_NET_WM_PID", 0);
	}

	data = get_window_property(window, pid_atom, &num_items);

	pid_t pid = (num_items > 0) ? ((pid_t) *((unsigned long *)data)) : 0;

	XFree(data);
	
	return pid;
}

static unsigned char *get_window_property(Window window, Atom atom,
	size_t *num_items)
{
	Atom actual_type;
	int actual_format;

	size_t bytes_after;
	unsigned char *prop;

	int status = XGetWindowProperty(display, window, atom, 0, (~0L), 0,
		AnyPropertyType, &actual_type, &actual_format, num_items,
		&bytes_after, &prop);

	if (status != Success) {
		printf("failed getting window (%ld) property\n", window);

		return NULL;
	}

	return prop;
}

static int is_window_visible(Window window)
{
	XWindowAttributes attr;
	int success = XGetWindowAttributes(display, window, &attr);

	if (!success) {
		printf("failed getting window (%ld) attributes\n",
			window);

		return 0;
	}

	if (attr.map_state != IsViewable) {
		return 0;
	}

	return 1;
}

static int get_window_name(Window window, char *title, int title_len)
{
	static Atom net_name_atom = -1, name_atom = -1;

	if (name_atom == (Atom)-1)
		name_atom = XInternAtom(display, "WM_NAME", 0);
	if (net_name_atom == (Atom)-1)
		net_name_atom = XInternAtom(display, "_NET_WM_NAME", 0);

	 // http://standards.freedesktop.org/wm-spec/1.3/ar01s05.html
	 // Prefer _NET_WM_NAME if available, otherwise use WM_NAME

	size_t num_items = 0;

	unsigned char *prop = get_window_property(window, net_name_atom,
		&num_items);

	if (!num_items) {
		prop = get_window_property(window, name_atom, &num_items);
	}

	strcpy(title, (char *)prop);

	return strlen(title);
}
#endif /* ON_LINUX */

// TODO: I'm certain there's a more elegant way to go about this.
int partial_match(char *base, char *partial)
{
	int i = 0;
	int m = 0;
	while (*base) {
		char c = partial[i];
		if (c == '.') {
			i++;
			continue;
		}

		if (*base++ == c) {
			i++;
			m++;
		}
	}

	return m;
}

void path_get_last(char *path, char **last)
{
	int i, l = 0;
	*last = path;
	for (i = 0; *path; path++, i++)
		if (*path == SEPERATOR)
			l = i + 1;

	*last += l;
}

int generate_number(int range, int rounds, float bound)
{
	int rn = rand() % range;

	// Min and max percentage of the range we will use with our constraint.
	float minr = 0.5 - (bound / 2);
	float maxr = 0.5 + (bound / 2);

	for (int i = 0; i < rounds; i++) {
		int in = rn > (range * minr) && rn < (range * maxr);

		rn += (in ? (rand() % (int)(range * minr)) : 0)
			* (rn < (range * 0.5) ? -1 : 1);
	}

	return rn;
}

int get_env_var(char *name, char **out_var)
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
