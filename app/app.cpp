#include "window.h"
#include "config.h"
#include <maniac/maniac.h>

static bool showHelpPopup = false;
static std::string currentHelpText;
static float helpPopupAlpha = 0.0f;
static bool closingHelpPopup = false;
static ImGuiID lastHelpID = 0;

static bool showSettingsWindow = false;
static float settingsAlpha = 0.0f;
static bool closingSettings = false;
static char keysInput4K[9] = "";  // 4 keys + null terminator, with extra space
static char keysInput7K[12] = ""; // 7 keys + null terminator, with extra space

static void help_marker(const char *desc) {
    ImGuiID id = ImGui::GetID(desc);
    char button_id[32];
    sprintf(button_id, "?##%u", id);
    
    ImVec4 textColor = ImGui::GetStyle().Colors[ImGuiCol_Text];
    ImVec4 hoveredTextColor = ImVec4(1.0f, 1.0f, 1.0f, 1.0f);
    
    ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0, 0, 0, 0));
    
    ImGui::PushStyleColor(ImGuiCol_Text, textColor);
    ImGui::PushStyleColor(ImGuiCol_TextDisabled, textColor);
    
    if (ImGui::Button(button_id)) {
        if (showHelpPopup && lastHelpID != id) {
            showHelpPopup = false;
            closingHelpPopup = false;
            helpPopupAlpha = 0.0f;
        }
        
        showHelpPopup = true;
        closingHelpPopup = false;
        currentHelpText = desc;
        lastHelpID = id;
    }
    
    if (ImGui::IsItemHovered()) {
        ImGui::PopStyleColor();
        ImGui::PushStyleColor(ImGuiCol_Text, hoveredTextColor);
    }
    
    ImGui::PopStyleColor(5);
}

static void render_help_popup() {
    if (!showHelpPopup && !closingHelpPopup) return;
    
    if (closingHelpPopup) {
        helpPopupAlpha = std::max(0.0f, helpPopupAlpha - ImGui::GetIO().DeltaTime * 6.0f);
        if (helpPopupAlpha <= 0.0f) {
            closingHelpPopup = false;
            lastHelpID = 0;
            return;
        }
    } else {
        helpPopupAlpha = std::min(helpPopupAlpha + ImGui::GetIO().DeltaTime * 6.0f, 1.0f);
    }
    
    ImGui::SetNextWindowPos(ImVec2(0, 0));
    ImGui::SetNextWindowSize(ImGui::GetIO().DisplaySize);
    
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, helpPopupAlpha * 0.5f);
    ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.0f, 0.0f, 0.0f, 0.7f));
    
    ImGui::Begin("##overlay", NULL, 
        ImGuiWindowFlags_NoTitleBar | 
        ImGuiWindowFlags_NoResize | 
        ImGuiWindowFlags_NoScrollbar | 
        ImGuiWindowFlags_NoInputs | 
        ImGuiWindowFlags_NoSavedSettings | 
        ImGuiWindowFlags_NoNavFocus |
        ImGuiWindowFlags_NoBringToFrontOnFocus);
    ImGui::End();
    
    ImGui::PopStyleColor();
    ImGui::PopStyleVar();
    
    ImVec2 center = ImGui::GetMainViewport()->GetCenter();
    ImVec2 popupSize = ImVec2(400.0f, 0.0f);
    ImVec2 popupPos = ImVec2(center.x - popupSize.x * 0.5f, center.y - 100.0f);
    
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, helpPopupAlpha);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 8.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15.0f, 15.0f));
    
    ImGui::SetNextWindowPos(popupPos);
    ImGui::SetNextWindowSize(popupSize);
    
    ImGui::PushFont(ImGui::GetIO().Fonts->Fonts[0]);
    float origScale = ImGui::GetIO().FontGlobalScale;
    ImGui::GetIO().FontGlobalScale = 1.25f;
    
    ImGui::Begin("##helpPopup", NULL, 
        ImGuiWindowFlags_NoTitleBar | 
        ImGuiWindowFlags_NoResize | 
        ImGuiWindowFlags_AlwaysAutoResize | 
        ImGuiWindowFlags_NoSavedSettings | 
        ImGuiWindowFlags_NoNavFocus);
    
    if (ImGui::Button("X", ImVec2(25, 0))) {
        showHelpPopup = false;
        closingHelpPopup = true;
    }
    
    ImGui::SameLine();
    ImGui::SetCursorPosX(ImGui::GetWindowWidth() * 0.5f - ImGui::CalcTextSize("Help").x * 0.5f);
    ImGui::Text("Help");
    ImGui::Separator();
    
    ImGui::PushTextWrapPos(ImGui::GetFontSize() * 25.0f);
    ImGui::TextUnformatted(currentHelpText.c_str());
    ImGui::PopTextWrapPos();
    
    if (ImGui::IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup) && ImGui::IsMouseClicked(0)) {
        showHelpPopup = false;
        closingHelpPopup = true;
    }
    
    ImGui::End();
    
    ImGui::GetIO().FontGlobalScale = origScale;
    ImGui::PopFont();
    ImGui::PopStyleVar(3);
    
    if (ImGui::IsMouseClicked(0) && !ImGui::IsAnyItemHovered()) {
        showHelpPopup = false;
        closingHelpPopup = true;
    }
}

static void render_settings_window() {
    if (!showSettingsWindow && !closingSettings) return;
    
    if (closingSettings) {
        settingsAlpha = std::max(0.0f, settingsAlpha - ImGui::GetIO().DeltaTime * 6.0f);
        if (settingsAlpha <= 0.0f) {
            closingSettings = false;
            return;
        }
    } else {
        settingsAlpha = std::min(settingsAlpha + ImGui::GetIO().DeltaTime * 6.0f, 1.0f);
    }
    
    ImVec2 fullScreenSize = ImGui::GetIO().DisplaySize;
    ImDrawList* drawList = ImGui::GetBackgroundDrawList();
    drawList->AddRectFilled(ImVec2(0, 0), fullScreenSize, IM_COL32(0, 0, 0, (int)(180 * settingsAlpha)));
    
    ImVec2 center = ImGui::GetMainViewport()->GetCenter();
    ImVec2 settingsSize = ImVec2(300.0f, 250.0f);
    ImVec2 settingsPos = ImVec2(center.x - settingsSize.x * 0.5f, center.y - settingsSize.y * 0.5f);
    
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, settingsAlpha);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 10.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(20.0f, 20.0f));
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(5.0f, 5.0f));
    ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.18f, 0.18f, 0.19f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(0.24f, 0.24f, 0.25f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_FrameBg, ImVec4(0.25f, 0.25f, 0.26f, 1.0f));
    
    ImGui::SetNextWindowPos(settingsPos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(settingsSize);
    
    bool open = true;
    if (ImGui::Begin("##SettingsWindow", &open, 
            ImGuiWindowFlags_NoTitleBar | 
            ImGuiWindowFlags_NoMove | 
            ImGuiWindowFlags_NoResize | 
            ImGuiWindowFlags_NoScrollbar | 
            ImGuiWindowFlags_NoSavedSettings)) {
        
        ImGui::SetCursorPos(ImVec2(settingsSize.x - 45, 5));
        if (ImGui::Button("X", ImVec2(25, 25))) {
            if (strlen(keysInput4K) == 4) {
                maniac::config.keys4k = keysInput4K;
            }
            
            if (strlen(keysInput7K) == 6) {
                maniac::config.keys7k = keysInput7K;
            }
            
            config::write_to_file(maniac::config);
            showSettingsWindow = false;
            closingSettings = true;
        }
        
        ImGui::SetCursorPos(ImVec2(20, 10));
        ImGui::Text("Key Settings");
        ImGui::SetCursorPos(ImVec2(20, 35));
        ImGui::Separator();
        
        ImGui::SetCursorPos(ImVec2(20, 50));
        ImGui::Text("4K Mode Keys:");
        ImGui::SetCursorPos(ImVec2(20, 75));
        ImGui::PushItemWidth(settingsSize.x - 40);
        ImGui::InputText("##4kkeys", keysInput4K, IM_ARRAYSIZE(keysInput4K));
        
        if (strlen(keysInput4K) != 4) {
            ImGui::SetCursorPos(ImVec2(20, 100));
            ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "Must be exactly 4 keys");
        }
        
        ImGui::SetCursorPos(ImVec2(20, 125));
        ImGui::Text("7K Mode Keys (without space):");
        ImGui::SetCursorPos(ImVec2(20, 150));
        ImGui::InputText("##7kkeys", keysInput7K, IM_ARRAYSIZE(keysInput7K));
        ImGui::PopItemWidth();
        
        if (strlen(keysInput7K) != 6) {
            ImGui::SetCursorPos(ImVec2(20, 175));
            ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "Must be exactly 6 keys");
        }
        
        ImGui::SetCursorPos(ImVec2(20, 200));
        ImGui::TextWrapped("Enter 6 keys for 7K - space will be added in the middle");
    }
    ImGui::End();
    
    if (!open && !closingSettings) {
        if (strlen(keysInput4K) == 4) {
            maniac::config.keys4k = keysInput4K;
        }
        
        if (strlen(keysInput7K) == 6) {
            maniac::config.keys7k = keysInput7K;
        }
        
        config::write_to_file(maniac::config);
        showSettingsWindow = false;
        closingSettings = true;
    }
    
    ImGui::PopStyleColor(3);
    ImGui::PopStyleVar(4);
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

    config::read_from_file(maniac::config);
    
    if (maniac::config.keys4k.empty()) {
        maniac::config.keys4k = "askl";
    }
    
    if (maniac::config.keys7k.empty() || maniac::config.keys7k.size() < 6) {
        maniac::config.keys7k = "asdkl;";
    }
    
    strncpy(keysInput4K, maniac::config.keys4k.c_str(), sizeof(keysInput4K) - 1);
    
    std::string keys7k_no_space = maniac::config.keys7k;
    if (keys7k_no_space.length() > 6) {
        keys7k_no_space.erase(std::remove(keys7k_no_space.begin(), keys7k_no_space.end(), ' '), keys7k_no_space.end());
    }
    strncpy(keysInput7K, keys7k_no_space.c_str(), sizeof(keysInput7K) - 1);
    
    keysInput4K[sizeof(keysInput4K) - 1] = '\0';
    keysInput7K[sizeof(keysInput7K) - 1] = '\0';

    auto run = [&message](osu::Osu &osu) {
        maniac::osu = &osu;

        message = "waiting for beatmap...";

        maniac::block_until_playing();

        message = "found beatmap";

        std::vector<osu::HitObject> hit_objects;

        for (int i = 0; i < 10; i++) {
            try {
                hit_objects = osu.get_hit_objects();

                break;
            } catch (std::exception &err) {
                debug("get hit objects attempt %d failed: %s", i + 1, err.what());

                std::this_thread::sleep_for(std::chrono::milliseconds(200));
            }
        }

        if (hit_objects.empty()) {
            throw std::runtime_error("failed getting hit objects");
        }

        set_priority_class(HIGH_PRIORITY_CLASS);

        maniac::randomize(hit_objects, maniac::config.randomization_mean, maniac::config.randomization_stddev);

        if (maniac::config.humanization_type == maniac::config::STATIC_HUMANIZATION) {
            maniac::humanize_static(hit_objects, maniac::config.humanization_modifier);
        }

        if (maniac::config.humanization_type == maniac::config::DYNAMIC_HUMANIZATION) {
            maniac::humanize_dynamic(hit_objects, maniac::config.humanization_modifier);
        }

        auto actions = maniac::to_actions(hit_objects, osu.get_game_time());

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
        
        ImGui::SameLine(ImGui::GetWindowWidth() - 80);
        if (ImGui::Button("Settings", ImVec2(70, 20))) {
            showSettingsWindow = true;
        }
        
        horizontal_break();

        ImGui::Combo("Humanization Type", &maniac::config.humanization_type, "Static\0Dynamic (new)\0\0");
        ImGui::SameLine();
        help_marker("Static: Changes timing based on whole song.\nDynamic: Changes timing based on each note section.");

        ImGui::InputInt("Humanization", &maniac::config.humanization_modifier, 0, 1000);
        ImGui::SameLine();
        help_marker("Makes your timing feel more human-like. Higher number = more human timing.");

        horizontal_break();

        ImGui::Text("Adds a random hit-time offset generated using a normal \ndistribution with given mean and standard deviation.");
        ImGui::Dummy(ImVec2(0.0f, 2.0f));

        ImGui::InputInt("Randomization Mean", &maniac::config.randomization_mean);
        ImGui::SameLine();
        help_marker("Changes when all notes are hit. Positive = later, Negative = earlier.");
        
        ImGui::InputInt("Randomization Stddev", &maniac::config.randomization_stddev);
        ImGui::SameLine();
        help_marker("How random your timing is. Higher number = more random.");

        horizontal_break();

        ImGui::InputInt("Compensation", &maniac::config.compensation_offset);
        ImGui::SameLine();
        help_marker("Fixes lag. Move up or down if notes hit too early or late.");

        ImGui::Checkbox("Mirror Mod", &maniac::config.mirror_mod);

        ImGui::InputInt("Tap time", &maniac::config.tap_time);
        ImGui::SameLine();
        help_marker("How long keys are pressed in milliseconds. Higher = longer key press.");

        horizontal_break();
        ImGui::Dummy(ImVec2(0.0f, 5.0f));

        ImGui::TextDisabled("maniac by fs-c, https://github.com/fs-c/maniac");

        ImGui::End();
        
        render_help_popup();
        render_settings_window();
    });

    config::write_to_file(maniac::config);

    thread.request_stop();

    return EXIT_SUCCESS;
}

