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

template<typename T>
class pointer {
	T ptr;

public:
	explicit pointer(T ptr) : ptr(ptr) {}

	T add_bytes(unsigned int num) const {
		return reinterpret_cast<T>(reinterpret_cast<uintptr_t>(ptr) + num);
	}
};

namespace osu_internal {
	Osu *osu;

	struct hit_object {
		hit_object *base;

		explicit hit_object(hit_object *base) : base(base) {}

		[[nodiscard]] auto get_start_time() {
			return osu->read_memory_safe<int32_t>("hit object start time",
				pointer(base).add_bytes(0x10));
		}

		[[nodiscard]] auto get_end_time() {
			return osu->read_memory_safe<int32_t>("hit object end time",
				pointer(base).add_bytes(0x14));
		}

		[[nodiscard]] auto get_type() {
			return osu->read_memory_safe<int32_t>("hit object type",
				pointer(base).add_bytes(0x18));
		}
	};

	template<typename T>
	struct list_contents {
		list_contents *base;

		explicit list_contents(list_contents *base) : base(base) {}

		[[nodiscard]] auto get_data() {
			return osu->read_memory_safe<T *>("list data",
				pointer<list_contents<T> *>(base).add_bytes(0x8));
		}
	};

	template<typename T>
	struct list_container {
		list_container *base;

		explicit list_container(list_container *base) : base(base) {}

		[[nodiscard]] auto get_size() {
			return osu->read_memory_safe<size_t>("list contents size",
				pointer<size_t *>(base).add_bytes(0xC));
		}

		[[nodiscard]] auto get_contents() {
			auto contents = list_contents(osu->read_memory_safe<list_contents<T> *>("list contents address",
				pointer<list_contents<hit_object> *>(base).add_bytes(0x4)
			));

			auto size = get_size();
			auto data = contents.get_data();

			return std::vector(data[0], data[size - 1]);
		}
	};

	struct hit_manager_headers {
		hit_manager_headers *base;

		explicit hit_manager_headers(hit_manager_headers *base) : base(base) {}

		[[nodiscard]] auto get_column_count() const {
			return static_cast<int>(osu->read_memory_safe<float>("column count",
				pointer(base).add_bytes(0x30)));
		}

		[[nodiscard]] auto get_list() const {
			return list_container(osu->read_memory_safe<list_container<hit_object> *>(
				"hitpoint list container", pointer(base).add_bytes(0x48)));
		}
	};

	struct hit_manager {
		uintptr_t base;

		explicit hit_manager(hit_manager *base) {
			this->base = reinterpret_cast<uintptr_t>(base);

			debug("hit manager base is %#x", base);
		}

		[[nodiscard]] auto get_headers() const {
			auto addr = base + 0x30;

			return hit_manager_headers(
				osu->read_memory_safe<hit_manager_headers *>(
					"hit manager headers", addr)
			);
		}
	};

	struct map_player {
		map_player *base;

		explicit map_player(map_player *base) : base(base) {}

		[[nodiscard]] auto get_hit_manager() const {
			return hit_manager(
				osu->read_memory_safe<hit_manager *>("hit manager",
					pointer(base).add_bytes(0x40))
			);
		}
	};
}

void Osu::alt_get_actions() {
};

// TODO: Break up into smaller functions, this is ugly as all hell.
std::vector<Action> Osu::get_actions() {
	osu_internal::osu = this;

	auto player_address = read_memory_safe<uintptr_t>("player address", player_pointer);
	auto manager_address = read_memory_safe<uintptr_t>("manager address", player_address + 0x40);
	debug("got hit object manager address: %#x", player_address);

	auto headers_address = read_memory_safe<uintptr_t>("headers address", manager_address + 0x30);
	auto mem_column_count = static_cast<int32_t>(read_memory_safe<float>("column count", headers_address + 0x30));
	debug("column count in memory is %d", mem_column_count);

	if (mem_column_count <= 0) {
		debug("got invalid column count in memory");
	}

	auto list_pointer = read_memory_safe<uintptr_t>("list pointer", manager_address + 0x48);
	auto list_address = read_memory_safe<uintptr_t>("list address", list_pointer + 0x4);
	auto list_size = read_memory_safe<size_t>("list size", list_pointer + 0xC);
	debug("got hit object list at %#x (size %d)", list_address, list_size);

	if (list_size <= 0) {
		throw std::runtime_error("got invalid list size");
	}

	std::vector<Action> actions;

	size_t i;
	size_t failed = 0;
	int largest_column = 0;
	for (i = 0; i < list_size; i++) {
		try {
			auto object_address = read_memory<uintptr_t>(list_address + 0x8 + 0x4 * i);

			auto start_time = read_memory<int32_t>(object_address + 0x10);
			auto end_time = read_memory<int32_t>(object_address + 0x14);

			// auto type = read_memory<int32_t>(object_address + 0x18);
			auto column = read_memory<int32_t>(object_address + 0x9C);

			if (start_time == end_time) {
				end_time += tap_time;
			}

			if (column > largest_column)
				largest_column = column;

			// Hacky: `column` is written into a field that should hold the key itself.
			actions.emplace_back(column, true, start_time + default_delay);
			actions.emplace_back(column, false, end_time + default_delay);
		} catch (std::exception &err) {
			failed++;

			// It's okay if a couple fail...
		}
	}

	// ...but not all of them.
	if (i == failed) {
		throw std::runtime_error("failed parsing hitpoints");
	}

	// 0 based to 1 based.
	auto actual_columns = largest_column + 1;

	if (largest_column != mem_column_count) {
		debug("actual and memory column counts don't match, defaulting to actual (%d)",
			largest_column);
	}

	auto keys = get_key_subset(actual_columns);
	debug("using key subset '%s'", keys.c_str());

	for (auto &action : actions) {
		action.key = keys.at(action.key);
	}

	debug("%s %d %s %d %s %d %s", "parsed", i, "out of", list_size, "hit objects into",
		actions.size(), "actions");

	std::sort(actions.begin(), actions.end());
	actions.erase(std::unique(actions.begin(), actions.end()), actions.end());

	return actions;
}

size_t Osu::discard_actions(std::vector<Action> &actions) {
	auto cur_time = get_game_time();

	size_t discarded = 0;
	for (auto action = actions.begin(); action != actions.end();) {
		if (action->time <= cur_time && ++discarded) {
			action = actions.erase(action);
		} else {
			action++;
		}
	}

	return discarded;
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
