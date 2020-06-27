#pragma once

#include "common.h"

#include "dependencies/cxxopts.h"

#include <vector>
#include <utility>

// TODO: This should be a class and needs to be refactored.
namespace config {
	std::vector<int> _randomization_range;

	bool should_exit = false;

	int humanization_modifier;
	std::pair<int, int> randomization_range = { 0, 0 };

	void parse(int argc, char *argv[]) {
		cxxopts::Options options("maniac", "Simple osu!mania cheat.\n");

		options.custom_help("[options...]");

		options.add_options()
			("h,help", "Show this message and exit.",
				cxxopts::value(should_exit))
			("u,humanization", "For every key press, an offset calculated through (density at that point * (arg / 100)) is added to the time.",
		    		cxxopts::value<int>(humanization_modifier)->default_value("0")->implicit_value("100"))
			("r,randomization", "where arg is `a,b`. Add milliseconds in the range [a,b] to all key presses. If only `a` is provided, `b` implicitly equals `-a`.",
				cxxopts::value<std::vector<int>>(_randomization_range)->default_value("0,0")->implicit_value("-5,5"));

		auto result = options.parse(argc, argv);

		if (result.count("help")) {
			printf("%s\n", options.help().c_str());
		}

		debug("length %d (%d,%d)", _randomization_range.size(), _randomization_range.at(0), _randomization_range.at(1));

		switch (_randomization_range.size()) {
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

		debug("should_exit: %d", should_exit);
		debug("humanization modifier: %d", humanization_modifier);
		debug("randomization range: [%d, %d]", randomization_range.first,
			randomization_range.second);
	}
}
