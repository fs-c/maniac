#pragma once

#include <functional>

#include <imgui/imgui.h>

namespace window {
    void start(const std::function<void()>& body);
}
