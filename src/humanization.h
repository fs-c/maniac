#ifndef HUMANIZATION_H
#define HUMANIZATION_H

#include "common.h"
#include "beatmap.h"

#define RNG_ROUNDS 50
#define RNG_BOUNDARY 0.5

/**
 * Add a randomized delay of magnitude level to the hitpoints.
 */
void humanize_hitpoints(int total, struct hitpoint **points, int level);

/**
 */
void humanize_actions_exp(int total, struct action **actions, int regen_time,
			  int delta);

#endif /* HUMANIZATION_H */