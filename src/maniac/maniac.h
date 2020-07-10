#pragma once

#include "common.h"
#include "config.h"
#include "osu/osu.h"

namespace maniac {
	inline config config;

	inline osu::Osu *osu;

	void play(std::vector<osu::Action> &actions);

	void block_until_playing();

	void humanize(std::vector<osu::Action> &actions, int modifier);
	void randomize(std::vector<osu::Action> &actions, std::pair<int, int> range);

	std::vector<osu::Action> get_actions(int32_t min_time);
}