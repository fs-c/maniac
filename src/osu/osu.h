#pragma once

#include "common.h"
#include "../process/process.h"

class Osu : public Process {
public:
	static constexpr auto TIME_SIG_OFFSET = 1;
	static constexpr auto TIME_SIG = "EB 0A A1 ? ? ? ? A3\0";

	static constexpr auto STATE_SIG_OFFSET = 1;
	static constexpr auto STATE_SIG = "A1 ? ? ? ? A3 ? ? ? ? A1 ? ? ? ? A3 ? ? ? ? 83 3D ? ? ? ? 00 0F 84 ? ? ? ? B9 ? ? ? ? E8\0";

	static constexpr auto STATE_PLAY = 2;

	int32_t *time_address = nullptr;
	int32_t *state_address = nullptr;

	Osu();

	~Osu();

	int32_t get_game_time();

	int32_t get_game_state();
};

inline int32_t Osu::get_game_time() {
	int32_t time = -1;

	if (!read_memory<int32_t>(time_address, &time)) {
		debug("%s %#x", "failed getting game time at", (unsigned int)time_address);
	}

	return time;
}

inline int32_t Osu::get_game_state() {
	int32_t state = -1;

	if (!read_memory<int32_t>(state_address, &state)) {
		debug("%s %#x", "failed getting game state at", (unsigned int)state_address);
	}

	return state;
}
