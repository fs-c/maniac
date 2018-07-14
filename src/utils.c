#include "osu.h"

#include <stdio.h>

#ifdef ON_LINUX
  Display *display;
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
  #include <windows.h>
  #include <tlhelp32.h>
  HANDLE game_proc;
#endif /* ON_WINDOWS */

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
	}
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		printf("failed to get handle to game process\n");
	}
#endif /* ON_WINDOWS */
}

unsigned long get_process_id(char *name)
{
#ifdef ON_WINDOWS
	DWORD proc_id = 0;
	HANDLE proc_list = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

	PROCESSENTRY32 entry = {0};
	entry.dwSize = sizeof(PROCESSENTRY32);

	do {
		if (strcmp((char *)entry.szExeFile, name) == 0) {
			CloseHandle(proc_list);

			return entry.th32ProcessID;
		}
	} while (Process32Next(proc_list, &entry));

	CloseHandle(proc_list);

	return proc_id;
#endif /* ON_WINDOWS */
	// Compiler will remove this anyways, and it gets rid of the annoying
	// unused variable warning.
	return name ? 0 : 0;
}