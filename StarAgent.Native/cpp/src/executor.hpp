// SPDX-License-Identifier: MIT
#pragma once

#include "logging.hpp"
#include "protocol.hpp"

#include <atomic>
#include <condition_variable>
#include <functional>
#include <map>
#include <mutex>
#include <thread>
#include <vector>

namespace staragent {

struct ExecutorOptions
{
    bool AllowExec = false;
    std::vector<std::string> AllowedCommands;
    int TimeoutSeconds = 30;

    std::function<TimePoint()> ServerNow;
    std::function<bool(const std::string&)> SetChannel;
    std::function<bool(TimePoint)> SyncTime;
    std::function<bool()> Restart;
    Logger* Log = nullptr;
};

class Executor
{
public:
    explicit Executor(ExecutorOptions options);
    ~Executor();

    Executor(const Executor&) = delete;
    Executor& operator=(const Executor&) = delete;

    // May block while the command executes (or while waiting for a slot).
    // Replies are delivered through the callback, possibly more than once for
    // delayed commands (Processing followed by the final status).
    void Handle(const CommandModel& command, std::function<void(const CommandReplyModel&)> reply);

    // Cancels pending work and joins executor-owned threads. Running shell
    // commands are terminated through the shared cancel flag.
    void Stop();

private:
    CommandReplyModel Execute(const CommandModel& command);
    CommandReplyModel Dispatch(const CommandModel& command);
    CommandReplyModel RunShellCommand(const CommandModel& command);
    CommandReplyModel SyncTimeCommand(const CommandModel& command);
    CommandReplyModel SetChannelCommand(const CommandModel& command);
    CommandReplyModel RestartCommand(const CommandModel& command);

    bool IsDuplicate(std::int64_t id);
    bool IsAllowed(const std::string& name) const;
    bool AcquireSlot();
    void ReleaseSlot();
    void RunDelayed(const CommandModel& command, const std::function<void(const CommandReplyModel&)>& reply);
    void TrackThread(std::thread thread);
    Logger& Log();

    ExecutorOptions options_;
    std::atomic<bool> stopping_{false};

    std::mutex seen_mutex_;
    std::map<std::int64_t, std::int64_t> seen_;

    std::mutex slot_mutex_;
    std::condition_variable slot_cv_;
    int available_slots_ = 4;

    std::mutex threads_mutex_;
    std::vector<std::thread> threads_;
};

} // namespace staragent
