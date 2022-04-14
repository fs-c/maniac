add_library(maniac maniac.cpp osu.cpp process.cpp humanization.cpp)
add_library(maniac::maniac ALIAS maniac)

if ("${CMAKE_BUILD_TYPE}" STREQUAL "Debug")
    # The additional / is important to remove the last character from the path.
    # Note that it does not matter if the OS uses / or \, because we are only
    # saving the path size.
    # Thanks: https://stackoverflow.com/questions/8487986/file-macro-shows-full-path
    string(LENGTH "${CMAKE_SOURCE_DIR}/" SOURCE_PATH_SIZE)

    target_compile_definitions(maniac PRIVATE SOURCE_PATH_SIZE=${SOURCE_PATH_SIZE})
    target_compile_options(maniac PRIVATE /DDEBUG)
else ()
    target_compile_options(maniac PRIVATE /O2)
endif()

# We need this directory, and users of the library will need it too
target_include_directories(maniac PUBLIC include)

# All users of the library will need at least C++17
target_compile_features(maniac PUBLIC cxx_std_17)
