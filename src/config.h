#pragma once

#include "common.h"

#include "dependencies/cxxopts.h"

#include <vector>
#include <utility>

// TODO: This should be a class and needs to be refactored.
namespace config {
	bool should_exit = false;
	int humanization_modifier = 0;

	void parse(int argc, char *argv[]) {
		cxxopts::Options options("maniac", "Simple osu!mania cheat.");

		options.custom_help("[options...]");

		options.add_options()
			("h,help", "Show this message and exit.",
				cxxopts::value(should_exit))
			("u,humanization", "For every action, an offset calculated through (density at that point * (arg / 100)) is added to the time. Defaults to 0.",
		    		cxxopts::value<int>(humanization_modifier));

		auto result = options.parse(argc, argv);

		if (result.count("help")) {
			printf("%s\n", options.help().c_str());
		}
	}
}
