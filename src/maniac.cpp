#include "maniac.h"

void run(Osu &osu) {
	printf("[*] waiting for beatmap...\n");

	while (true) {
		if (!osu.is_playing())
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
		else break;
	}

	printf("[+] found beatmap\n");

	std::vector<Action> actions;
	for (int i = 0; i < 10; i++) {
		try {
			actions = osu.get_actions();

			break;
		} catch (std::exception &err) {
			debug("get actions attempt %d failed: %s", i + 1, err.what());

			std::this_thread::sleep_for(std::chrono::milliseconds(200));
		}
	}

	if (actions.empty()) {
		throw std::runtime_error("failed getting actions");
	}

	auto cur_time = osu.get_game_time();

	size_t discarded = 0;
	for (auto action = actions.begin(); action != actions.end();) {
		if (action->time <= cur_time && ++discarded) {
			action = actions.erase(action);
		} else {
			action++;
		}
	}

	printf("[+] parsed %d actions (discarded: %d)\n", actions.size(), discarded);

	auto cur_i = 0;
	auto raw_actions = actions.data();
	auto total_actions = actions.size();

	while (cur_i < total_actions) {
		if (!osu.is_playing())
			return;

		cur_time = osu.get_game_time();
		while (cur_i < total_actions && (raw_actions + cur_i)->time <= cur_time) {
			Osu::execute_actions(raw_actions + cur_i, 1);

			cur_i++;
		}

		std::this_thread::sleep_for(std::chrono::milliseconds(5));
	}
}

int main(int argc, char *argv[]) {
	try {
		config::parse(argc, argv);

		if (config::should_exit) {
			return EXIT_FAILURE;
		}

		auto osu = Osu();

		while (true) {
			run(osu);
		}
	} catch (std::exception &err) {
		printf("%s %s\n", "[-] unhandled exception:", err.what());

		return EXIT_FAILURE;
	}

	return EXIT_SUCCESS;
}
