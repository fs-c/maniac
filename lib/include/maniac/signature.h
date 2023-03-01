#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <sstream>
#include <iostream>
#include <array>
#include <algorithm>

class Signature {
    uintptr_t offset;
    std::vector<std::pair<uint8_t, bool>> pattern;

    // converts a pattern like "AB CD EF ? ? FF" to [ [171, false], ..., [0, true], ... ]
    // where the second pair member signifies whether the byte is a wildcard
    constexpr void parse_string_pattern(const std::string &string_pattern) {
        for (auto cur = string_pattern.data(); *cur; cur++) {
            if (*cur == '?') {
                pattern.emplace_back(0, true);
            } else if (*cur == ' ') {
                continue;
            } else {
                const auto value = strtol(cur, const_cast<char **>(&cur), 16);

                pattern.emplace_back(static_cast<uint8_t>(value), false);

                if (!*cur) {
                    break;
                }
            }
        }
    }

public:
    constexpr Signature(const std::string &pattern, uintptr_t offset) : offset(offset) {
        parse_string_pattern(pattern);
    }

    template<typename T>
    uintptr_t scan(T begin, T end) const {
        const auto comparator = [](auto byte, auto pair) {
            // treat everything as equal to a wildcard, compare others normally
            return pair.second || byte == pair.first;
        };

        const auto result = std::search(begin, end, pattern.begin(), pattern.end(), comparator);

        return result == end ? 0 : (result - begin + offset);
    }
};
