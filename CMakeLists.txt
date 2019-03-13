cmake_minimum_required(VERSION 3.5)
project(maniac VERSION 1.0.0 LANGUAGES C)

add_executable(maniac
	src/beatmap.c
	src/game.c
	src/maniac.c
	src/process.c
	src/window.c
)

target_include_directories(maniac
	PRIVATE
		${CMAKE_CURRENT_SOURCE_DIR}/src
)

if("${CMAKE_C_COMPILER_ID}" STREQUAL "GNU")
	# unused-parameter:	some functions which are stubs on some platforms would
	#			cause warnings
	# frame-address:	debug functions call unsafe functions, this is
	#			irrelevant for prod and acceptable for devel
	target_compile_options(maniac PRIVATE
		-Wall -Wextra -Wpedantic -Wno-unused-parameter -Wno-frame-address)
elseif("${CMAKE_C_COMPILER_ID}" STREQUAL "MSVC")
	target_compile_options(maniac PRIVATE
		/W4)
endif()

# TODO: Is C11 really required?
target_compile_features(maniac PRIVATE c_std_11)

# TODO: Import X11 on UNIX systems.