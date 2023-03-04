#pragma once

#include <maniac/signature.h>

namespace signatures {
    const auto time = Signature{ "EB 0A A1 ? ? ? ? A3", 3 };
    const auto player = Signature{ "A1 ? ? ? ? 8B ? ? ? 00 00 6A 00", 1 };
    const auto status = Signature{ "A1 ? ? ? ? A3 ? ? ? ? A1 ? ? ? ? A3 ? ? ? ? 83 3D ? ? ? ? 00 0F 84 ? ? ? ? B9 ? ? ? ? E8", 1 };

    //	constexpr auto RULESET_SIG_OFFSET = 4;
    //	constexpr auto RULESET_SIG = "73 7A 8B 0D ? ? ? ? 85 C9 74 1F\0";
}
