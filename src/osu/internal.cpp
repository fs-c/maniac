#include "osu.h"

using namespace osu::internal;

hit_object::hit_object() : base(0), start_time(0), end_time(0), type(0),
	column(0) {}

hit_object::hit_object(uintptr_t base) : base(base) {
	start_time = get_start_time();
	end_time = get_end_time();
	type = get_type();
	column = get_column();
}

[[nodiscard]] int32_t hit_object::get_start_time() const {
	return process->read_memory<int32_t>(base + 0x10);
}

[[nodiscard]] int32_t hit_object::get_end_time() const {
	return process->read_memory<int32_t>(base + 0x14);
}

[[nodiscard]] int32_t hit_object::get_type() const {
	return process->read_memory<int32_t>(base + 0x18);
}

[[nodiscard]] int32_t hit_object::get_column() const {
	return process->read_memory<int32_t>(base + 0x9C);
}

hit_manager_headers::hit_manager_headers(uintptr_t base) : base(base),
	column_count(get_column_count()) {}

[[nodiscard]] int hit_manager_headers::get_column_count() const {
	return static_cast<int>(process->read_memory_safe<float>(
		"column count", base + 0x30));
}

hit_manager::hit_manager(uintptr_t base) : base(base), headers(get_headers()),
	list(get_list()) {}

[[nodiscard]] hit_manager_headers hit_manager::get_headers() const {
	auto addr = base + 0x30;

	return hit_manager_headers(
		process->read_memory_safe<uintptr_t>("hit manager headers",
			addr));
}

[[nodiscard]] list_container<hit_object> hit_manager::get_list() const {
	return list_container<hit_object>(process->read_memory_safe<uintptr_t>(
		"hitpoint list container", base + 0x48));
}

map_player::map_player(uintptr_t base) : base(base), manager(get_hit_manager()) {}

[[nodiscard]] hit_manager map_player::get_hit_manager() const {
	return hit_manager(process->read_memory_safe<uintptr_t>("hit manager",
		base + 0x40));
}
