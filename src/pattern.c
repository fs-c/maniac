#include "osu.h"

// This is obsolete on Windows.
#ifdef ON_LINUX
  #include <limits.h>
#endif /* ON_LINUX */

static inline void *check_chunk(const unsigned char *sig, size_t sig_size,
	unsigned char *buf, size_t buf_size);

void *find_pattern(const unsigned char *signature, unsigned int sig_len)
{
	const size_t read_size = 4096;
	unsigned char chunk[read_size];

	// Get reasonably sized chunks of memory...
	for (size_t off = 0; off < INT_MAX; off += read_size - sig_len) {
		if (!(read_game_memory((void *)off, chunk, read_size))) {
			debug("failed getting chunk at %#x", off);

			continue;
		}

		// ...and check if they contain our signature.
		void *hit = check_chunk(signature, sig_len, chunk, read_size);

		if (hit)
			return (void *)(off + (intptr_t)hit);
	}

	return NULL;
}

// TODO: Use a more efficient pattern matching algorithm.
static inline void *check_chunk(const unsigned char *sig, size_t sig_size,
	unsigned char *buf, size_t buf_size)
{
	// Iterate over the buffer...
	for (size_t i = 0; i < buf_size; i++) {
		int hit = true;

		// ...to check if it includes the pattern/sig.
		for (size_t j = 0; j < sig_size && hit; j++) {
			hit = buf[i + j] == sig[j];
		}

		if (hit) {
			return (void *)(i + sig_size);
		}
	}

	return NULL;
}