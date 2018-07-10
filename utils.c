#include "osu.h"

#include <stdio.h>

#ifdef ON_LINUX
Display *display;
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
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

	in.ki.wVk = 0;
	in.ki.time = 0;
	in.ki.wScan = key;
	in.ki.dwExtraInfo = 0;
	in.ki.dwFlags = KEYEVENTF_UNICODE | (down ? 0 : KEYEVENTF_KEYUP);

	SendInput(1, &in, sizeof(INPUT));
#endif /* ON_WINDOWS */
}

void do_setup()
{
#ifdef ON_LINUX
	if (!(display = OpenDisplay(NULL))) {
		printf("failed to open X display");
	}
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		printf("failed to get handle to game process\n");
	}
#endif /* ON_WINDOWS */
}