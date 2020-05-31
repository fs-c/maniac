#include "osu.h"

Osu::Osu() : Process("osu!.exe") {
	// TODO: Some kind of address manager would be good, this isn't particularly nice
	//	 to maintain.

	auto time_addr_ft = std::async(std::launch::async, &Osu::get_pointer_to<int32_t>,
	        this, "time address", TIME_SIG, TIME_SIG_OFFSET);
	auto state_addr_ft = std::async(std::launch::async, &Osu::get_pointer_to<int32_t>,
		this, "state address", STATE_SIG, STATE_SIG_OFFSET);
	auto player_addr_ft = std::async(std::launch::async, &Osu::get_pointer_to<int32_t>,
		this, "player address", PLAYER_SIG, PLAYER_SIG_OFFSET);

	time_address = time_addr_ft.get();
	state_address = state_addr_ft.get();
	player_address = player_addr_ft.get();
}

Osu::~Osu() = default;
