#pragma once

#include <vector>
#include <utility>
#include <maniac/osu.h>
#include <maniac/common.h>

namespace maniac {
	struct config {
        int tap_time = 20;
		bool mirror_mod = false;
		int compensation_offset = 0;
		int humanization_modifier = 0;
		std::pair<int, int> randomization_range = { 0, 0 };
	};

	inline config config;

	inline osu::Osu *osu;

	void play(std::vector<osu::Action> &actions);

	void block_until_playing();

	void humanize(std::vector<osu::Action> &actions, int modifier);
	void randomize(std::vector<osu::Action> &actions, std::pair<int, int> range);

	std::vector<osu::Action> get_actions(int32_t min_time);
}
