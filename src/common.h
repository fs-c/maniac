#pragma once

#include "Windows.h"

#ifdef DEBUG

	#define debug(...)\
		printf("[debug] [%s] ", __FUNCTION__ );\
		printf(__VA_ARGS__);\
		putchar('\n');\

#else

	#define debug(...)\
		;\

#endif
