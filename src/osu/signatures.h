#pragma once

namespace signatures {
	constexpr auto TIME_SIG_OFFSET = 3;
	constexpr auto TIME_SIG = "EB 0A A1 ? ? ? ? A3\0";

	constexpr auto STATE_SIG_OFFSET = 1;
	constexpr auto STATE_SIG = "A1 ? ? ? ? A3 ? ? ? ? A1 ? ? ? ? A3 ? ? ? ? 83 3D ? ? ? ? 00 0F 84 ? ? ? ? B9 ? ? ? ? E8\0";

	constexpr auto PLAYER_SIG_OFFSET = 7;
	constexpr auto PLAYER_SIG = "FF 50 0C 8B D8 8B 15\0";
}
