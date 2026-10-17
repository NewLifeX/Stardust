// SPDX-License-Identifier: MIT
#pragma once

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <random>
#include <thread>

namespace staragent {

// Exponential backoff with jitter: 1s, 2s, 4s, 8s, 16s, 30s cap.
class Backoff
{
public:
    Backoff() : random_(std::random_device{}()) {}

    int NextDelayMs()
    {
        std::int64_t delay = 1000;
        for (int i = 0; i < attempt_ && delay < 30000; i++) delay *= 2;
        if (delay > 30000) delay = 30000;
        attempt_++;

        const double factor = 1.0 + (uniform_(random_) * 2.0 - 1.0) * 0.2;
        delay = static_cast<std::int64_t>(static_cast<double>(delay) * factor);
        if (delay < 1000) delay = 1000;
        if (delay > 30000) delay = 30000;
        return static_cast<int>(delay);
    }

    void Reset() { attempt_ = 0; }

    bool Wait(const std::atomic<bool>& stopping)
    {
        const int delay = NextDelayMs();
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(delay);
        while (!stopping.load() && std::chrono::steady_clock::now() < deadline)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        }
        return !stopping.load();
    }

private:
    int attempt_ = 0;
    std::mt19937 random_;
    std::uniform_real_distribution<double> uniform_{0.0, 1.0};
};

} // namespace staragent
