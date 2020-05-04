#include "maniac.h"

#include <time.h>
#include <string.h>
#include <getopt.h>

#define PLAY_ERROR 0
#define PLAY_FINISH 1

#define STANDBY_BREAK 0
#define STANDBY_CONTINUE 1

#ifdef ON_LINUX
Window game_window;
#endif /* ON_LINUX */

#ifdef ON_WINDOWS
HWND game_window;
#endif /* ON_WINDOWS */

int opterr;
char *optarg = 0;

char *osu_path = NULL;
char *default_map = "map.osu";

int delay = 0;
int exit_check = 1;
int replay_delta = 0;

int exp_delay = 0;
int exp_regen = 0;

void *time_address;
pid_t game_proc_id;

static void print_usage();

static int standby(char **map, int search);

static int standby_loop(char *map, int *search, int replay);

static int play(char *map);

static void play_loop(struct action *actions, int num_actions);

static int parse_key_value(char *string, char **key, char **value);

int main(int argc, char **argv) {
	setbuf(stdout, NULL);

	char *map = NULL;
	int replay = 0, c;

	time_address = 0;

	int option_index = 0;
	struct option long_options[] = {
		{ "map",        	required_argument, NULL, 'm' },
		{ "process",    	required_argument, NULL, 'p' },
		{ "address",    	required_argument, NULL, 'a' },
		{ "humanization",	required_argument, NULL, 'l' },
		{ "replay",		optional_argument, NULL, 'r' },
		{ "exit-checks", 	no_argument,       NULL, 'e' },
		{ "help",       	no_argument,       NULL, 'h' },
		{ "exp-humanization",	required_argument, NULL, 'x' },
		{ NULL, 		0, 		   NULL, 0   },
	};

	opterr = 0;
	char *v1, *v2;
	while (true) {
		c = getopt_long(argc, argv, ":m:p:a:l:r:x:he", long_options,
			&option_index);

		if (c == -1)
			break;

		switch (c) {
		case 0:
			break;
		case 'm':
			map = optarg;
			break;
		case 'p':
			game_proc_id = (pid_t) strtol(optarg, NULL, 10);
			break;
		case 'a':
			time_address = (void *) (intptr_t) strtol(
				optarg, NULL, 0);
			break;
		case 'l':
			delay = strtol(optarg, NULL, 10);
			break;
		case 'x':
			parse_key_value(optarg, &v1, &v2);

			exp_delay = strtol(v1, NULL, 10);
			exp_regen = strtol(v2, NULL, 10);

			printf("enabled experimental humanization with delay = %d"
	 		       " and regen = %d\n", exp_delay, exp_regen);

			break;
		case 'r':
			replay = 1;
			replay_delta = (int) strtol(optarg, NULL, 10);
			break;
		case 'e':
			exit_check = !exit_check;
			break;
		case 'h':
			print_usage(argv[0]);
			exit(EXIT_SUCCESS);
		case '?':
			if (optopt == 0) {
				printf("ignoring unknown option '%s'\n",
				       argv[optind - 1]);
			} else {
				printf("error parsing option '%c'\n", optopt);
				return EXIT_FAILURE;
			}

			break;
		}
	}

	if (!(get_osu_path(&osu_path))) {
		printf("couldn't get osu! path\n");
		return EXIT_FAILURE;
	}

	if (!game_proc_id &&
	    !(game_proc_id = (pid_t) get_process_id("osu!.exe"))) {
		printf("couldn't find game process ID\n");
		return EXIT_FAILURE;
	}

	do_setup();

	char *path;
	if (!(get_osu_path_exp(&path))) {
		printf("couldn't get osu path\n");
		return EXIT_FAILURE;
	}

	debug("osu path: %s", path);

	// We can only fetch time address after setup has been done.
	if (!(time_address = get_time_address())) {
		printf("couldn't find time address\n");
		return EXIT_FAILURE;
	}

	if (!(find_window((unsigned long) game_proc_id,
			  (void *) &game_window))) {
		printf("couldn't find game window\n");
		return EXIT_FAILURE;
	}

	const int fetch_len = 128;
	char *fetched_map = malloc(fetch_len);
	// If the user passed a map, play it.
	// If they didn't and window fetching is failing, use the default map.
	if (map || !(get_window_title(&fetched_map, fetch_len))) {
		play(map ? map : default_map);
		return EXIT_SUCCESS;
	}

	free(fetched_map);

	int search = 1;
	// Block while the user is not in a beatmap.
	while (standby(&map, search)) {
		// Play the beatmap, handle replay, handle stop.
		if (standby_loop(map, &search, replay) == STANDBY_BREAK)
			break;
	}

	free((void *) game_window);

	return EXIT_SUCCESS;
}

static int standby(char **map, int search) {
	debug("in standby mode");

	const int title_len = 128;
	char *title = malloc(title_len);
	// Idle while we're in menus.
	while (get_window_title(&title, title_len) && !strcmp(title, "osu!")) {
		nanosleep((struct timespec[]) {{ 0, 500000000L }}, NULL);
	}

	if (search) {
		find_beatmap(osu_path, title + 8, map);
	}

	free(title);

	return 1;
}

static int standby_loop(char *map, int *search, int replay) {
	int status = play(map);
	static int retries = 0;

	debug("play returned status %d", status);

	if (status == PLAY_ERROR) {
		printf("an error occurred while playing, "
		       "there's likely additional error output above\n");

		if (replay) {
			int retry_delay = 1000 * ++retries;
			printf("retrying in %d ms\n", retry_delay);

			nanosleep((struct timespec[]) {{
				0, (long) (retry_delay * 1000)
			}}, NULL);

			return STANDBY_CONTINUE;
		}

		return STANDBY_BREAK;
	}

	retries = 0;

	if (replay) {
		tap_key(KEY_ESCAPE);
		debug("pressed escape");

		nanosleep((struct timespec[]) {{ 8, 0 }}, NULL);

		tap_key(KEY_RETURN);
		debug("pressed enter");

		nanosleep((struct timespec[]) {{ 2, 0 }}, NULL);

		*search = 0;
		delay -= replay_delta;
	}

	return STANDBY_CONTINUE;
}

static int play(char *map) {
	struct hitpoint *points = NULL;
	struct beatmap_meta *meta = NULL;
	int num_points = (int) parse_beatmap(map, &points, &meta);

	if (num_points <= 0 || !points || !meta) {
		printf("failed to parse beatmap (%s)\n", map);

		if (num_points == ERROR_UNSUPPORTED_BEATMAP) {
			printf("beatmap has unsupported format, only maps "
			       "which were created specifically for osu!mania "
			       "and are 1-9k are supported at the moment\n");
		}

		return PLAY_ERROR;
	}

	printf("parsed %d hitpoints of map '%s' ('%s', %d)\n", num_points,
	       meta->title, meta->version, meta->map_id);

	humanize_hitpoints(num_points, &points, delay);

	debug("humanized %d hitpoints with delay of %d", num_points, delay);

	struct action *actions = NULL;
	int num_actions = parse_hitpoints(num_points, meta->columns, &points,
					  &actions);
	if (!num_actions || !actions) {
		printf("failed to parse hitpoints\n");
		return PLAY_ERROR;
	}

	debug("parsed %d actions", num_actions);

	free(points);

	if (sort_actions(num_actions, &actions) != 0) {
		printf("failed sorting actions\n");
		return PLAY_ERROR;
	}

	debug("sorted %d actions", num_actions);

	humanize_actions_exp(num_actions, &actions, exp_regen, exp_delay);

	printf("playing with delay of %d (delta: %d)\n", delay, replay_delta);

	play_loop(actions, num_actions);

	nanosleep((struct timespec[]) {{ 8, 0 }}, NULL);

	free(meta);
	free(actions);

	return PLAY_FINISH;
}

// TODO: The structure of play_loop and standby_loop are inconsistent, one is
// 	 looping and the other is called in a loop. Investigate a clean,
//	 consistent solution.
static void play_loop(struct action *actions, int num_actions) {
	int cur_i = 0;				// Current action offset.
	struct action *cur_a = NULL;		// Pointer to current action.
	int32_t time = get_maptime();		// Current maptime.

	const int title_len = 128;		// Max length of title.
	char *title = malloc(title_len);	// Current window title.

	// Discard all actions which come before our current maptime.
	for (; cur_i < num_actions; cur_i++)
		if (actions[cur_i].time >= time)
			break;

	debug("discarded %d actions", cur_i);

	while (cur_i < num_actions) {
		if (exit_check) {
			// If the user exited the map...
			if (get_window_title(&title, title_len)
			    && !strcmp(title, "osu!"))
				goto clean_exit;
		}

		time = get_maptime();

		while (cur_i < num_actions &&
		       (cur_a = actions + cur_i)->time < time) {
			cur_i++;

			debug("sending %i / '%c' (%s)", cur_a->key, cur_a->key,
			      cur_a->down ? "down" : "up");
			send_keypress(cur_a->key, cur_a->down);
		}

		// Sleep for 10ms (1 000 000 000ns == 1s).
		nanosleep((struct timespec[]) {{ 0, 10000000L }}, NULL);
	}

clean_exit:
	free(title);
}

static int parse_key_value(char *string, char **key, char **value) {
	int i = 0;
	char *token = strtok(string, ":");
	while (token != NULL) {
		switch (i++) {
		case 0:
			*key = token;
			break;
		case 1:
			*value = token;
			break;
		}

		token = strtok(NULL, ":");
	}

	return i;
}

static void print_usage() {
	printf("  Usage: ./maniac [options]\n\n");
	printf("  Options: \n\n");

	printf("    %s / %-15s id of game process (optional)\n",
	       "-p", "--process");
	printf("    %s / %-15s humanization level (default: 0)\n",
	       "-l", "--humanization");
	printf("    %s / %-15s address to read time from (optional)\n",
	       "-a", "--address");
	printf("    %s / %-15s path to beatmap (optional)\n",
	       "-m", "--map");
	printf("    %s / %-15s replay humanization level delta (optional)\n",
	       "-r", "--replay");
	printf("    %s / %-15s toggle exit checks in game loop (default: on)\n",
	       "-e", "--exit-checks");
	printf("    %s / %-15s print this message\n",
	       "-h", "--help");
}
