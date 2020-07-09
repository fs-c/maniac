#include "maniac.h"

namespace maniac {
	void block_until_playing() {
		while (true) {
			if (!osu->is_playing())
				std::this_thread::sleep_for(std::chrono::milliseconds(100));
			else break;
		}
	}

	void play(std::vector<osu::Action> &actions) {
		auto cur_i = 0;
		auto cur_time = 0;
		auto raw_actions = actions.data();
		auto total_actions = actions.size();

		while (cur_i < total_actions) {
			if (!osu->is_playing())
				return;

			cur_time = osu->get_game_time();
			while (cur_i < total_actions && (raw_actions + cur_i)->time <= cur_time) {
				osu::Osu::execute_actions(raw_actions + cur_i, 1);

				cur_i++;
			}

			std::this_thread::sleep_for(std::chrono::milliseconds(5));
		}
	}
}
