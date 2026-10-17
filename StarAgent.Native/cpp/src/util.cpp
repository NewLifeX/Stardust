// SPDX-License-Identifier: MIT
#include "util.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <sstream>

namespace staragent {
namespace {

// Howard Hinnant's days_from_civil, returning days since 1970-01-01.
std::int64_t DaysFromCivil(int year, unsigned month, unsigned day)
{
    year -= month <= 2;
    const int era = (year >= 0 ? year : year - 399) / 400;
    const unsigned year_of_era = static_cast<unsigned>(year - era * 400);
    const unsigned day_of_year = (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1;
    const unsigned day_of_era = year_of_era * 365 + year_of_era / 4 - year_of_era / 100 + day_of_year;
    return era * 146097LL + static_cast<std::int64_t>(day_of_era) - 719468;
}

bool IsLeapYear(int year)
{
    return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
}

int DaysInMonth(int year, int month)
{
    static const int days[] = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};
    if (month == 2 && IsLeapYear(year)) return 29;
    if (month < 1 || month > 12) return 0;
    return days[month - 1];
}

TimePoint MakeUtc(int year, int month, int day, int hour, int minute, int second, int millisecond)
{
    const auto days = DaysFromCivil(year, static_cast<unsigned>(month), static_cast<unsigned>(day));
    const auto seconds = days * 86400LL + hour * 3600LL + minute * 60LL + second;
    return TimePoint(std::chrono::milliseconds(seconds * 1000 + millisecond));
}

bool ParseDatePart(const std::string& text, int& year, int& month, int& day)
{
    if (text.size() < 10) return false;
    if (std::sscanf(text.c_str(), "%4d-%2d-%2d", &year, &month, &day) != 3) return false;
    if (month < 1 || month > 12) return false;
    if (day < 1 || day > DaysInMonth(year, month)) return false;
    return true;
}

} // namespace

ProtocolTime ProtocolTime::FromTimePoint(TimePoint value)
{
    ProtocolTime result;
    result.value_ = value;
    result.set_ = true;
    return result;
}

ProtocolTime ProtocolTime::FromUnixMs(std::int64_t milliseconds)
{
    if (milliseconds <= 0) return {};
    return FromTimePoint(TimePoint(std::chrono::milliseconds(milliseconds)));
}

ProtocolTime ProtocolTime::Parse(const std::string& text)
{
    const auto value = Trim(text);
    if (value.empty()) return {};
    if (value.rfind("0001-01-01", 0) == 0) return {};

    int year = 0, month = 0, day = 0;
    if (!ParseDatePart(value, year, month, day)) return {};

    int hour = 0, minute = 0, second = 0, millisecond = 0;
    std::size_t position = 10;
    if (position < value.size() && (value[position] == ' ' || value[position] == 'T'))
    {
        position++;
        if (std::sscanf(value.c_str() + position, "%2d:%2d:%2d", &hour, &minute, &second) != 3) return {};
        position += 8;
        if (position < value.size() && value[position] == '.')
        {
            position++;
            int digits = 0;
            while (position < value.size() && std::isdigit(static_cast<unsigned char>(value[position])))
            {
                if (digits < 3) millisecond = millisecond * 10 + (value[position] - '0');
                digits++;
                position++;
            }
            while (digits < 3)
            {
                millisecond *= 10;
                digits++;
            }
        }
    }
    else if (position != value.size())
    {
        return {};
    }

    if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 60) return {};

    auto result = MakeUtc(year, month, day, hour, minute, second, millisecond);

    // Optional trailing time zone. The protocol uses naive UTC, but accept
    // RFC3339 values so servers that emit them still work.
    if (position < value.size())
    {
        if (value[position] == 'Z' || value[position] == 'z')
        {
            // already UTC
        }
        else if (value[position] == '+' || value[position] == '-')
        {
            const int sign = value[position] == '-' ? -1 : 1;
            int offset_hour = 0, offset_minute = 0;
            if (std::sscanf(value.c_str() + position + 1, "%2d:%2d", &offset_hour, &offset_minute) == 2)
            {
                result -= std::chrono::minutes(sign * (offset_hour * 60 + offset_minute));
            }
        }
    }

    return FromTimePoint(result);
}

std::int64_t ProtocolTime::UnixMs() const
{
    if (!set_) return 0;
    return std::chrono::duration_cast<std::chrono::milliseconds>(value_.time_since_epoch()).count();
}

std::string ProtocolTime::Text() const
{
    if (!set_) return "0001-01-01 00:00:00";
    return FormatUtc(value_);
}

std::string Trim(const std::string& value)
{
    std::size_t begin = 0;
    while (begin < value.size() && std::isspace(static_cast<unsigned char>(value[begin]))) begin++;
    std::size_t end = value.size();
    while (end > begin && std::isspace(static_cast<unsigned char>(value[end - 1]))) end--;
    return value.substr(begin, end - begin);
}

std::string ToLower(const std::string& value)
{
    std::string result = value;
    std::transform(result.begin(), result.end(), result.begin(),
                   [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    return result;
}

bool StartsWith(const std::string& value, const std::string& prefix)
{
    return value.size() >= prefix.size() && value.compare(0, prefix.size(), prefix) == 0;
}

bool EndsWith(const std::string& value, const std::string& suffix)
{
    return value.size() >= suffix.size() &&
           value.compare(value.size() - suffix.size(), suffix.size(), suffix) == 0;
}

bool EqualsIgnoreCase(const std::string& left, const std::string& right)
{
    return ToLower(left) == ToLower(right);
}

std::vector<std::string> Split(const std::string& value, char separator)
{
    std::vector<std::string> result;
    std::string current;
    for (const char c : value)
    {
        if (c == separator)
        {
            result.push_back(current);
            current.clear();
        }
        else
        {
            current.push_back(c);
        }
    }
    result.push_back(current);
    return result;
}

std::vector<std::string> SplitAny(const std::string& value, const std::string& separators)
{
    std::vector<std::string> result;
    std::string current;
    for (const char c : value)
    {
        if (separators.find(c) != std::string::npos)
        {
            result.push_back(current);
            current.clear();
        }
        else
        {
            current.push_back(c);
        }
    }
    result.push_back(current);
    return result;
}

std::string Join(const std::vector<std::string>& values, const std::string& separator)
{
    std::string result;
    for (std::size_t i = 0; i < values.size(); i++)
    {
        if (i > 0) result += separator;
        result += values[i];
    }
    return result;
}

std::string UrlEncode(const std::string& value)
{
    static const char* hex = "0123456789ABCDEF";
    std::string result;
    result.reserve(value.size() * 3);
    for (const unsigned char c : value)
    {
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' ||
            c == '_' || c == '.' || c == '~')
        {
            result.push_back(static_cast<char>(c));
        }
        else
        {
            result.push_back('%');
            result.push_back(hex[c >> 4]);
            result.push_back(hex[c & 0x0F]);
        }
    }
    return result;
}

std::string FormatBytes(std::uint64_t value)
{
    constexpr std::uint64_t k = 1024;
    constexpr std::uint64_t m = k * 1024;
    constexpr std::uint64_t g = m * 1024;
    constexpr std::uint64_t t = g * 1024;

    char buffer[64];
    if (value >= t)
    {
        if (value % t == 0) std::snprintf(buffer, sizeof(buffer), "%lluT", static_cast<unsigned long long>(value / t));
        else std::snprintf(buffer, sizeof(buffer), "%.1fT", static_cast<double>(value) / static_cast<double>(t));
    }
    else if (value >= g)
    {
        if (value % g == 0) std::snprintf(buffer, sizeof(buffer), "%lluG", static_cast<unsigned long long>(value / g));
        else std::snprintf(buffer, sizeof(buffer), "%.1fG", static_cast<double>(value) / static_cast<double>(g));
    }
    else if (value >= m)
    {
        if (value % m == 0) std::snprintf(buffer, sizeof(buffer), "%lluM", static_cast<unsigned long long>(value / m));
        else std::snprintf(buffer, sizeof(buffer), "%.1fM", static_cast<double>(value) / static_cast<double>(m));
    }
    else if (value >= k)
    {
        if (value % k == 0) std::snprintf(buffer, sizeof(buffer), "%lluK", static_cast<unsigned long long>(value / k));
        else std::snprintf(buffer, sizeof(buffer), "%.1fK", static_cast<double>(value) / static_cast<double>(k));
    }
    else
    {
        std::snprintf(buffer, sizeof(buffer), "%lluB", static_cast<unsigned long long>(value));
    }
    return buffer;
}

std::string FormatUtc(TimePoint value)
{
    const std::time_t seconds = Clock::to_time_t(value);
    std::tm tm{};
#ifdef _WIN32
    gmtime_s(&tm, &seconds);
#else
    gmtime_r(&seconds, &tm);
#endif
    char buffer[80];
    std::snprintf(buffer, sizeof(buffer), "%04d-%02d-%02d %02d:%02d:%02d", tm.tm_year + 1900, tm.tm_mon + 1,
                  tm.tm_mday, tm.tm_hour, tm.tm_min, tm.tm_sec);
    return buffer;
}

std::string FormatLocal(TimePoint value, bool with_milliseconds)
{
    const std::time_t seconds = Clock::to_time_t(value);
    std::tm tm{};
#ifdef _WIN32
    localtime_s(&tm, &seconds);
#else
    localtime_r(&seconds, &tm);
#endif
    char buffer[96];
    if (with_milliseconds)
    {
        const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(value.time_since_epoch()).count() % 1000;
        std::snprintf(buffer, sizeof(buffer), "%04d-%02d-%02d %02d:%02d:%02d.%03d", tm.tm_year + 1900,
                      tm.tm_mon + 1, tm.tm_mday, tm.tm_hour, tm.tm_min, tm.tm_sec, static_cast<int>(ms));
    }
    else
    {
        std::snprintf(buffer, sizeof(buffer), "%04d-%02d-%02d %02d:%02d:%02d", tm.tm_year + 1900, tm.tm_mon + 1,
                      tm.tm_mday, tm.tm_hour, tm.tm_min, tm.tm_sec);
    }
    return buffer;
}

std::int64_t UnixMsNow()
{
    return std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now().time_since_epoch()).count();
}

std::int64_t UnixSecondsNow()
{
    return std::chrono::duration_cast<std::chrono::seconds>(Clock::now().time_since_epoch()).count();
}

std::int64_t SteadyMsNow()
{
    return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch())
        .count();
}

std::string ToHex(const unsigned char* data, std::size_t size)
{
    static const char* hex = "0123456789abcdef";
    std::string result;
    result.reserve(size * 2);
    for (std::size_t i = 0; i < size; i++)
    {
        result.push_back(hex[data[i] >> 4]);
        result.push_back(hex[data[i] & 0x0F]);
    }
    return result;
}

} // namespace staragent
