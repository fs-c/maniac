CC="gcc"
FILES="debug/test.c utils.c beatmap.c window.c"

PRE_FLAGS="-DDEBUG"
# unused-parameter:	some functions which are stubs on some platforms would
#			cause warnings
# frame-address:	debug functions call unsafe functions, this is
#			irrelevant for production and an acceptable risk here
POST_FLAGS="-Wall -Wextra -Wpedantic -Wno-unused-parameter -Wno-frame-address -g"

cd src

if [[ "$OSTYPE" == "linux-gnu" ]]; then
	${CC} ${PRE_FLAGS} ${FILES} ${POST_FLAGS} -lX11 -lXtst  -o debug_maniac
	mv debug_maniac ../debug_maniac
elif [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "msys" ]]; then
	${CC} ${PRE_FLAGS} ${FILES} ${POST_FLAGS} -o debug_maniac
	mv debug_maniac.exe ../debug_maniac.exe
fi

cd ..