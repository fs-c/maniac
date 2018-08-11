#include "osu.h"
#include "pattern.h"

#include <stdio.h>

#ifdef ON_LINUX
  Display *display;
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
#endif /* ON_WINDOWS */

/**
 * Given a base, returns the number of concurrent characters which match
 * partial.
 */
int partial_match(char *base, char *partial);

/**
 * Returns a handle to the main window of the process with the given ID.
 */
HWND find_window(unsigned long process_id);

__stdcall int enum_windows_callback(HWND handle, void *param);

void *time_address;
pid_t game_proc_id;

void send_keypress(char key, int down)
{
#ifdef ON_LINUX
	int keycode = XKeysymToKeycode(display, key);

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

void do_setup()
{
#ifdef ON_LINUX
	if (!(display = XOpenDisplay(NULL))) {
		printf("failed to open X display");

		return;
	}
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		printf("failed to get handle to game process\n");

		return;
	}

	if (!(game_window = find_window(game_proc_id))) {
		printf("failed to find game window\n");

		return;
	}
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

int get_window_title(char **title)
{
#ifdef ON_WINDOWS
	const int title_len = 128;
	*title = malloc(title_len);
	return GetWindowText(game_window, *title, title_len);
#endif /* ON_WINDOWS */

	return 0;
}

// I hate having to this but can't think of a cleaner solution.
#ifdef ON_WINDOWS
HWND find_window(unsigned long process_id)
{
	struct handle_data data = { 0, process_id };
	EnumWindows((WNDENUMPROC)enum_windows_callback, (LPARAM)&data);

	return data.window_handle;
}

__stdcall int enum_windows_callback(HWND handle, void *param)
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