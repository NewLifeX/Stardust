// SPDX-License-Identifier: MIT
#include "logging.hpp"

#include "util.hpp"

#include <chrono>
#include <cstdio>
#include <filesystem>
#include <iostream>
#include <sstream>

namespace staragent {
namespace {

namespace fs = std::filesystem;

const char* LevelName(LogLevel level)
{
    switch (level)
    {
    case LogLevel::Debug:
        return "DEBUG";
    case LogLevel::Info:
        return "INFO";
    case LogLevel::Warn:
        return "WARN";
    case LogLevel::Error:
        return "ERROR";
    }
    return "INFO";
}

LogLevel ParseLevel(const std::string& text)
{
    const auto value = ToLower(Trim(text));
    if (value == "debug") return LogLevel::Debug;
    if (value == "warn" || value == "warning") return LogLevel::Warn;
    if (value == "error") return LogLevel::Error;
    return LogLevel::Info;
}

} // namespace

Logger::Logger(std::string dir, std::string level, bool console, int keep_days)
    : dir_(std::move(dir)), level_(ParseLevel(level)), console_(console), keep_days_(keep_days)
{
    std::error_code error;
    fs::create_directories(dir_, error);
    if (!error)
    {
        fs::permissions(dir_, fs::perms::owner_all, fs::perm_options::replace, error);
    }
}

Logger::~Logger() { Close(); }

void Logger::Close()
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (stream_.is_open()) stream_.close();
}

void Logger::Debug(const std::string& message) { Log(LogLevel::Debug, message); }
void Logger::Info(const std::string& message) { Log(LogLevel::Info, message); }
void Logger::Warn(const std::string& message) { Log(LogLevel::Warn, message); }
void Logger::Error(const std::string& message) { Log(LogLevel::Error, message); }

void Logger::Log(LogLevel level, const std::string& message)
{
    if (level < level_) return;

    const auto now = Clock::now();
    const auto day = FormatLocal(now, false).substr(0, 10);
    const auto line = FormatLocal(now, true) + " [" + LevelName(level) + "] " + message;

    std::lock_guard<std::mutex> lock(mutex_);
    if (!dir_.empty())
    {
        try
        {
            RotateLocked(day);
            if (stream_.is_open())
            {
                stream_ << line << '\n';
                stream_.flush();
            }
        }
        catch (...)
        {
            // Logging must never take the agent down.
        }
    }
    if (console_ || dir_.empty())
    {
        std::cerr << line << '\n';
        std::cerr.flush();
    }
}

void Logger::RotateLocked(const std::string& day)
{
    if (stream_.is_open() && day_ == day) return;

    if (stream_.is_open()) stream_.close();
    std::error_code error;
    fs::create_directories(dir_, error);
    if (error) return;

    const auto path = (fs::path(dir_) / ("staragent-" + day + ".log")).string();
    stream_.open(path, std::ios::binary | std::ios::app);
    day_ = day;
    if (stream_.is_open())
    {
        CleanupLocked(day);
    }
}

void Logger::CleanupLocked(const std::string& day)
{
    if (keep_days_ <= 0) return;

    // Determine the cutoff by subtracting whole days from the current day.
    // File names are ISO dates, so they sort lexicographically.
    const auto current = ProtocolTime::Parse(day + " 00:00:00");
    if (!current.IsSet()) return;
    const auto cutoff = current.Value() - std::chrono::hours(24 * keep_days_);
    const auto cutoff_text = FormatUtc(cutoff).substr(0, 10);

    std::error_code error;
    for (const auto& entry : fs::directory_iterator(dir_, error))
    {
        if (error) return;
        if (!entry.is_regular_file()) continue;
        const auto name = entry.path().filename().string();
        if (!StartsWith(name, "staragent-") || !EndsWith(name, ".log")) continue;
        const auto date = name.substr(10, 10);
        if (date.size() == 10 && date < cutoff_text)
        {
            std::error_code remove_error;
            fs::remove(entry.path(), remove_error);
        }
    }
}

Logger& ConsoleLogger()
{
    static Logger logger("", "info", true);
    return logger;
}

} // namespace staragent
