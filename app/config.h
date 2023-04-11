#pragma once

#include <maniac/maniac.h>
#include <nlohmann/json.hpp>

namespace config {
    using json = nlohmann::json;

    constexpr auto format_version = 1;
    constexpr auto file_name = "maniac-config.json";

    void read_from_file(struct maniac::config &c);
    void write_to_file(struct maniac::config &c);
}

void config::read_from_file(struct maniac::config &c) {
    std::ifstream file(file_name);

    try {
        const auto data = json::parse(file);

        c.tap_time = data["tap_time"];
        c.mirror_mod = data["mirror_mod"];
        c.compensation_offset = data["compensation_offset"];
        c.randomization_mean = data["randomization_mean"];
        c.randomization_stddev = data["randomization_stddev"];
        c.humanization_type = data["humanization_type"];
        c.humanization_modifier = data["humanization_modifier"];
        c.keys = data["keys"];

        debug("loaded config from file");
    } catch (json::parse_error &err) {
        debug("failed parsing config: '%s'", err.what());
    }
}

void config::write_to_file(struct maniac::config &c) {
    json data = {
            {"format_version", format_version},
            {"tap_time", c.tap_time},
            {"mirror_mod", c.mirror_mod},
            {"compensation_offset", c.compensation_offset},
            {"randomization_mean", c.randomization_mean},
            {"randomization_stddev", c.randomization_stddev},
            {"humanization_type", c.humanization_type},
            {"humanization_modifier", c.humanization_modifier},
            {"keys", c.keys}
    };

    std::ofstream file(file_name);

    file << data.dump(4) << std::endl;

    debug("wrote config to file");
}
