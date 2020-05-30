#include "maniac.h"

int main() {
	try {
		auto osu = Osu();

		while (true) {
			printf("%d\n", osu.get_game_state());

			std::this_thread::sleep_for(std::chrono::seconds(1));
		}
	} catch (std::exception &err) {
		printf("%s %s\n", "unhandled exception:", err.what());

		return EXIT_FAILURE;
	}

	return EXIT_SUCCESS;
}
