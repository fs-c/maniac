#include <maniac/maniac.h>

namespace maniac {
    void reset_keys() {
        auto keys = osu::Osu::get_key_subset(config.keys, 9);
        for (auto key : keys) {
            Process::send_keypress(key, false);
        }
    }

	void block_until_playing() {
		while (true) {
			if (osu->is_playing()) {
                break;
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(250));
		}
	}

	void play(const std::vector<Action> &actions) {
		reset_keys();

		size_t cur_i = 0;
		auto cur_time = 0;
		auto raw_actions = actions.data();
		auto total_actions = actions.size();

		while (cur_i < total_actions) {
			if (!osu->is_playing())
				return;

			cur_time = osu->get_game_time();
			while (cur_i < total_actions && (raw_actions + cur_i)->time <= cur_time) {
				(raw_actions + cur_i)->execute();

				cur_i++;
			}

			std::this_thread::sleep_for(std::chrono::nanoseconds(100));
		}
	}

    std::vector<Action> to_actions(std::vector<osu::HitObject> &hit_objects, int32_t min_time) {
        if (hit_objects.empty()) {
            return {};
        }

        const auto columns = std::max_element(hit_objects.begin(),
                                              hit_objects.end(), [](auto a, auto b) {
                    return a.column < b.column; })->column + 1;
        auto keys = osu::Osu::get_key_subset(config.keys, columns);

        if (config.mirror_mod)
            std::reverse(keys.begin(), keys.end());

        std::vector<Action> actions;
        actions.reserve(hit_objects.size() * 2);

        for (auto &hit_object : hit_objects) {
            if (hit_object.start_time < min_time)
                continue;

            if (!hit_object.is_slider)
                hit_object.end_time = hit_object.start_time + config.tap_time;

            actions.emplace_back(keys[hit_object.column], true,
                hit_object.start_time + config.compensation_offset);
            actions.emplace_back(keys[hit_object.column], false,
                hit_object.end_time + config.compensation_offset);
        }

        debug("converted %d hit objects to %d actions", hit_objects.size(), actions.size());

        std::sort(actions.begin(), actions.end());
        actions.erase(std::unique(actions.begin(), actions.end()), actions.end());

        return actions;
    }
}
