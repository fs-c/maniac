#pragma once

#include <maniac/common.h>

#include <utility>
#include <fstream>

namespace maniac {
    struct config {
        static constexpr auto STATIC_HUMANIZATION = 0;
        static constexpr auto DYNAMIC_HUMANIZATION = 1;

        int tap_time = 20;
        bool mirror_mod = false;
        int compensation_offset = -15;
        int humanization_modifier = 0;
        std::pair<int, int> randomization_range = { 0, 0 };
        int humanization_type = DYNAMIC_HUMANIZATION;

        // TODO: This isn't configurable yet, use a non-shit config format
        std::string keys = "asdfjkl;";

        // TODO: Would be good to have the read/write stuff be in a constructor/destructor

        void read_from_file() {
            std::fstream file{"maniac-config", std::fstream::binary | std::fstream::in};

            if (!file.is_open()) {
                debug("couldn't open config file for reading");

                return;
            }

            file.read(reinterpret_cast<char *>(&tap_time), sizeof tap_time);
            file.read(reinterpret_cast<char *>(&mirror_mod), sizeof mirror_mod);
            file.read(reinterpret_cast<char *>(&compensation_offset), sizeof compensation_offset);
            file.read(reinterpret_cast<char *>(&humanization_modifier), sizeof humanization_modifier);
            file.read(reinterpret_cast<char *>(&randomization_range.first), sizeof randomization_range.first);
            file.read(reinterpret_cast<char *>(&randomization_range.second), sizeof randomization_range.second);
            file.read(reinterpret_cast<char *>(&humanization_type), sizeof humanization_type);

            debug("loaded config from file");
        }

        void write_to_file() {
            std::fstream file{"maniac-config", std::fstream::binary | std::fstream::trunc | std::fstream::out};

            if (!file.is_open()) {
                debug("couldn't open config file for writing");

                return;
            }

            file.write(reinterpret_cast<char *>(&tap_time), sizeof tap_time);
            file.write(reinterpret_cast<char *>(&mirror_mod), sizeof mirror_mod);
            file.write(reinterpret_cast<char *>(&compensation_offset), sizeof compensation_offset);
            file.write(reinterpret_cast<char *>(&humanization_modifier), sizeof humanization_modifier);
            file.write(reinterpret_cast<char *>(&randomization_range.first), sizeof randomization_range.first);
            file.write(reinterpret_cast<char *>(&randomization_range.second), sizeof randomization_range.second);
            file.write(reinterpret_cast<char *>(&humanization_type), sizeof humanization_type);

            debug("wrote config to file");
        }
    };
}

