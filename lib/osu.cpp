#include <maniac/osu.h>

using namespace osu;

Osu::Osu() : Process("osu!.exe") {
	using namespace signatures;

	// TODO: This fails with an unhelpful error when the pattern wasn't found, refactor.

    time_address = read_memory<uintptr_t>(find_signature(signatures::time));
    debug("found time address: %#x", time_address);

    player_pointer = read_memory<uintptr_t>(find_signature(signatures::player));
    debug("found player pointer: %#x", player_pointer);
}

Osu::~Osu() = default;

std::string Osu::get_key_subset(const std::string &keys, int column_count) {
	// TODO: This is straight from maniac 0.x and needs to be refactored.
	//       Probably best to just have a dictionary of subsets for every
	//       reasonable column count.

	if (column_count > 9) {
		throw std::runtime_error("maps with more than 9 columns are not supported");
	}

	if (column_count <= 0) {
		throw std::runtime_error("got negative column count");
	}

	const size_t key_subset_len = column_count + 1;
	char *const key_subset = reinterpret_cast<char *>(malloc(key_subset_len));

	if (!key_subset) {
		throw std::runtime_error("failed allocating memory");
	}

	key_subset[column_count] = '\0';

	const size_t subset_offset = (keys.length() / 2) - (column_count / 2);

	memmove_s(key_subset, key_subset_len, reinterpret_cast<const void *>(keys.data() + subset_offset),
		keys.length() - (subset_offset * 2));

	if (column_count % 2) {
		auto offset = column_count / 2;
		memmove_s(key_subset + offset + 1, key_subset_len + offset + 1, key_subset + offset,
			offset);

		key_subset[column_count / 2] = ' ';
	}

	auto out_string = std::string{key_subset};
	free(key_subset);

	return out_string;
}

std::vector<HitObject> Osu::get_hit_objects() {
    internal::process = this;

    const auto player_address = read_memory_safe<uintptr_t>("player", player_pointer);
    const auto player = internal::map_player(player_address);

    return player.manager.list.content;
}
