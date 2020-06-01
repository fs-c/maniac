#pragma once

#include "common.h"
#include "../process/process.h"
#include "signatures.h"

#include <set>
#include <future>
#include <functional>

struct HitObject {
	enum Type {
		HIT_CIRCLE = 1 << 0,
		SLIDER = 1 << 1,
		SPINNER = 1 << 3,
		MANIA_HOLD = 1 << 7
	};

	int32_t type;
	int32_t column;

	int32_t start_time;
	int32_t end_time;

	bool operator<(const HitObject &hit_object) const {
		return start_time < hit_object.start_time;
	};

	// Only used for debugging
	void log() const;
};

struct Action {
	char key;
	bool down;
	int32_t time;

	Action(char key, bool down, int32_t time) : key(key), down(down), time(time) { };

	bool operator<(const Action &action) const { return time < action.time; };
};

class Osu : public Process {
	int32_t tap_time = 10;

	// TODO: Generic pointers are bad in the long run.
	uintptr_t time_address = 0;
	uintptr_t player_pointer = 0;

	static std::string get_key_subset(int column_count);

public:
	Osu();

	~Osu();

	int32_t get_game_time();

	bool is_playing();

	std::set<Action> get_actions();
};

inline int32_t Osu::get_game_time() {
	int32_t time = -1;

	if (!read_memory<int32_t>(time_address, &time)) {
		debug("%s %#x", "failed getting game time at", (unsigned int)time_address);
	}

	return time;
}

inline bool Osu::is_playing() {
	uintptr_t address;
	read_memory<uintptr_t>(player_pointer, &address);

	return address != 0;
}
