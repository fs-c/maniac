cmake_minimum_required(VERSION 3.16)
project(maniac VERSION 1.0.0 LANGUAGES CXX)

message(STATUS "CMake version: ${CMAKE_VERSION}")

if (NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Debug")
endif (NOT CMAKE_BUILD_TYPE)

message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

add_executable(maniac
        src/maniac.cpp)

if (UNIX)
    message(FATAL_ERROR "UNIX systems are currently not supported")
endif ()

target_include_directories(maniac PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src)

# unused-parameter:	some functions which are stubs on some platforms
#			        would cause warnings
# frame-address:	debug functions call unsafe functions, this is
#			        irrelevant for prod and acceptable for devel
target_compile_options(maniac PRIVATE
        -Wall -Wextra -Wpedantic -Wno-unused-parameter
        -Wno-frame-address)

if ("${CMAKE_BUILD_TYPE}" STREQUAL "Debug")
    # The additional / is important to remove the last character from the path.
    # Note that it does not matter if the OS uses / or \, because we are only
    # saving the path size.
    # Thanks: https://stackoverflow.com/questions/8487986/file-macro-shows-full-path
    string(LENGTH "${CMAKE_SOURCE_DIR}/" SOURCE_PATH_SIZE)

    add_compile_definitions(DEBUG SOURCE_PATH_SIZE=${SOURCE_PATH_SIZE})
    add_compile_options(-g)
else ()
    message(STATUS "Adding release compile options")
    add_compile_options(-s -O3 -g0)
endif ()

target_compile_features(maniac PRIVATE cxx_std_17)
