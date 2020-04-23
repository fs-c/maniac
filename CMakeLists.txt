cmake_minimum_required(VERSION 3.5)
project(maniac VERSION 1.0.0 LANGUAGES C)

add_executable(maniac
	src/beatmap.c
	src/game.c
	src/maniac.c
	src/process.c
	src/window.c
)

if (UNIX)
    find_package(X11 REQUIRED)
	target_link_libraries(maniac ${X11_LIBRARIES})

	find_path(XTEST_INCLUDE_DIR NAMES X11/extensions/XTest.h
		PATH_SUFFIXES X11/extensions
		PATHS /opt/X11/include
		DOC "The XTest include directory"
	)

	find_library(XTEST_LIBRARY NAMES Xtst
		PATHS /opt/X11/lib
		DOC "The XTest library"
	)

	include(FindPackageHandleStandardArgs)
	FIND_PACKAGE_HANDLE_STANDARD_ARGS(XTest DEFAULT_MSG XTEST_LIBRARY XTEST_INCLUDE_DIR)

	if (XTEST_FOUND)
		set( XTEST_LIBRARIES ${XTEST_LIBRARY} )
		set( XTEST_INCLUDE_DIRS ${XTEST_INCLUDE_DIR} )
	endif()

	mark_as_advanced(XTEST_INCLUDE_DIR XTEST_LIBRARY)

	target_link_libraries(maniac ${XTEST_LIBRARIES})
endif()

target_include_directories(maniac
	PRIVATE
		${CMAKE_CURRENT_SOURCE_DIR}/src
)

# unused-parameter:	some functions which are stubs on some platforms
#			would cause warnings
# frame-address:	debug functions call unsafe functions, this is
#			irrelevant for prod and acceptable for devel
target_compile_options(maniac PRIVATE
		-Wall -Wextra -Wpedantic -Wno-unused-parameter
		-Wno-frame-address
		-static
        -l:libwinpthread
)

if ("${CMAKE_BUILD_TYPE}" STREQUAL "Debug")
	add_compile_definitions(DEBUG)
endif()

# TODO: Is C11 really required?
target_compile_features(maniac PRIVATE c_std_11)
