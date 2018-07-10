if [[ "$OSTYPE" == "linux-gnu" ]]; then
	gcc main.c beatmap.c memory.c utils.c -lX11 -lXtst -Wall -Wextra -Wpedantic
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	gcc main.c beatmap.c memory.c utils.c -Wall -Wextra -Wpedantic
fi