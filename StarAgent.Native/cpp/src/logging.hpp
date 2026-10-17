// SPDX-License-Identifier: MIT
#pragma once

#include <fstream>
#include <mutex>
#include <string>

namespace staragent {

enum class LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
};

class Logger
{
public:
    // dir is created when needed. Logs rotate daily and files older than
    // keep_days are removed.
    Logger(std::string dir, std::string level, bool console, int keep_days = 7);
    ~Logger();

    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;

    void Debug(const std::string& message);
    void Info(const std::string& message);
    void Warn(const std::string& message);
    void Error(const std::string& message);
    void Log(LogLevel level, const std::string& message);
    void Close();

    LogLevel Level() const { return level_; }

private:
    void RotateLocked(const std::string& day);
    void CleanupLocked(const std::string& day);

    std::mutex mutex_;
    std::string dir_;
    std::string day_;
    std::ofstream stream_;
    LogLevel level_ = LogLevel::Info;
    bool console_ = false;
    int keep_days_ = 7;
};

// Process-wide logger used before a file logger is configured. Writes to
// stderr only.
Logger& ConsoleLogger();

} // namespace staragent
