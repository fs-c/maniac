#pragma once

#include "common.h"

#include "dependencies/cxxopts.h"

#include <vector>
#include <utility>

// TODO: This should be a class and needs to be refactored.
namespace config {
	std::vector<int> _randomization_range;

	bool should_exit = false;
	int humanization_modifier = 0;
	std::pair<int, int> randomization_range = { 0, 0 };

	void parse(int argc, char *argv[]) {
		cxxopts::Options options("maniac", "Simple osu!mania cheat.");

		options.custom_help("[options...]");

		options.add_options()
			("h,help", "Show this message and exit.",
				cxxopts::value(should_exit))
			("u,humanization", "For every action, an offset calculated through (density at that point * (arg / 100)) is added to the time. Defaults to 0.",
		    		cxxopts::value<int>(humanization_modifier))
			("r,randomization", "where arg is `a,b`. Add milliseconds in the range [a,b] to all key presses. If only `a` is provided, defaults to [-a,a]. If nothing is provided, defaults to [0,0].");

		auto result = options.parse(argc, argv);

		if (result.count("help")) {
			printf("%s\n", options.help().c_str());
		}

		switch (_randomization_range.size()) {
			case 0: // Already handled
				break;
			case 1: randomization_range = { -_randomization_range.at(0),
				   _randomization_range.at(0) };
				break;
			case 2: randomization_range = {_randomization_range.at(0),
				  _randomization_range.at(1) };
				break;
			default:
				printf("--randomization expects no more than two values to be passed, exiting\n");
				should_exit = true;
		}
	}
}
