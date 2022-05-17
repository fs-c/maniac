#pragma once

#include <chrono>
#include <thread>
#include <vector>
#include <random>
#include <algorithm>
#include <maniac/common.h>
#include <maniac/process.h>
#include <maniac/osu/signatures.h>

namespace osu {
	namespace internal {
		#include <maniac/osu/internal.h>
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

		// Only used for debugging
		void log() const;

		inline void execute() const {
			Process::send_scan_code(scan_code, down);
		}
	};

	class Osu : public Process {
		// TODO: Generic pointers are bad in the long run.
		uintptr_t time_address = 0;
		uintptr_t player_pointer = 0;

	public:
		Osu();

		~Osu();

		int32_t get_game_time();

		bool is_playing();

		internal::map_player get_map_player();

		static std::string get_key_subset(int column_count);
	};

	inline int32_t Osu::get_game_time() {
		int32_t time = -1;

		if (!read_memory<int32_t>(time_address, &time)) {
			debug("%s %#x", "failed getting game time at",
				(unsigned int)(time_address));
		}

		return time;
	}

	inline bool Osu::is_playing() {
		uintptr_t address = 0;

		size_t read = read_memory<uintptr_t>(player_pointer, &address, 1);

		if (!read) {
			debug("%s %#x", "failed getting player address at", player_pointer);
		}

		return address != 0;
	}
}
