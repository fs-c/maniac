#include "common.h"
#include "osu.h"

void Osu::humanize_actions(std::vector<Action> &actions, std::pair<int, int> range) {
	if (!range.first && !range.second)
		return;

	debug("using legacy humanization");

	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<> distr(range.first, range.second);

	for (auto &action : actions) {
		action.time += distr(gen);;
	}

	debug("humanized actions with a range of [%d, %d]", range.first, range.second);
}

std::vector<int> actions_per_frame(const std::vector<Action> &actions, int time_frame = 1000) {
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

void Osu::humanize_actions(std::vector<Action> &actions, int modifier) {
	if (!modifier)
		return;

	const auto actual_modifier = static_cast<double>(modifier) / 100.0;

	constexpr auto frame_range = 1000;
	const auto frames = actions_per_frame(actions, frame_range);

	const auto frames_size = frames.size();
	for (auto &action : actions) {
		const int frame_i = action.time / frame_range;

		if (frame_i >= frames_size) {
			debug("got invalid frame_i (%d)", frame_i);

			continue;
		}

		action.time += frames.at(frame_i) * actual_modifier;
	}

	debug("%s %d %s %d %s %dms %s %f", "humanized", actions.size(), "actions over",
		frames_size, "time frames of", frame_range, "with a modifier of", actual_modifier);
}
