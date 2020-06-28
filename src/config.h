#pragma once

#include "common.h"

#include "dependencies/argh.h"

#include <cstdio>
#include <vector>
#include <utility>

class config {
	argh::parser cmdl;

	// TODO: This is ugly and prone to bugs
	static void print_option(const char *s_form, const char *l_form, const char *desc) {
		std::string broken_desc;
		for (int i = 0; desc[i]; i++) {
			if (!(i % 60) && i != 0) {
				broken_desc.append("\n                         ");
			}

			broken_desc.append(1, desc[i]);
		}

		constexpr auto buf_size = 1024;
		char temp_buf[buf_size];

		sprintf_s(temp_buf, buf_size, "    %s / %-15s %s\n", s_form, l_form, broken_desc.c_str());

		printf("%s", temp_buf);
	}

	static void print_help() {
		printf("\nUsage: maniac [options]\n");
		printf("\nOptions:\n");

		print_option("-h", "--help", "Show this message and exit.");
		print_option("-r", "--randomization", "where arg is `a,b`. Add milliseconds in the range [a,b] to all key presses. If only `a` is provided, `b` implicitly equals `-a`. (default: 0,0, implicit: -5,5)");
		print_option("-u", "--humanization", "For every key press, an offset calculated through (density at that point * (arg / 100)) is added to the time. (default: 0, implicit: 100)");
	}

	template<typename T>
	T get_param(std::initializer_list<const char *const> name, T def, T imp) {
		if (cmdl[name]) {
			return imp;
		}

		auto value = cmdl(name, def);

		T temp;
		value >> temp;

		return temp;
	}

	// TODO: This should be more general.
	std::pair<int, int> string_to_pair(std::string string, std::string delim = ",") {
		std::pair<int, int> out;
		auto pos = string.find_first_of(delim);

		if (pos == std::string::npos) {
			throw std::runtime_error("no delimiter found");
		}

		out.first = std::stoi(string.substr(0, pos));
		out.second = std::stoi(string.substr(pos + 1, std::string::npos));

		return out;
	}

public:
	bool should_exit = false;

	int humanization_modifier;
	std::pair<int, int> randomization_range = { 0, 0 };

	void parse(int argc, char *argv[]) {
		cmdl.parse(argc, argv, argh::parser::PREFER_PARAM_FOR_UNREG_OPTION);

		if (cmdl[{ "-h", "--help" }]) {
			should_exit = true;
			print_help();

			return;
		}

		humanization_modifier = get_param({ "-u", "--humanization" }, 0, 100);
		randomization_range = string_to_pair(get_param<std::string>(
			{ "-r", "--randomization" }, "0,0", "-5,5"));

		debug("humanization modifier: %d", humanization_modifier);
		debug("randomization range: [%d, %d]", randomization_range.first,
			randomization_range.second);
	}
};
