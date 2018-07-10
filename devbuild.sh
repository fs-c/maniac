cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	gcc main.c beatmap.c memory.c utils.c -lX11 -lXtst -Wall -Wextra -Wpedantic
	mv a.out ../a.out
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	gcc main.c beatmap.c memory.c utils.c -Wall -Wextra -Wpedantic
	mv a.exe ../a.exe
fi

cd ..