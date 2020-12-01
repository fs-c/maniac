cmake_minimum_required(VERSION 3.16)
project(maniac VERSION 0.1.0 LANGUAGES CXX)

if (UNIX)
    message(FATAL_ERROR "UNIX systems are currently not supported")
endif ()

# Only do these if this is the main project, not if it is included through add_subdirectory
if(CMAKE_PROJECT_NAME STREQUAL PROJECT_NAME)
    # Optionally set things like CMAKE_CXX_STANDARD, CMAKE_POSITION_INDEPENDENT_CODE here

    message(STATUS "CMake version: ${CMAKE_VERSION}")

    if (NOT CMAKE_BUILD_TYPE)
        set(CMAKE_BUILD_TYPE "Debug")
    endif (NOT CMAKE_BUILD_TYPE)

    message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

    # Ensure -std=c++xx instead of -std=g++xx
    set(CMAKE_CXX_EXTENSIONS OFF)
    # Nicely support folders in IDEs
    set_property(GLOBAL PROPERTY USE_FOLDERS ON)

    add_subdirectory(app)
endif()

add_subdirectory(lib)
