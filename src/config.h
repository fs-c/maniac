#pragma once

#include "common.h"

#include "dependencies/cxxopts.h"

namespace config {
	int humanization = 0;
	bool should_exit = false;

	void parse(int argc, char *argv[]) {
		cxxopts::Options options("maniac", "Simple osu!mania cheat.");

		options.add_options()
			("h,help", "Show this message and exit",
				cxxopts::value(should_exit))
			("humanization", "Set the level of humanization",
				cxxopts::value<int>(humanization));

		auto result = options.parse(argc, argv);

		if (result.count("help")) {
			printf("%s\n", options.help().c_str());
		}
	}
}
