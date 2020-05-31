#include "maniac.h"

int main() {
	try {
		auto osu = Osu();

		while (true) {
			if (osu.get_game_state() != Osu::STATE_PLAY) {
				debug("standby");
			} else {
			}

			std::this_thread::sleep_for(std::chrono::milliseconds(100));
		}
	} catch (std::exception &err) {
		printf("%s %s\n", "unhandled exception:", err.what());

		return EXIT_FAILURE;
	}

	return EXIT_SUCCESS;
}
