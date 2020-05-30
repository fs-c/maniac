#include "osu.h"

Osu::Osu() : Process("osu!.exe") {
	// TODO: These two really want to be run in parallel.
	auto time_address_ptr = reinterpret_cast<int32_t **>(find_pattern(TIME_SIG,
		TIME_SIG_OFFSET));
	auto state_address_ptr = reinterpret_cast<int32_t **>(find_pattern(STATE_SIG,
		STATE_SIG_OFFSET));

	if (!time_address_ptr) {
		throw std::runtime_error("couldn't find time address pointer");
	}

	debug("found time address pointer at %#x", (uintptr_t)time_address_ptr);

	if (!state_address_ptr) {
		throw std::runtime_error("couldn't find state address pointer");
	}

	debug("found state address pointer at %#x", (uintptr_t)state_address_ptr);

	read_memory<int32_t *>(time_address_ptr, &time_address);
	read_memory<int32_t *>(state_address_ptr, &state_address);
}

Osu::~Osu() = default;
