add_executable(maniac-app app.cpp window.cpp
        dependencies/imgui/imgui.cpp
        dependencies/imgui/backends/imgui_impl_dx9.cpp
        dependencies/imgui/backends/imgui_impl_win32.cpp
        dependencies/imgui/imgui_demo.cpp
        dependencies/imgui/imgui_draw.cpp
        dependencies/imgui/imgui_tables.cpp
        dependencies/imgui/imgui_widgets.cpp)

target_compile_features(maniac-app PRIVATE cxx_std_20)

if (NOT "${CMAKE_BUILD_TYPE}" STREQUAL "Debug")
    target_link_options(maniac-app PRIVATE /SUBSYSTEM:windows /ENTRY:mainCRTStartup)
endif()

target_include_directories(maniac-app PRIVATE dependencies)
target_include_directories(maniac-app PRIVATE dependencies/imgui)

target_link_libraries(maniac-app PRIVATE maniac d3d9.lib)

set_target_properties(maniac-app PROPERTIES OUTPUT_NAME "maniac")
