CC="gcc"
FILES="maniac.c beatmap.c process.c window.c game.c"

PRE_FLAGS="-DDEBUG"
# unused-parameter:	some functions which are stubs on some platforms would
#			cause warnings
# frame-address:	debug functions call unsafe functions, this is
#			irrelevant for production and an acceptable risk here
POST_FLAGS="-Wall -Wextra -Wpedantic -Wno-unused-parameter -Wno-frame-address -g"

cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	${CC} ${PRE_FLAGS} ${FILES} ${POST_FLAGS} -lX11 -lXtst  -o maniac
	mv maniac ../maniac
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	${CC} ${PRE_FLAGS} ${FILES} ${POST_FLAGS} -o maniac
	mv maniac.exe ../maniac.exe
fi

cd ..