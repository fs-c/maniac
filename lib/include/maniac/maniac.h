#pragma once

#include <maniac/osu.h>
#include <maniac/config.h>
#include <maniac/common.h>

#include <vector>

namespace maniac {
    struct Action {
        char key;
        bool down;
        int32_t time;

        short scan_code;

        Action(char key, bool down, int32_t time) : key(key), down(down), time(time) {
            static auto layout = GetKeyboardLayout(0);

            scan_code = VkKeyScanEx(key, layout) & 0xFF;
        };

        bool operator < (const Action &action) const {
            return time < action.time;
        };

        bool operator == (const Action &action) const {
            return action.key == key && action.down == down
                   && action.time == time;
        };

        inline void execute() const {
            Process::send_scan_code(scan_code, down);
        }
    };

	inline config config;

	inline osu::Osu *osu;

	void play(const std::vector<Action> &actions);

	void block_until_playing();

	void randomize(std::vector<osu::HitObject> &hit_objects, std::pair<int, int> range);
    void humanize_static(std::vector<osu::HitObject> &hit_objects, int modifier);
    void humanize_dynamic(std::vector<osu::HitObject> &hit_objects, int modifier);

	std::vector<Action> to_actions(std::vector<osu::HitObject> &hit_objects, int32_t min_time);
}
