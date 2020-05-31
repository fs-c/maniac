#pragma once

#include "common.h"
#include "../process/process.h"
#include "signatures.h"

#include <future>
#include <vector>
#include <functional>

struct HitObject {
	enum Type {
		HIT_CIRCLE = 1 << 0,
		SLIDER = 1 << 1,
		SPINNER = 1 << 3,
		MANIA_HOLD = 1 << 7
	};

	int32_t type;

	int32_t start_time;
	int32_t end_time;

	int32_t position_x;
	int32_t position_y;

	// For sliders only
	int32_t repeats;
	int32_t pixel_length;
	int32_t curve_type;
};

class Osu : public Process {
	int32_t *time_address = nullptr;
	int32_t *state_address = nullptr;

	// TODO: Generic pointers are bad.
	void *player_address = nullptr;
	void *hit_object_manager_address = nullptr;

	template<typename T>
	T *get_pointer_to(const char *name, const char *pattern, int offset);

public:
	static constexpr auto STATE_PLAY = 2;

	Osu();

	~Osu();

	int32_t get_game_time();

	int32_t get_game_state();

	std::vector<HitObject> get_hit_objects();
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

template<typename T>
T *Osu::get_pointer_to(const char *name, const char *pattern, int offset) {
	auto ptr = find_pattern(pattern);

	if (!ptr) {
		// TODO: Replace this once std::format is a thing in MSVC.
		char buffer[128];
		sprintf(buffer, "%s %s %s", "couldn't find the", name, "pointer");

		throw std::runtime_error(buffer);
	}

	auto address_ptr = reinterpret_cast<T **>(ptr + offset);

	debug("%s %s %s %#x", "found", name, "pointer at", (uintptr_t)address_ptr);

	T *address;
	read_memory<T *>(address_ptr, &address);

	if (!address) {
		// TODO: See above.
		char buffer[128];
		sprintf(buffer, "%s %s %s", "found invalid", name, "pointer");

		throw std::runtime_error(buffer);
	}

	return address;
}
