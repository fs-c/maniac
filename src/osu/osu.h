#pragma once

#include "common.h"
#include "../process/process.h"
#include "signatures.h"

#include <chrono>
#include <thread>
#include <vector>
#include <random>
#include <algorithm>

// TODO: I don't like the osu namespace since it leads to the ugly `osu::Osu` but
//	 I also don't want `Action` and `internal` to be in the global namespace.

namespace osu {

namespace internal {

inline Process *process;

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

// TODO: I really dislike that the implementation of this has to live here.
template<typename T>
struct list_container {
	uintptr_t base;

	size_t size;
	std::vector<T> content;

	explicit list_container(uintptr_t base) : base(base), size(get_size()),
		content(get_content()) {}

	[[nodiscard]] size_t get_size() {
		return process->read_memory_safe<size_t>("list contents size",
			base + 0xC);
	}

	[[nodiscard]] std::vector<T> get_content() {
		auto contents_address = process->read_memory_safe<uintptr_t>(
			"list contents address", base + 0x4);

		auto size = get_size();

		std::vector<T> vector;
		vector.reserve(size);

		for (int i = 0; i < size; i++) {
			vector.push_back(T(process->read_memory<uintptr_t>(
				contents_address + 0x8 + (i * 0x4))));
		}

		return vector;
	}
};

struct hit_manager_headers {
	uintptr_t base;

	int column_count;

	explicit hit_manager_headers(uintptr_t base);

	[[nodiscard]] int get_column_count() const;
};

struct hit_manager {
	uintptr_t base;

	hit_manager_headers headers;
	list_container<hit_object> list;

	explicit hit_manager(uintptr_t base);

	[[nodiscard]] hit_manager_headers get_headers() const;

	[[nodiscard]] list_container<hit_object> get_list() const;
};

struct map_player {
	uintptr_t base;

	hit_manager manager;

	explicit map_player(uintptr_t base);

	[[nodiscard]] hit_manager get_hit_manager() const;
};

}

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

	inline void execute() {
		Process::send_keypress(key, down);
	}
};

class Osu : public Process {
	int tap_time = 50;

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
		debug("%s %#x", "failed getting player address at", address);
	}

	return address != 0;
}

}
