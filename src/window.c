#include "window.h"

#include <string.h>

#ifdef ON_LINUX
Display *display;
Window game_window;

static pid_t get_window_pid(Window window);
static int is_window_visible(Window window);
static int is_window_match(Window window, pid_t pid);
static void search_children(pid_t pid, Window window, Window *out);
static int get_xwindow_title(Window window, char *title, int title_len);
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

__stdcall static WINBOOL enum_windows_callback(HWND handle, LPARAM param);

#endif /* ON_WINDOWS */

int find_window(unsigned long process_id, void **out_window) {
#ifdef ON_WINDOWS
	struct handle_data data = { 0, process_id };
	EnumWindows((WNDENUMPROC) enum_windows_callback, (LPARAM) &data);

	HWND handle = data.window_handle;
	*out_window = malloc(sizeof(HWND));

	memcpy(out_window, &handle, sizeof(HWND));

	return 1;
#endif /* ON_WINDOWS */

#ifdef ON_LINUX
	Window root = RootWindow(display, 0), found = 0;

	search_children((pid_t)process_id, root, &found);

	if (found) {
		*out_window = malloc(sizeof(Window));

		memcpy(out_window, &found, sizeof(Window));

		return 1;
	}

	return 0;
#endif /* ON_LINUX */
}

hot int get_window_title(char **title, int title_len) {
#ifdef ON_WINDOWS
	return GetWindowText(game_window, *title, title_len);
#endif /* ON_WINDOWS */

#ifdef ON_LINUX
	return get_xwindow_title(game_window, *title, title_len);
#endif /* ON_LINUX */

	return 0;
}

#ifdef ON_WINDOWS
__stdcall static WINBOOL enum_windows_callback(HWND handle, LPARAM param) {
	struct handle_data *data = (struct handle_data *) param;

	unsigned long process_id = 0;
	GetWindowThreadProcessId(handle, &process_id);

	if (process_id != data->process_id)
		return 1;

	data->window_handle = handle;
	return 0;
}

#endif /* ON_WINDOWS */

#ifdef ON_LINUX
static int get_xwindow_title(Window window, char *title, int title_len)
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
		debug("_NET_WM_NAME not set, falling back to WM_NAME");

		prop = get_window_property(window, name_atom, &num_items);
	}

	if ((int)strlen((char *)prop) >= title_len) {
		debug("window name exceeds max length of %d", title_len);

		return 0;
	}

	strcpy(title, (char *)prop);

	XFree((void *)prop);

	return strlen(title);
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

		debug("failed getting children of %ld", window);

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

	XFree(children);	
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
		debug("failed getting window (%ld) property", window);

		return NULL;
	}

	return prop;
}

static int is_window_visible(Window window)
{
	XWindowAttributes attr;
	int success = XGetWindowAttributes(display, window, &attr);

	if (!success) {
		debug("failed getting window (%ld) attributes",
			window);

		return 0;
	}

	if (attr.map_state != IsViewable) {
		return 0;
	}

	return 1;
}
#endif /* ON_LINUX */
