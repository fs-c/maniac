/**
 * Windows only:
 * Searches for a signature (sequence of bytes) in the process, returning the
 * addresses of the end of the first occurence.
 */
void *find_pattern(const unsigned char *signature, unsigned int sig_len)
{
#ifdef ON_WINDOWS
	bool hit = false;
	const size_t read_size = 4096;

	unsigned char chunk[read_size];

	for (size_t i = 0; i < INT_MAX; i += read_size - sig_len) {
		ReadProcessMemory(game_proc, (void *)i, &chunk, read_size, NULL);

		for (size_t a = 0; a < read_size; a++) {
			hit = true;

			for (size_t j = 0; j < sig_len && hit; j++) {
				if (chunk[a + j] != signature[j]) {
					hit = false;
				}
			}

			if (hit) {
				return (void *)(i + a + sig_len);
			}
		}
	}

	return NULL;
#endif /* ON_WINDOWS */
	// Remove warning.
	return signature ? NULL : NULL;
}