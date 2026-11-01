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
// 计算 MD5（16 字节小写十六进制），用于校验升级包
std::string Md5Hex(const std::string& data);
std::string Md5File(const std::string& path);

// 网络质量评分：延迟指数衰减（1ms 满分，10ms 99.1%，100ms 90.6%），再乘成功率。
// latency_ms 为平均延迟，success 为成功次数，count 为总探测次数。
double ComputePingScore(int latency_ms, int success, int count);

// 按指定小数位四舍五入（factor 由 digits 推导），用于避免 27.850000000000023 这类浮点尾数
double RoundTo(double value, int digits);
// 百分比（0~100）转比例（0~1），用于心跳中的 percent 字段。
// 服务端这些字段按比例存储、展示时再乘 100，与 .NET 客户端 MachineInfo 口径一致。
double PercentToRatio(double percent);

} // namespace staragent
