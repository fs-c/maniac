#include <maniac/common.h>
#include <maniac/maniac.h>

void maniac::randomize(std::vector<osu::HitObject> &hit_objects, std::pair<int, int> range) {
	if (!range.first && !range.second)
		return;

	std::random_device rd;
	std::mt19937 gen(rd());

	std::uniform_int_distribution<> distr(range.first, range.second);

	for (auto &hit_object : hit_objects) {
        // if it's a slider we want to randomize start and end, if it's not we ignore end anyway
        hit_object.start_time += distr(gen);
        hit_object.end_time += distr(gen);
	}

	debug("randomized %d hit objects with a range of [%d, %d]", hit_objects.size(), range.first,
		range.second);
}

void maniac::humanize(std::vector<osu::HitObject> &hit_objects, int modifier) {
	if (!modifier)
		return;

	const auto actual_modifier = static_cast<double>(modifier) / 100.0;

    // count number of hits/unit of time (slice size)
	constexpr auto slice_size = 1000;
    auto slices = std::vector<int>{};

    for (const auto &hit_object : hit_objects) {
        slices.at(hit_object.start_time / slice_size)++;

        if (hit_object.is_slider) {
            slices.at(hit_object.end_time / slice_size)++;
        }
    }

    for (auto &hit_object : hit_objects) {
        const auto start_offset = static_cast<int>(slices.at(hit_object.start_time / slice_size) * actual_modifier);

        hit_object.start_time += start_offset;

        if (hit_object.is_slider) {
            const auto end_offset = static_cast<int>(slices.at(hit_object.end_time / slice_size) * actual_modifier);

            hit_object.end_time += end_offset;
        }
    }

	debug("humanized %d hit objects (%d slices of %dms)", hit_objects.size(), slices.size(), slice_size);
}
