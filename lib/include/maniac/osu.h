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

    using HitObject = internal::hit_object;

	class Osu : public Process {
		// TODO: Generic pointers are bad in the long run.
		uintptr_t time_address = 0;
		uintptr_t player_pointer = 0;
        uintptr_t status_pointer = 0;

	public:
		Osu();

		~Osu();

		int32_t get_game_time();

		bool is_playing();

		std::vector<HitObject> get_hit_objects();

        static std::string get_key_subset(const std::string &keys, int column_count);
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
		const auto status = read_memory<int>(status_pointer);

        return status == internal::OSU_STATUS_PLAYING;
	}
}
