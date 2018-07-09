#include "osu.h"

#include <time.h>
#include <stdio.h>
#include <unistd.h>
#include <signal.h> 

#include <X11/Xlib.h>
#include <X11/extensions/XTest.h>

void dbg_print_actions(int count, action** actions);
void dbg_print_hitpoints(int count, hitpoint **points);

static inline void send_keypress(int code, int down);

int opterr;
char *optarg = 0;

Display *display;

int main(int argc, char **argv)
{
        pid_t pid;
	char *map, c;

	while ((c = getopt(argc, argv, "m:p:")) != -1) {
		switch (c) {
		case 'm': map = optarg;
			break;
		case 'p': pid = strtol(optarg, NULL, 10);
			break;
		}
	}

	if (!pid || !map) {
		printf("usage: <executable> -p <pid of osu! process> ");
		printf("-m <path to beatmap.osu>\n");
	}

	if (!(display = XOpenDisplay(NULL))) {
		printf("failed to open X display\n");
		return EXIT_FAILURE;
	}

	hitpoint *points;
	int num_points = 0;
	if ((num_points = parse_beatmap(map, &points)) == 0 || !points) {
		printf("failed to parse beatmap (%s)\n", map);
		return EXIT_FAILURE;
	}

	printf("parsed %d hitpoints\n", num_points);

	action *actions;
	int num_actions = 0;
	if ((num_actions = parse_hitpoints(num_points, &points, &actions)) == 0
		|| !actions) {
		printf("failed to parse hitpoints\n");
		return EXIT_FAILURE;
	}

	printf("parsed %d actions\n", num_actions);

	free(points);

	if (sort_actions(num_actions, &actions) != 0) {
		printf("failed sorting actions\n");
		return EXIT_FAILURE;
	}

	int32_t time;
	int cur_i = 0;
	action *cur_a;

	while (cur_i < num_actions) {
		time = get_maptime(pid);

		while ((cur_a = actions + cur_i)->time <= time) {
			cur_i++;
					
			send_keypress(cur_a->code, cur_a->down);		
		}

		nanosleep((struct timespec[]){{0, 1000000L}}, NULL);
	}

	return 0;
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

static inline void send_keypress(int code, int down)
{
	XTestFakeKeyEvent(display, code, down, CurrentTime);

	XFlush(display);
}