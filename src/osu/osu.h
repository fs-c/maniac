#pragma once

#include "common.h"
#include "../process/process.h"
#include "signatures.h"

#include <algorithm>

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

	bool operator==(const Action &action) const {
		return action.key == key && action.down == down && action.time == time;
	};

	// Only used for debugging
	void log() const;
};

class Osu : public Process {
	int tap_time = 50;
	int default_delay = -20;

	// TODO: Generic pointers are bad in the long run.
	uintptr_t time_address = 0;
	uintptr_t player_pointer = 0;

	static std::string get_key_subset(int column_count);

public:
	Osu();

	~Osu();

	int32_t get_game_time();

	bool is_playing();

	std::vector<Action> get_actions();

	static void execute_actions(Action *action, size_t count);
};

inline int32_t Osu::get_game_time() {
	int32_t time = -1;

	if (!read_memory<int32_t>(time_address, &time)) {
		debug("%s %#x", "failed getting game time at", (unsigned int)time_address);
	}

	return time;
}

inline bool Osu::is_playing() {
	int32_t address;
	read_memory<int32_t>(player_pointer, &address);

	return address != 0;
}

inline void Osu::execute_actions(Action *action, size_t count) {
	// TODO: Look into KEYEVENTF_SCANCODE (see esp. KEYBDINPUT remarks section).

	static auto layout = GetKeyboardLayout(0);

	debug("sending %d", count);

	// TODO: Magic numbers are a bad idea.
	INPUT in[10];

	for (size_t i = 0; i < count; i++) {
		in[i].type = INPUT_KEYBOARD;

		in[i].ki.time = 0;
		in[i].ki.wScan = 0;
		in[i].ki.dwExtraInfo = 0;
		in[i].ki.dwFlags = (action + i)->down ? 0 : KEYEVENTF_KEYUP;
		in[i].ki.wVk = VkKeyScanEx((action + i)->key, layout) & 0xFF;
	}

	if (!SendInput(count, in, sizeof(INPUT))) {
		debug("failed sending %d inputs: %lu", count, GetLastError());
	}
}
