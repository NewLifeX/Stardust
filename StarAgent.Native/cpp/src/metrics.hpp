// SPDX-License-Identifier: MIT
#pragma once

#include <cstdint>
#include <mutex>
#include <string>

namespace staragent {

struct MetricsSnapshot
{
    double CpuRate = 0;
    std::uint64_t Memory = 0;
    std::uint64_t AvailableMemory = 0;
    std::uint64_t FreeMemory = 0;
    std::uint64_t TotalSize = 0;
    std::uint64_t AvailableFreeSpace = 0;
    std::uint64_t DriveSize = 0;
    std::string DriveInfo;
    std::uint64_t UplinkSpeed = 0;
    std::uint64_t DownlinkSpeed = 0;
    std::uint64_t ProcessCount = 0;
    std::string Processes;
    std::uint64_t Uptime = 0;
    double SystemLoad = 0;
};

class MetricsCollector
{
public:
    explicit MetricsCollector(std::string data_dir);

    MetricsSnapshot Collect();

private:
    std::string data_dir_;
    std::mutex mutex_;
    std::uint64_t previous_sent_ = 0;
    std::uint64_t previous_received_ = 0;
    std::int64_t previous_time_ms_ = 0;
};

} // namespace staragent
