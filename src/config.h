#pragma once

#include "common.h"

#include "dependencies/cxxopts.h"

#include <vector>
#include <utility>

// TODO: This should be a class and needs to be refactored.
namespace config {
	// Temporarily store in a vector because cxxopts doesn't support pair
	std::vector<int> _humanization_range;
	std::pair<int, int> humanization_range;
	bool should_exit = false;

	void parse(int argc, char *argv[]) {
		cxxopts::Options options("maniac", "Simple osu!mania cheat.");

		options.custom_help("[options...]");

		options.add_options()
			("h,help", "Show this message and exit.",
				cxxopts::value(should_exit))
			("u,humanization", "where arg is `a,b`. Add milliseconds in the range [a,b] to all key presses. If only `a` is provided, defaults to [-a,a]. If nothing is provided, defaults to [0,0].",
		    		cxxopts::value<std::vector<int>>(_humanization_range));

		auto result = options.parse(argc, argv);

		if (result.count("help")) {
			printf("%s\n", options.help().c_str());
		}

		if (_humanization_range.empty()) {
			humanization_range.first = 0;
			humanization_range.second = 0;
		} else if (_humanization_range.size() == 1) {
			humanization_range.first = _humanization_range.at(0) * -1;
			humanization_range.second = _humanization_range.at(0);
		} else {
			humanization_range.first = _humanization_range.at(0);
			humanization_range.second =_humanization_range.at(0);
		}
	}
}
