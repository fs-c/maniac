cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	gcc main.c beatmap.c memory.c utils.c -lX11 -lXtst -Ofast -o
	mv maniac ../maniac
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	gcc main.c beatmap.c memory.c utils.c -Ofast -o maniac.exe
	mv maniac.exe ../maniac.exe
fi

cd ..