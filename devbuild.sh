cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	gcc main.c beatmap.c process.c utils.c -lX11 -lXtst -Wall -Wextra -Wpedantic -o maniac
	mv maniac ../maniac
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	gcc main.c beatmap.c process.c utils.c -Wall -Wextra -Wpedantic -o maniac
	mv maniac.exe ../maniac.exe
fi

cd ..