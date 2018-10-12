#include "../osu.h"

void dbg_print_actions(struct action *actions, int from, int to);
void dbg_print_hitpoints(struct hitpoint *points, int from, int to);

int main()
{
	/*

	int osu_path_len = 0;
	char *osu_path = NULL;

	if (!(osu_path_len = get_osu_path(&osu_path))) {
		printf("error: failed getting osu! path\n");

		return EXIT_FAILURE;
	}

	debug("got osu path: %s (%d)", osu_path, osu_path_len);

	int beatmap_len = 0;
	char *beatmap = NULL;

	if (!(beatmap_len = find_beatmap(osu_path, "Shakunet Dysto", &beatmap))) {
		printf("error: couldn't find beatmap\n");

		return EXIT_FAILURE;
	}

	debug("found beatmap: %s (%d)", beatmap, beatmap_len);

	*/

	char beatmap[] = "./example.osu";

	int total_points = 0;
	struct hitpoint *points = NULL;
	struct beatmap_meta *meta = NULL;

	if (!(total_points = parse_beatmap(beatmap, &points, &meta))) {
		printf("error: failed parsing beatmap\n");

		return EXIT_FAILURE;
	}

	debug("got %d points", total_points);

	debug("set: %d, map: %d, title: %s, artist: %s, version: %s, columns: %d",
		meta->set_id, meta->map_id, meta->title, meta->artist,
		meta->version, meta->columns);

	int total_actions = 0;
	struct action *actions = NULL;

	if (!(total_actions = parse_hitpoints(total_points, meta->columns,
		&points, &actions)))
	{
		printf("error: failed parsing hitpoints\n");

		return EXIT_FAILURE;
	}

	debug("got %d actions", total_actions);

	int sort_sucess = !sort_actions(total_actions, &actions);

	debug("sorting: %d", sort_sucess);

	return 0;
}

void dbg_print_hitpoints(struct hitpoint *points, int from, int to)
{
	for (int i = from; i < to; i++) {
		struct hitpoint *p = points + i;
		debug("%d - col: %d, start: %d, end: %d", i, p->column,
			p->start_time, p->end_time);
	}
}

void dbg_print_actions(struct action *actions, int from, int to)
{
	for (int i = from; i < to; i++) {
		struct action *a = actions + i;
		debug("%d - down: %d, key: %c (%d), time: %d", i, a->down,
			a->key, a->key, a->time);
	}
}