#include "common.h"
#include "maniac.h"

void maniac::randomize(std::vector<osu::Action> &actions, std::pair<int, int> range) {
	if (!range.first && !range.second)
		return;

	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<> distr(range.first, range.second);

	for (auto &action : actions) {
		action.time += distr(gen);
	}

	debug("randomized %d actions with a range of [%d, %d]", actions.size(), range.first,
		range.second);
}

static std::vector<int> actions_per_frame(const std::vector<osu::Action> &actions,
	int time_frame = 1000) {
	std::vector<int> frames = {};
	const int chunks_needed = (actions.back().time / time_frame) + 1;

	debug("will need %d frames", chunks_needed);

	for (size_t chunk_i = 0; chunk_i < chunks_needed; chunk_i++) {
		frames.emplace_back(0);

		for (const auto &action : actions) {
			const auto lower_bound = time_frame * chunk_i;
			const auto upper_bound = lower_bound + time_frame;

			if (action.time >= lower_bound && action.time <= upper_bound) {
				frames[chunk_i]++;
			}
		}
	}

	return frames;
}

void maniac::humanize(std::vector<osu::Action> &actions, int modifier) {
	if (!modifier)
		return;

	const auto actual_modifier = static_cast<double>(modifier) / 100.0;

	constexpr auto frame_range = 1000;
	const auto frames = actions_per_frame(actions, frame_range);

	std::random_device rd;
	std::mt19937 gen(rd());

	std::uniform_int_distribution<> offset_distr(-5, 5);

	const auto frames_size = frames.size();
	for (auto &action : actions) {
		const int frame_i = action.time / frame_range;
		const auto random_offset = offset_distr(gen);

		if (frame_i >= frames_size) {
			debug("ignoring invalid frame_i (%d)", frame_i);

			continue;
		}

		const auto offset = (frames.at(frame_i) * actual_modifier) + random_offset;

		action.time += offset;
	}

	debug("%s %d %s %d %s %dms %s %f", "humanized", actions.size(), "actions over",
		frames_size, "time frames of", frame_range, "with a modifier of", actual_modifier);
}
