#pragma once

#include <vector>
#include <utility>
#include <maniac/osu.h>
#include <maniac/common.h>
#include <fstream>

namespace maniac {
	struct config {
        int tap_time = 20;
		bool mirror_mod = false;
		int compensation_offset = 0;
		int humanization_modifier = 0;
		std::pair<int, int> randomization_range = { 0, 0 };

        // TODO: Would be good to have the read/write stuff be in a constructor/destructor

        void read_from_file() {
            std::fstream file{"maniac-config", std::fstream::binary | std::fstream::in};

            if (!file.is_open()) {
                debug("couldn't open config file for reading");

                return;
            }

            file.read(reinterpret_cast<char *>(&tap_time), sizeof tap_time);
            file.read(reinterpret_cast<char *>(&mirror_mod), sizeof mirror_mod);
            file.read(reinterpret_cast<char *>(&compensation_offset), sizeof compensation_offset);
            file.read(reinterpret_cast<char *>(&humanization_modifier), sizeof humanization_modifier);
            file.read(reinterpret_cast<char *>(&randomization_range.first), sizeof randomization_range.first);
            file.read(reinterpret_cast<char *>(&randomization_range.second), sizeof randomization_range.second);

            debug("loaded config from file");
        }

        void write_to_file() {
            std::fstream file{"maniac-config", std::fstream::binary | std::fstream::trunc | std::fstream::out};

            if (!file.is_open()) {
                debug("couldn't open config file for writing");

                return;
            }

            file.write(reinterpret_cast<char *>(&tap_time), sizeof tap_time);
            file.write(reinterpret_cast<char *>(&mirror_mod), sizeof mirror_mod);
            file.write(reinterpret_cast<char *>(&compensation_offset), sizeof compensation_offset);
            file.write(reinterpret_cast<char *>(&humanization_modifier), sizeof humanization_modifier);
            file.write(reinterpret_cast<char *>(&randomization_range.first), sizeof randomization_range.first);
            file.write(reinterpret_cast<char *>(&randomization_range.second), sizeof randomization_range.second);

            debug("wrote config to file");
        }
	};

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

	void play(const std::vector<Action> &&actions);

	void block_until_playing();

	void humanize(std::vector<osu::HitObject> &hit_objects, int modifier);
	void randomize(std::vector<osu::HitObject> &hit_objects, std::pair<int, int> range);

	std::vector<Action> to_actions(std::vector<osu::HitObject> &hit_objects, int32_t min_time);
}
