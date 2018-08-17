CC="gcc"
FILES="main.c beatmap.c process.c utils.c process.c"

POST_FLAGS="-Ofast"

cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	${CC} ${FILES} ${POST_FLAGS} -lX11 -lXtst -o maniac
	mv maniac ../maniac
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	${CC} ${FILES} ${POST_FLAGS} -o maniac
	mv maniac.exe ../maniac.exe
fi

cd ..