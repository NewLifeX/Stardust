// SPDX-License-Identifier: MIT
#include "metrics.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <algorithm>
#include <cmath>
#include <set>
#include <vector>

namespace staragent {
namespace {

std::string DescribeDrives(std::uint64_t& total_size)
{
    total_size = 0;
    std::vector<std::string> items;
    for (const auto& drive : platform::Drives())
    {
        if (drive.Total == 0) continue;
        total_size += drive.Total;
        items.push_back(drive.Path + "[" + drive.FileSystem + "]=" + FormatBytes(drive.Free) + "/" +
                        FormatBytes(drive.Total));
    }
    std::sort(items.begin(), items.end());
    return Join(items, ",");
}

std::string JoinProcessNames(const std::vector<std::string>& names)
{
    static const std::set<std::string> ignored = {"system",
                                                  "system idle process",
                                                  "idle",
                                                  "registry",
                                                  "memory compression"};
    std::set<std::string> seen;
    std::vector<std::string> result;
    for (const auto& raw : names)
    {
        const auto name = Trim(raw);
        if (name.empty() || EndsWith(name, "$")) continue;
        const auto key = ToLower(name);
        if (ignored.count(key) > 0) continue;
        if (!seen.insert(key).second) continue;
        result.push_back(name);
        if (result.size() >= 200) break;
    }
    std::sort(result.begin(), result.end());
    return Join(result, ",");
}

} // namespace

MetricsCollector::MetricsCollector(std::string data_dir) : data_dir_(std::move(data_dir)) {}

MetricsSnapshot MetricsCollector::Collect()
{
    MetricsSnapshot snapshot;

    try
    {
        const auto value = platform::CpuPercent();
        if (!std::isnan(value)) snapshot.CpuRate = std::max(0.0, std::min(100.0, value));
    }
    catch (...)
    {
    }

    try
    {
        const auto value = platform::Memory();
        snapshot.Memory = value.Total;
        snapshot.AvailableMemory = value.Available;
        snapshot.FreeMemory = value.Free;
    }
    catch (...)
    {
    }

    try
    {
        const auto value = platform::DiskUsage(data_dir_);
        snapshot.TotalSize = value.Total;
        snapshot.AvailableFreeSpace = value.Free;
    }
    catch (...)
    {
    }

    try
    {
        snapshot.DriveInfo = DescribeDrives(snapshot.DriveSize);
    }
    catch (...)
    {
    }

    try
    {
        const auto totals = platform::NetTotalsSinceStart();
        if (totals.Valid)
        {
            const auto now = SteadyMsNow();
            std::lock_guard<std::mutex> lock(mutex_);
            if (previous_time_ms_ > 0 && now > previous_time_ms_ && totals.Sent >= previous_sent_ &&
                totals.Received >= previous_received_)
            {
                const double seconds = static_cast<double>(now - previous_time_ms_) / 1000.0;
                if (seconds > 0)
                {
                    snapshot.UplinkSpeed =
                        static_cast<std::uint64_t>(static_cast<double>(totals.Sent - previous_sent_) / seconds);
                    snapshot.DownlinkSpeed =
                        static_cast<std::uint64_t>(static_cast<double>(totals.Received - previous_received_) / seconds);
                }
            }
            previous_sent_ = totals.Sent;
            previous_received_ = totals.Received;
            previous_time_ms_ = now;
        }
    }
    catch (...)
    {
    }

    try
    {
        snapshot.ProcessCount = platform::ProcessCount();
    }
    catch (...)
    {
    }

    try
    {
        snapshot.Processes = JoinProcessNames(platform::ProcessNames());
    }
    catch (...)
    {
    }

    try
    {
        snapshot.Uptime = platform::UptimeSeconds();
    }
    catch (...)
    {
    }

    try
    {
        const auto value = platform::LoadAverage();
        if (!std::isnan(value)) snapshot.SystemLoad = value;
    }
    catch (...)
    {
    }

    return snapshot;
}

} // namespace staragent
