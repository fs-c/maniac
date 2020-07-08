#pragma once

#include "common.h"
#include "osu/osu.h"

namespace maniac {
	inline Osu *osu;

	void play(std::vector<Action> &actions);

	void block_until_playing();

	void humanize(std::vector<Action> &actions, int modifier);
	void randomize(std::vector<Action> &actions, std::pair<int, int> range);
}