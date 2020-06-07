cmake_minimum_required(VERSION 3.16)
project(maniac VERSION 1.0.0 LANGUAGES CXX)

message(STATUS "CMake version: ${CMAKE_VERSION}")

if (NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Debug")
endif (NOT CMAKE_BUILD_TYPE)

message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

set_target_properties(maniac PROPERTIES OUTPUT_NAME "maniac.exe")

add_executable(maniac
        src/maniac.cpp
        src/process/process.cpp
        src/osu/osu.cpp)

if (UNIX)
    message(FATAL_ERROR "UNIX systems are currently not supported")
endif ()

target_include_directories(maniac PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src)

target_compile_options(maniac PRIVATE)

if ("${CMAKE_BUILD_TYPE}" STREQUAL "Debug")
    # The additional / is important to remove the last character from the path.
    # Note that it does not matter if the OS uses / or \, because we are only
    # saving the path size.
    # Thanks: https://stackoverflow.com/questions/8487986/file-macro-shows-full-path
    string(LENGTH "${CMAKE_SOURCE_DIR}/" SOURCE_PATH_SIZE)

    add_compile_definitions(DEBUG SOURCE_PATH_SIZE=${SOURCE_PATH_SIZE})
    add_compile_options(/W4 /WX /DDEBUG)
else ()
    add_compile_options(/O2)
endif ()

target_compile_features(maniac PRIVATE cxx_std_17)
