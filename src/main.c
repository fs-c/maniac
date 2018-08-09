#include "osu.h"

#include <time.h>
#include <stdio.h>
#include <unistd.h>
#include <string.h>
#include <signal.h> 

void dbg_print_actions(int count, action** actions);
void dbg_print_hitpoints(int count, hitpoint **points);

int opterr;
char *optarg = 0;

char *default_map = "map.osu";

int delay = 0;

void *time_address;
pid_t game_proc_id;

void play(char *map);
int standby(char **map);

int main(int argc, char **argv)
{
	setbuf(stdout, NULL);

	int c;
	char *map = NULL;

	time_address = 0;
	game_proc_id = get_process_id("osu!.exe");

	while ((c = getopt(argc, argv, "m:p:a:d:")) != -1) {
		switch (c) {
		case 'm': map = optarg;
			break;
		case 'p': game_proc_id = strtol(optarg, NULL, 10);
			break;
		case 'a': time_address = (void *)(uintptr_t)strtol(optarg, NULL, 0);
			break;
		case 'd': delay = strtol(optarg, NULL, 10);
			break;
		}
	}

	if (!game_proc_id) {
		printf("usage: %s -p <pid of osu! process> ", argv[0]);
		printf("-m <path to beatmap.osu> -a <time address> ");
		printf("-d <humanization level>\n");

		return EXIT_FAILURE;
	}

	do_setup();

	if (!(time_address = get_time_address())) {
		printf("couldn't find time address");
		return EXIT_FAILURE;
	}

	char *fetched_map = NULL;
	// If the user passed a map, play it.
	// If window fetching failed (probably because we're on Linux), use the
	// default map.
	if (map || !(get_window_title(&fetched_map))) {
		play(map ? map : default_map);

		return EXIT_SUCCESS;
	}

	while (standby(&map)) {
		play(map);
	}

	return EXIT_SUCCESS;
}

int standby(char **map)
{
	char osu_path[256];

	strcpy(osu_path, getenv(HOME_ENV));
	strcpy(osu_path + strlen(osu_path), DEFAULT_OSU_PATH);

	char *title;
	// Idle while we're in menus.
	while (get_window_title(&title) && strcmp(title, "osu!") == 0) {
		nanosleep((struct timespec[]){{ 0, 500000000L }}, NULL);
	}

	find_beatmap(osu_path, title + 8, map);

	return 1;
}

void play(char *map)
{
	hitpoint *points;
	int num_points = 0;
	if ((num_points = parse_beatmap(map, &points)) == 0 || !points) {
		printf("failed to parse beatmap (%s)\n", map);
		return;
	}

	printf("parsed %d hitpoints\n", num_points);

	humanize_hitpoints(num_points, &points, delay);

	action *actions;
	int num_actions = 0;
	if ((num_actions = parse_hitpoints(num_points, &points, &actions)) == 0
		|| !actions)
	{
		printf("failed to parse hitpoints\n");
		return;
	}

	printf("parsed %d actions\n", num_actions);

	free(points);

	if (sort_actions(num_actions, &actions) != 0) {
		printf("failed sorting actions\n");
		return;
	} 

	int32_t time;
	int cur_i = 0;
	action *cur_a;

	while (cur_i < num_actions) {
		time = get_maptime();

		while ((cur_a = actions + cur_i)->time <= time) {
			cur_i++;

			send_keypress(cur_a->key, cur_a->down);		
		}

		nanosleep((struct timespec[]){{ 0, 1000000L }}, NULL);
	}

	free(actions);

	return;
}

void dbg_print_actions(int count, action **actions)
{
	for (int i = 0; i < count; i++) {
		action *a = *actions + i;
		printf("%d / %d (%c) / %d\n", a->time, a->key, a->key, a->down);
	}
}

void dbg_print_hitpoints(int count, hitpoint **points)
{
	for (int i = 0; i < count; i++) {
		hitpoint *p = *points + i;
		printf("%d - %d / %d\n", p->start_time, p->end_time, p->column);
	}
}