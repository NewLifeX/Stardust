// SPDX-License-Identifier: MIT
// StarAgent Native C++ - small shared utilities.
#pragma once

#include <chrono>
#include <cstdint>
#include <string>
#include <vector>

namespace staragent {

using Clock = std::chrono::system_clock;
using TimePoint = Clock::time_point;

// Protocol time wrapper. The wire format is UTC "yyyy-MM-dd HH:mm:ss"; the
// zero value is "0001-01-01 00:00:00" and means "not set".
class ProtocolTime
{
public:
    ProtocolTime() = default;

    static ProtocolTime FromTimePoint(TimePoint value);
    static ProtocolTime FromUnixMs(std::int64_t milliseconds);
    static ProtocolTime Parse(const std::string& text);

    bool IsSet() const { return set_; }
    std::int64_t UnixMs() const;
    TimePoint Value() const { return value_; }
    std::string Text() const;

private:
    TimePoint value_{};
    bool set_ = false;
};

std::string Trim(const std::string& value);
std::string ToLower(const std::string& value);
bool StartsWith(const std::string& value, const std::string& prefix);
bool EndsWith(const std::string& value, const std::string& suffix);
bool EqualsIgnoreCase(const std::string& left, const std::string& right);
std::vector<std::string> Split(const std::string& value, char separator);
std::vector<std::string> SplitAny(const std::string& value, const std::string& separators);
std::string Join(const std::vector<std::string>& values, const std::string& separator);
std::string UrlEncode(const std::string& value);
std::string FormatBytes(std::uint64_t value);
std::string FormatUtc(TimePoint value);
std::string FormatLocal(TimePoint value, bool with_milliseconds);
std::int64_t UnixMsNow();
std::int64_t UnixSecondsNow();
std::int64_t SteadyMsNow();
std::string ToHex(const unsigned char* data, std::size_t size);

} // namespace staragent
