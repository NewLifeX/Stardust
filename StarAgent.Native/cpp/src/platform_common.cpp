// SPDX-License-Identifier: MIT
// 与操作系统无关的平台实现（三个平台共用一份）。
#include "platform.hpp"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <thread>

namespace staragent::platform {

bool SleepCancelable(int milliseconds, const std::atomic<bool>& cancelled)
{
    const auto deadline = SteadyMsNow() + milliseconds;
    while (!cancelled.load() && SteadyMsNow() < deadline)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(
            std::min<std::int64_t>(50, std::max<std::int64_t>(1, deadline - SteadyMsNow()))));
    }
    return !cancelled.load();
}

} // namespace staragent::platform
