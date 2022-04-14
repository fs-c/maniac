#include "window.h"
#include <maniac/maniac.h>

static void help_marker(const char *desc) {
    ImGui::TextDisabled("(?)");
    if (ImGui::IsItemHovered()) {
        ImGui::BeginTooltip();
        ImGui::PushTextWrapPos(ImGui::GetFontSize() * 35.0f);
        ImGui::TextUnformatted(desc);
        ImGui::PopTextWrapPos();
        ImGui::EndTooltip();
    }
}

int main(int, char **) {
    std::string message;

    auto run = [&message](osu::Osu &osu) {
        maniac::osu = &osu;

        message = "waiting for beatmap...";

        maniac::block_until_playing();

        message = "found beatmap";

        std::vector<osu::Action> actions;

        for (int i = 0; i < 10; i++) {
            try {
                actions = maniac::get_actions(osu.get_game_time());

                break;
            } catch (std::exception &err) { debug("get actions attempt %d failed: %s", i + 1, err.what());

                std::this_thread::sleep_for(std::chrono::milliseconds(200));
            }
        }

        if (actions.empty()) {
            throw std::runtime_error("failed getting actions");
        }

        maniac::randomize(actions, maniac::config.randomization_range);
        maniac::humanize(actions, maniac::config.humanization_modifier);

        message = "playing";

        maniac::play(actions);
    };

    auto t = std::thread([&message, &run]() {
        while (true) {
            try {
                auto osu = osu::Osu();

                while (true) {
                    run(osu);
                }
            } catch (std::exception &err) {
                (message = err.what()).append(" (retrying in 2 seconds)");

                std::this_thread::sleep_for(std::chrono::seconds(2));
            }
        }
    });

    window::start([&message] {
        ImGui::Begin("maniac", nullptr, ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoResize);

        ImGui::Text("Status: %s", message.c_str());

        ImGui::SliderInt("Humanization", &maniac::config.humanization_modifier, 0, 1000);

        ImGui::DragIntRange2("Randomization", &maniac::config.randomization_range.first,
            &maniac::config.randomization_range.second);

        ImGui::InputInt("Compensation", &maniac::config.compensation_offset);

        ImGui::Checkbox("Mirror Mod", &maniac::config.mirror_mod);

        ImGui::End();
    });

    return EXIT_SUCCESS;
}

