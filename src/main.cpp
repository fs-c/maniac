#include "common.h"
#include "osu/osu.h"
#include "maniac/maniac.h"
#include "config.h"

#include <cstdio>
#include <thread>
#include <chrono>
#include <cstdlib>

void run(osu::Osu &osu) {
	maniac::osu = &osu;

	printf("[*] waiting for beatmap...\n");

	maniac::block_until_playing();

	printf("[+] found beatmap\n");

	std::vector<osu::Action> actions;

	for (int i = 0; i < 10; i++) {
		try {
			actions = maniac::get_actions(osu.get_game_time());

			break;
		} catch (std::exception &err) {
			debug("get actions attempt %d failed: %s", i + 1, err.what());

			std::this_thread::sleep_for(std::chrono::milliseconds(200));
		}
	}

	if (actions.empty()) {
		throw std::runtime_error("failed getting actions");
	}

	maniac::randomize(actions, maniac::config.randomization_range);
	maniac::humanize(actions, maniac::config.humanization_modifier);

	printf("[+] parsed %d actions\n", actions.size());

	maniac::play(actions);
}

int main(int argc, char *argv[]) {
	try {
		maniac::config.parse(argc, argv);

		if (maniac::config.should_exit) {
			return EXIT_FAILURE;
		}

		auto osu = osu::Osu();

		while (true) {
			run(osu);
		}
	} catch (std::exception &err) {
		printf("%s %s\n", "[-] unhandled exception:", err.what());

		return EXIT_FAILURE;
	}

	return EXIT_SUCCESS;
}

