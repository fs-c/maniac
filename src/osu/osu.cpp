#include "osu.h"

Osu::Osu() : Process("osu!.exe") {
	using namespace signatures;

	// TODO: Run this in parallel.

	time_address = read_memory<uintptr_t>(find_pattern(TIME_SIG) + TIME_SIG_OFFSET);
	debug("found time address: %#x", time_address);

	player_pointer = read_memory<uintptr_t>(find_pattern(PLAYER_SIG) + PLAYER_SIG_OFFSET);
	debug("found player pointer: %#x", player_pointer);
}

Osu::~Osu() = default;

std::string Osu::get_key_subset(int column_count) {
	// TODO: This pile of shit is copied pretty much straight from maniac 0.x and needs to
	// 	 be refactored.

	const char *keys = "asdfjkl[";
	constexpr auto keys_len = 8;

	char *const key_subset = reinterpret_cast<char *>(malloc(column_count + 1));
	key_subset[column_count] = '\0';

	const size_t subset_offset = (keys_len / 2) - (column_count / 2);

	memmove(key_subset, reinterpret_cast<const void *>(keys + subset_offset),
		keys_len - (subset_offset * 2));

	if (column_count % 2) {
		memmove(key_subset + column_count / 2 + 1, key_subset + column_count / 2,
			column_count / 2);
		key_subset[column_count / 2] = ' ';
	}

	auto out_string = std::string(key_subset);
	free(key_subset);

	return out_string;
}

std::vector<Action> Osu::get_actions() {
	auto player_address = read_memory<uintptr_t>(player_pointer);
	auto manager_address = read_memory<uintptr_t>(player_address + 0x40);
	debug("got hit object manager address: %#x", player_address);

	auto headers_address = read_memory<uintptr_t>(manager_address + 0x30);
	auto column_count = static_cast<int32_t>(read_memory<float>(headers_address + 0x30));
	debug("column count is %d", column_count);

	auto keys = get_key_subset(column_count);
	debug("using key subset '%s'", keys.c_str());

	auto list_pointer = read_memory<uintptr_t>(manager_address + 0x48);
	auto list_address = read_memory<uintptr_t>(list_pointer + 0x4);
	auto list_size = read_memory<size_t>(list_pointer + 0xC);
	debug("got hit object list at %#x", list_address);

	std::vector<Action> actions;

	size_t i;
	for (i = 0; i < list_size; i++) {
		auto object_address = read_memory<uintptr_t>(list_address + 0x8 + 0x4 * i);

		auto start_time = read_memory<int32_t>(object_address + 0x10);
		auto end_time = read_memory<int32_t>(object_address + 0x14);

		// auto type = read_memory<int32_t>(object_address + 0x18);
		auto column = read_memory<int32_t>(object_address + 0x9C);

		if (start_time == end_time) {
			end_time += tap_time;
		}

		actions.emplace_back(keys[column], true, start_time + default_delay);
		actions.emplace_back(keys[column], false, end_time + default_delay);
	}

	debug("%s %d %s %d %s %d %s", "parsed", i, "out of", list_size, "hit objects into",
		actions.size(), "actions");

	std::sort(actions.begin(), actions.end());
	actions.erase(std::unique(actions.begin(), actions.end()), actions.end());

	return actions;
}

void HitObject::log() const {
#ifdef DEBUG
	debug("hit object:");
	debug("    start: %d", start_time);
	debug("    end: %d", end_time);
	debug("    type: %d", type);
	debug("    col: %d", column)
#else
	return;
#endif
}

void Action::log() const {
#ifdef DEBUG
	debug("action:");
	debug("    time: %d", time);
	debug("    key: %c", key);
	debug("    down: %d", down);
#else
	return;
#endif
}
