#pragma once

#include "common.h"
#include "../process/process.h"
#include "signatures.h"

#include <chrono>
#include <thread>
#include <vector>
#include <random>
#include <algorithm>

namespace osu {

struct Action {
	char key;
	bool down;
	int32_t time;

	Action(char key, bool down, int32_t time) : key(key), down(down),
		time(time) { };

	bool operator < (const Action &action) const {
		return time < action.time;
	};

	bool operator == (const Action &action) const {
		return action.key == key && action.down == down
			&& action.time == time;
	};

	// Only used for debugging
	void log() const;
};

class Osu : public Process {
	int tap_time = 50;

	// TODO: Generic pointers are bad in the long run.
	uintptr_t time_address = 0;
	uintptr_t player_pointer = 0;

	static std::string get_key_subset(int column_count);

public:
	Osu();

	~Osu();

	int32_t get_game_time();

	bool is_playing();

	std::vector<Action> get_actions(int32_t min_time, int32_t default_delay);

	static void execute_actions(Action *actions, size_t count);
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
		debug("%s %#x", "failed getting player address at", address);
	}

	return address != 0;
}

inline void Osu::execute_actions(Action *actions, size_t count) {
	// TODO: Look into KEYEVENTF_SCANCODE (see esp. KEYBDINPUT remarks
	// 	 section).

	static auto layout = GetKeyboardLayout(0);

	// TODO: Magic numbers are a bad idea.
	INPUT in[10];

	if (count > 10) {
		debug("count > 10, setting to 10");

		count = 10;
	}

	for (size_t i = 0; i < count; i++) {
		in[i].type = INPUT_KEYBOARD;

		in[i].ki.time = 0;
		in[i].ki.wScan = 0;
		in[i].ki.dwExtraInfo = 0;
		in[i].ki.dwFlags = (actions + i)->down ? 0 : KEYEVENTF_KEYUP;
		in[i].ki.wVk = VkKeyScanEx((actions + i)->key, layout) & 0xFF;
	}

	if (!SendInput(count, in, sizeof(INPUT))) {
		debug("failed sending %d inputs: %lu", count, GetLastError());
	}
}

namespace internal {

// TODO: Would be enough to take a pointer to the underlying Process
// 	 instance
inline Osu *osu;

struct hit_object {
	uintptr_t base;

	int32_t start_time;
	int32_t end_time;
	int32_t type;
	int32_t column;

	hit_object();
	explicit hit_object(uintptr_t base);

	[[nodiscard]] int32_t get_start_time() const;

	[[nodiscard]] int32_t get_end_time() const;

	[[nodiscard]] int32_t get_type() const;

	[[nodiscard]] int32_t get_column() const;
};

template<typename T>
struct list_container {
	uintptr_t base;

	explicit list_container(uintptr_t base) : base(base) {}

	[[nodiscard]] size_t get_size() {
		return osu->read_memory_safe<size_t>("list contents size",
						     base + 0xC);
	}

	[[nodiscard]] std::vector<T> get_contents() {
		auto contents_address = osu->read_memory_safe<uintptr_t>(
			"list contents address", base + 0x4);

		auto size = get_size();

		std::vector<T> vector;
		vector.reserve(size);

		for (int i = 0; i < size; i++) {
			vector.push_back(T(osu->read_memory<uintptr_t>(
				contents_address + 0x8 + (i * 0x4))));
		}

		return vector;
	}
};

struct hit_manager_headers {
	uintptr_t base;

	explicit hit_manager_headers(uintptr_t base);

	[[nodiscard]] int get_column_count() const;
};

struct hit_manager {
	uintptr_t base;

	explicit hit_manager(uintptr_t base);

	[[nodiscard]] hit_manager_headers get_headers() const;

	[[nodiscard]] list_container<hit_object> get_list() const;
};

struct map_player {
	uintptr_t base;

	explicit map_player(uintptr_t base);

	[[nodiscard]] hit_manager get_hit_manager() const;
};

}

}
