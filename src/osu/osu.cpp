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

	if (column_count > 9) {
		throw std::runtime_error("maps with more than 9 columns are not supported");
	}

	if (column_count <= 0) {
		throw std::runtime_error("got negative column count");
	}

	const char *keys = "asdfjkl[";
	constexpr auto keys_len = 8;

	const size_t key_subset_len = column_count + 1;
	char *const key_subset = reinterpret_cast<char *>(malloc(key_subset_len));

	if (!key_subset) {
		throw std::runtime_error("failed allocating memory");
	}

	key_subset[column_count] = '\0';

	const size_t subset_offset = (keys_len / 2) - (column_count / 2);

	memmove_s(key_subset, key_subset_len, reinterpret_cast<const void *>(keys + subset_offset),
		keys_len - (subset_offset * 2));

	if (column_count % 2) {
		auto offset = column_count / 2;
		memmove_s(key_subset + offset + 1, key_subset_len + offset + 1, key_subset + offset,
			offset);

		key_subset[column_count / 2] = ' ';
	}

	auto out_string = std::string(key_subset);
	free(key_subset);

	return out_string;
}

std::vector<Action> Osu::get_actions(int32_t min_time, int32_t default_delay) {
	using namespace osu_internal;
	osu = this;

	auto player_address = read_memory_safe<uintptr_t>("player", player_pointer);

	auto player = map_player(player_address);
	// Using auto here leads to compilation errors, no idea why.
	hit_manager manager = player.get_hit_manager();
	auto list = manager.get_list();
	auto hit_points = list.get_contents();

	int32_t highest_col = 0;
	std::vector<Action> actions;

	for (auto &hit_point : hit_points) {
		if (hit_point.start_time == hit_point.end_time) {
			hit_point.end_time += osu->tap_time;
		}

		if (hit_point.column > highest_col) {
			highest_col = hit_point.column;
		}

		if (hit_point.start_time < min_time) {
			continue;
		}

		// Hacky: `column` is written into a field that should hold the key itself.
		actions.emplace_back(hit_point.column, true, hit_point.start_time
			+ default_delay);
		actions.emplace_back(hit_point.column, false, hit_point.end_time
			+ default_delay);
	}

	// highest_col is 0-x, get_key_subset expects 1-x
	auto keys = get_key_subset(highest_col + 1);
	debug("using key subset '%s'", keys.c_str());

	// Hacky cont.
	for (auto &action : actions) {
		action.key = keys.at(action.key);
	}

	debug("got %d actions", actions.size());

	std::sort(actions.begin(), actions.end());
	actions.erase(std::unique(actions.begin(), actions.end()), actions.end());

	return actions;
};

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
