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

void maniac::humanize_static(std::vector<osu::HitObject> &hit_objects, int modifier) {
	if (!modifier) {
        return;
    }

	const auto actual_modifier = static_cast<double>(modifier) / 100.0;

    // count number of hits/unit of time (slice size)
	constexpr auto slice_size = 1000;

    const auto latest_hit = std::max_element(hit_objects.begin(), hit_objects.end(), [](auto a, auto b) {
        return a.end_time < b.end_time;
    })->end_time;

    auto slices = std::vector<int>{};
    slices.resize((latest_hit / slice_size) + 1);

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

	debug("statically humanized %d hit objects (%d slices of %dms) with modifier %d", hit_objects.size(), slices.size(), slice_size, modifier);
}

void maniac::humanize_dynamic(std::vector<osu::HitObject> &hit_objects, int modifier) {
    const auto actual_modifier = static_cast<double>(modifier) / 100.0;

    constexpr auto max_delta = 1000;

    for (int i = 0; i < hit_objects.size(); i++) {
        auto &cur = hit_objects.at(i);

        int start_density = 0;

        for (int j = i - 1; j >= 0; j--) {
            const auto pre = hit_objects.at(j);

            if ((pre.start_time + max_delta > cur.start_time) || (pre.is_slider && pre.end_time + max_delta > cur.start_time)) {
                start_density++;
            }
        }

        cur.start_time += static_cast<int>(start_density * actual_modifier);

        if (cur.is_slider) {
            int end_density = 0;

            for (int j = i - 1; j >= 0; j--) {
                const auto pre = hit_objects.at(j);

                if ((pre.start_time + max_delta > cur.end_time) || (pre.is_slider && pre.end_time + max_delta > cur.end_time)) {
                    end_density++;
                }
            }

            cur.end_time += static_cast<int>(end_density * actual_modifier);
        }
    }

    debug("dynamically humanized %d hit objects with modifier %d", hit_objects.size(), modifier);
}
