#include "osu.h"
#include "pattern.h"

#include <stdio.h>

#ifdef ON_LINUX
  Display *display;
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
  #include <windows.h>
  #include <tlhelp32.h>
  HANDLE game_proc;
#endif /* ON_WINDOWS */

void *time_address;

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
	return (void *)TIME_ADDRESS;
#endif
}