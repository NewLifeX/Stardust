// SPDX-License-Identifier: MIT
#pragma once

#include "backoff.hpp"
#include "config.hpp"
#include "events.hpp"
#include "executor.hpp"
#include "http_client.hpp"
#include "logging.hpp"
#include "metrics.hpp"
#include "platform.hpp"
#include "protocol.hpp"
#include "ws_client.hpp"

#include <atomic>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace staragent {

struct AgentOptions
{
    std::string ConfigPath;
    Config Configuration;
    Logger* Log = nullptr;
    std::string Version = "1.0.2026.1017";
    TimePoint BuildTime{};
    std::vector<std::string> Arguments;
};

class Agent
{
public:
    explicit Agent(AgentOptions options);
    ~Agent();

    Agent(const Agent&) = delete;
    Agent& operator=(const Agent&) = delete;

    int Run();
    int Once();
    void RequestStop();

private:
    enum class SessionResult
    {
        Ended,
        Migrated,
    };

    SessionResult RunSession(Backoff& backoff);
    void NotifyLoop(std::atomic<bool>& session_stopping);
    void Shutdown();
    void Login();
    PingResponse Ping();
    void Migrate(const std::string& new_server);
    void DispatchCommandAsync(const CommandModel& command);
    void SendReply(const CommandReplyModel& reply);
    void FlushEvents();
    void CheckUpgrade();
    void Enqueue(const std::string& type, const std::string& name, const std::string& remark);
    NodeInfo BuildNodeInfo();
    PingInfo BuildPingInfo();
    platform::NetworkInfo NetworkSnapshot(bool force);
    std::string LocalIP();
    TimePoint ServerNow() const;
    std::string Version() const { return version_; }
    std::vector<std::string> Plugins() const;
    Logger& Log() { return logger_; }
    bool Wait(int milliseconds);

    AgentOptions options_;
    std::string config_path_;
    ConfigStore store_;
    Logger& logger_;
    std::string version_;
    TimePoint build_time_;

    HttpClient client_;
    MetricsCollector metrics_;
    EventQueue events_;
    std::unique_ptr<Executor> executor_;
    WsClient ws_client_;

    platform::HardwareInfo hardware_;
    platform::SystemInfo system_;

    mutable std::mutex network_mutex_;
    platform::NetworkInfo network_;
    std::int64_t network_at_ms_ = 0;

    std::mutex login_mutex_;
    std::atomic<std::int64_t> server_offset_ms_{0};
    std::atomic<std::int64_t> last_delay_ms_{0};
    std::atomic<int> period_seconds_{60};
    std::atomic<bool> stopping_{false};
    std::atomic<bool> shutdown_done_{false};

    std::mutex wait_mutex_;
    std::condition_variable wait_cv_;

    std::mutex command_threads_mutex_;
    std::vector<std::thread> command_threads_;

    std::mutex upgrade_mutex_;
    std::int64_t last_upgrade_ms_ = 0;

    std::vector<std::string> restart_arguments_;
};

} // namespace staragent
