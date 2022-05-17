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

static void horizontal_break() {
    ImGui::Dummy(ImVec2(0.0f, 5.0f));

    ImGui::Separator();

    ImGui::Dummy(ImVec2(0.0f, 5.0f));
}

static void set_priority_class(int priority) {
    const auto proc = GetCurrentProcess();
    const auto old_priority = GetPriorityClass(proc);

    SetPriorityClass(proc, priority);

    debug("changed priority class from 0x%lx to 0x%lx", old_priority,
            GetPriorityClass(proc));
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
            } catch (std::exception &err) {
                debug("get actions attempt %d failed: %s", i + 1, err.what());

                std::this_thread::sleep_for(std::chrono::milliseconds(200));
            }
        }

        if (actions.empty()) {
            throw std::runtime_error("failed getting actions");
        }

        set_priority_class(HIGH_PRIORITY_CLASS);

        maniac::randomize(actions, maniac::config.randomization_range);
        maniac::humanize(actions, maniac::config.humanization_modifier);

        message = "playing";

        maniac::play(actions);

        set_priority_class(NORMAL_PRIORITY_CLASS);
    };

    auto thread = std::jthread([&message, &run](const std::stop_token& token) {
        while (!token.stop_requested()) {
            try {
                auto osu = osu::Osu();

                while (!token.stop_requested()) {
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
        horizontal_break();

        ImGui::SliderInt("Humanization", &maniac::config.humanization_modifier, 0, 1000);
        ImGui::SameLine();
        help_marker("Advanced hit-time randomization based on hit density.");

        ImGui::DragIntRange2("Randomization", &maniac::config.randomization_range.first,
            &maniac::config.randomization_range.second);
        ImGui::SameLine();
        help_marker("Adds a random hit-time offset between the first and last value, in milliseconds.");

        ImGui::InputInt("Compensation", &maniac::config.compensation_offset);
        ImGui::SameLine();
        help_marker("Adds constant value to all hit-times to compensate for input latency, slower processors, etc.");

        ImGui::Checkbox("Mirror Mod", &maniac::config.mirror_mod);

        ImGui::InputInt("Tap time", &maniac::config.tap_time);
        ImGui::SameLine();
        help_marker("How long a key is held down for a single keypress, in milliseconds.");

        horizontal_break();
        ImGui::Text("Drag or <Ctrl>+Click to change values.");

        ImGui::Dummy(ImVec2(0.0f, 5.0f));

        ImGui::TextDisabled("maniac by fs-c, https://github.com/fs-c/maniac");

        ImGui::End();
    });

    thread.request_stop();

    return EXIT_SUCCESS;
}

