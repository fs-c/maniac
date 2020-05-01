#include "humanization.h"

/**
 * Returns a randomly generated number in the range of [0, range], while
 * attempting to constrain it outside of a bound(ary) given in percent (]0, 1[),
 * in a given number of rounds.
 */
static int generate_number(int range, int rounds, double bound);

// TODO: This function is retarded, fix it and add actual humanization.
void humanize_hitpoints(int total, struct hitpoint **points, int level) {
	if (!level) {
		return;
	}

	int i, offset;
	struct hitpoint *p = NULL;
	for (i = 0; i < total; i++) {
		p = *points + i;

		// [0, level]
		offset = generate_number(level, RNG_ROUNDS, RNG_BOUNDARY);

		// [-(level / 2), (level / 2)]
		offset -= (level / 2);

		p->end_time += offset;
		p->start_time += offset;
	}
}

static int generate_number(int range, int rounds, double bound) {
	int rn = rand() % range;

	// Min and max percentage of the range we will use with our constraint.
	double minr = 0.5 - (bound / 2);
	double maxr = 0.5 + (bound / 2);

	for (int i = 0; i < rounds; i++) {
		int in = rn > (range * minr) && rn < (range * maxr);

		rn += (in ? (rand() % (int) (range * minr)) : 0)
		      * (rn < (range * 0.5) ? -1 : 1);
	}

	return rn;
}