/**
 * This is not a script, but it does not belong in /src either.
 * When compiled, this can be used to pinpoint where (and if) maniac is failing
 * as there appear to be unusually large failure rates on machines different
 * from the one I use to develop this. 
 */

#include "../src/maniac.h"

#ifndef ON_WINDOWS

int main()
{
	printf("this utility only works on machines running Windows\n");

	return EXIT_FAILURE;
}

#else

void fatal(char *message);

struct handle_data {
	HWND window_handle;
	unsigned long process_id;
};

static HWND find_window(unsigned long process_id);
__stdcall static WINBOOL enum_windows_callback(HWND handle, LPARAM param);

int main()
{
	char *osu_path;
	int game_proc_id;

	if (!(get_osu_path(&osu_path))) {
		fatal("couldn't get osu! path\n");
	}

	printf("osu path found: %s\n", osu_path);

	if (!(game_proc_id = get_process_id("osu!.exe"))) {
		fatal("couldn't find game process ID\n");
	}

	printf("found game process with ID %d\n", game_proc_id);

	if (!(game_proc = OpenProcess(PROCESS_VM_READ, 0, game_proc_id))) {
		fatal("failed to get handle to game process\n");
	}

	printf("got handle to game process (%#x)\n",
		(unsigned)(intptr_t)game_proc);

	if (!(game_window = find_window(game_proc_id))) {
		fatal("failed to find game window\n");
	}

	printf("found game window (%#x)\n", (unsigned)(intptr_t)&game_window);

	const int fetch_len = 128;
	char *fetched_map = malloc(fetch_len);
	if (!(get_window_title(&fetched_map, fetch_len))) {
		fatal("failed to get window title\n");
	}

	printf("got window title: %s\n", fetched_map);

	const int map_len = 256;
	char *map = malloc(map_len);
	if (!(find_beatmap(osu_path, fetched_map + 8, &map))) {
		fatal("failed to find beatmap\n");
	}

	printf("found beatmap: %s\n", map);

	int num_points = 0;
	struct beatmap *meta;
	struct hitpoint *points;
	if ((num_points = parse_beatmap(map, &points, &meta)) == 0) {
		fatal("failed to parse beatmap\n");
	}

	printf("parsed beatmap with %d hitpoints\n", num_points);
	printf("  set: %d, map: %d, title: %s, artist: %s, version: %s\n",
		meta->set_id, meta->map_id, meta->title, meta->artist,
		meta->version);

	struct hitpoint p = *points;

	printf("first hitpoint:\n");
	printf("  column: %d, start: %d, end: %d", p.column, p.start_time,
		p.end_time);

	return EXIT_SUCCESS;
}

void fatal(char *message)
{
	printf("fatal: %s\n", message);
	exit(EXIT_FAILURE);
}

static HWND find_window(unsigned long process_id)
{
	struct handle_data data = { 0, process_id };
	EnumWindows((WNDENUMPROC)enum_windows_callback, (LPARAM)&data);

	return data.window_handle;
}

__stdcall static WINBOOL enum_windows_callback(HWND handle, LPARAM param)
{
	struct handle_data *data = (struct handle_data *)param;

	unsigned long process_id = 0;
	GetWindowThreadProcessId(handle, &process_id);

	if (process_id != data->process_id)
		return 1;

	data->window_handle = handle;
	return 0;
}

#endif
