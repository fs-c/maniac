#include "osu.h"

Osu::Osu() : Process("osu!.exe") {
	// TODO: These two really want to be run in parallel.
	time_address = get_address<int32_t>("time address", TIME_SIG, TIME_SIG_OFFSET);
	state_address = get_address<int32_t>("state address", STATE_SIG, STATE_SIG_OFFSET);
}

Osu::~Osu() = default;
