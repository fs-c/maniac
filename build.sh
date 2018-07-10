if [[ "$OSTYPE" == "linux-gnu" ]]; then
	gcc main.c beatmap.c memory.c utils.c -lX11 -lXtst -Ofast
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	gcc main.c beatmap.c memory.c utils.c -Ofast
fi