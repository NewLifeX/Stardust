// SPDX-License-Identifier: MIT
#include "agent.hpp"

#include "util.hpp"

#include <algorithm>
#include <chrono>
#include <exception>
#include <filesystem>
#include <stdexcept>

namespace staragent {
namespace {

constexpr int kMaxEventBatch = 100;
constexpr std::int64_t kUpgradePeriodMs = 6LL * 60 * 60 * 1000;

struct LoginRejected : std::runtime_error
{
    explicit LoginRejected(const std::string& message) : std::runtime_error(message) {}
};

bool IsLoginRejection(const HttpError& error)
{
    return error.StatusCode >= 400 && error.StatusCode < 500 && error.StatusCode != 408 &&
           error.StatusCode != 429;
}

std::string OsTag()
{
#if defined(_WIN32)
    return "windows";
#elif defined(__APPLE__)
    return "macos";
#else
    return "linux";
#endif
}

} // namespace

Agent::Agent(AgentOptions options)
    : options_(std::move(options)),
      config_path_(options_.ConfigPath),
      store_(config_path_, options_.Configuration),
      logger_(options_.Log != nullptr ? *options_.Log : ConsoleLogger()),
      version_(options_.Version.empty() ? "1.0.2026.1017" : options_.Version),
      build_time_(options_.BuildTime.time_since_epoch().count() == 0 ? Clock::now() : options_.BuildTime),
      client_(options_.Configuration.Server, options_.Configuration.TlsVerify, logger_),
      metrics_(options_.Configuration.DataDir),
      events_((std::filesystem::path(options_.Configuration.DataDir) / "events.json").string(), 1000),
      ws_client_(logger_)
{
    restart_arguments_ = options_.Arguments;
    period_seconds_ = std::max(1, options_.Configuration.MetricsInterval);

    hardware_ = platform::Hardware();
    system_ = platform::System();
    NetworkSnapshot(true);
    // Warm up CPU and network sampling so the first heartbeat carries rates.
    metrics_.Collect();

    ExecutorOptions executor_options;
    executor_options.AllowExec = options_.Configuration.AllowExec;
    executor_options.AllowedCommands = options_.Configuration.AllowedCommands;
    executor_options.TimeoutSeconds = options_.Configuration.CommandTimeout;
    executor_options.ServerNow = [this]() { return ServerNow(); };
    executor_options.SetChannel = [this](const std::string& channel) {
        store_.Update([&](Config& config) { config.Channel = channel; });
        return true;
    };
    executor_options.SyncTime = [](TimePoint value) { return platform::SetSystemTime(value); };
    executor_options.Restart = [this]() {
        const bool started = platform::RestartSelf(restart_arguments_);
        if (started) RequestStop();
        return started;
    };
    executor_options.Log = &logger_;
    executor_ = std::make_unique<Executor>(std::move(executor_options));

    client_.SetOnUnauthorized([this]() { Login(); });
}

Agent::~Agent()
{
    RequestStop();
    if (executor_) executor_->Stop();
    {
        std::lock_guard<std::mutex> lock(command_threads_mutex_);
        for (auto& thread : command_threads_)
        {
            if (thread.joinable()) thread.join();
        }
        command_threads_.clear();
    }
}

void Agent::RequestStop()
{
    if (stopping_.exchange(true)) return;
    wait_cv_.notify_all();
    ws_client_.Stop();
}

int Agent::Run()
{
    Log().Info("staragent starting version=" + version_ + " server=" + client_.BaseUrl() +
               " allow_exec=" + (store_.Snapshot().AllowExec ? "true" : "false"));
    Enqueue("info", "Startup", "agent started version=" + version_);

    Backoff backoff;
    bool failed = false;
    while (!stopping_.load())
    {
        try
        {
            if (client_.Token().empty() || client_.TokenExpired()) Login();
            RunSession(backoff);
        }
        catch (const LoginRejected& ex)
        {
            Log().Error(std::string("login rejected: ") + ex.what());
            failed = true;
            break;
        }
        catch (const HttpError& ex)
        {
            if (IsLoginRejection(ex))
            {
                Log().Error(std::string("login rejected: ") + ex.what());
                failed = true;
                break;
            }
            if (stopping_.load()) break;
            Log().Warn(std::string("connection lost, reconnecting with backoff: ") + ex.what());
            client_.ClearToken();
            if (!backoff.Wait(stopping_)) break;
        }
        catch (const std::exception& ex)
        {
            if (stopping_.load()) break;
            Log().Warn(std::string("connection lost, reconnecting with backoff: ") + ex.what());
            client_.ClearToken();
            if (!backoff.Wait(stopping_)) break;
        }
    }

    Shutdown();
    return failed ? 1 : 0;
}

int Agent::Once()
{
    Enqueue("info", "Startup", "agent started once version=" + version_);
    int result = 0;
    try
    {
        Login();
        const auto response = Ping();
        Log().Info("once ping completed period=" + std::to_string(response.Period) +
                   " commands=" + std::to_string(response.Commands.size()));

        for (const auto& command : response.Commands)
        {
            if (stopping_.load()) break;
            executor_->Handle(command, [this](const CommandReplyModel& reply) { SendReply(reply); });
        }
    }
    catch (const std::exception& ex)
    {
        Log().Error(std::string("once failed: ") + ex.what());
        result = 1;
    }

    Shutdown();
    return result;
}

Agent::SessionResult Agent::RunSession(Backoff& backoff)
{
    if (client_.Token().empty()) Login();

    int period = std::max(1, store_.Snapshot().MetricsInterval);
    period_seconds_ = period;

    std::atomic<bool> session_stopping{false};
    std::thread notify_thread([this, &session_stopping]() { NotifyLoop(session_stopping); });

    auto stop_notify = [&]() {
        session_stopping = true;
        ws_client_.Stop();
        if (notify_thread.joinable()) notify_thread.join();
    };

    struct NotifyGuard
    {
        std::function<void()> stop;
        ~NotifyGuard() { stop(); }
    } guard{stop_notify};

    CheckUpgrade();

    bool had_ping = false;
    while (!stopping_.load())
    {
        if (client_.TokenExpired()) Login();

        PingResponse response;
        try
        {
            response = Ping();
        }
        catch (...)
        {
            throw;
        }

        if (!had_ping)
        {
            backoff.Reset();
            had_ping = true;
        }

        if (response.Period > 0)
        {
            const int next = std::max(1, std::min(3600, response.Period));
            if (next != period) Log().Info("heartbeat period changed to " + std::to_string(next) + "s");
            period = next;
            period_seconds_ = next;
        }

        for (const auto& command : response.Commands) DispatchCommandAsync(command);

        FlushEvents();
        CheckUpgrade();

        if (!response.NewServer.empty())
        {
            auto target = Trim(response.NewServer);
            while (!target.empty() && target.back() == '/') target.pop_back();
            auto current = client_.BaseUrl();
            while (!current.empty() && current.back() == '/') current.pop_back();
            if (!target.empty() && target != current)
            {
                stop_notify();
                Migrate(target);
                return SessionResult::Migrated;
            }
        }

        if (!Wait(period * 1000)) break;
    }

    stop_notify();
    return SessionResult::Ended;
}

void Agent::NotifyLoop(std::atomic<bool>& session_stopping)
{
    Backoff backoff;
    while (!stopping_.load() && !session_stopping.load())
    {
        if (client_.Token().empty())
        {
            try
            {
                Login();
            }
            catch (const std::exception& ex)
            {
                Log().Warn(std::string("notify login failed: ") + ex.what());
                const int delay = backoff.NextDelayMs();
                const auto deadline = SteadyMsNow() + delay;
                while (!stopping_.load() && !session_stopping.load() && SteadyMsNow() < deadline)
                {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                }
                continue;
            }
        }

        WsClient::Options ws_options;
        ws_options.Url = BuildNotifyUrl(client_.BaseUrl());
        ws_options.Token = client_.Token();
        ws_options.PingIntervalSeconds = period_seconds_.load();
        ws_options.OnCommand = [this](const CommandModel& command) { DispatchCommandAsync(command); };
        const bool connected = ws_client_.Run(ws_options, stopping_);

        if (stopping_.load() || session_stopping.load()) return;
        Log().Warn(connected ? "notify connection closed, retrying" : "notify connection failed, retrying");

        const int delay = backoff.NextDelayMs();
        const auto deadline = SteadyMsNow() + delay;
        while (!stopping_.load() && !session_stopping.load() && SteadyMsNow() < deadline)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        }
    }
}

void Agent::Shutdown()
{
    if (shutdown_done_.exchange(true)) return;

    if (executor_) executor_->Stop();
    {
        std::lock_guard<std::mutex> lock(command_threads_mutex_);
        for (auto& thread : command_threads_)
        {
            if (thread.joinable()) thread.join();
        }
        command_threads_.clear();
    }

    Enqueue("info", "Stop", "agent stopping");
    FlushEvents();
    if (!client_.Token().empty())
    {
        try
        {
            client_.Logout("agent stopping");
        }
        catch (const std::exception& ex)
        {
            Log().Warn(std::string("logout failed: ") + ex.what());
        }
    }
    Log().Info("staragent stopped");
}

void Agent::Login()
{
    std::lock_guard<std::mutex> lock(login_mutex_);

    const auto config = store_.Snapshot();
    LoginInfo request;
    request.Code = config.Code;
    request.Secret = config.Secret;
    request.ProductCode = config.ProductCode;
    request.ClientId = LocalIP() + "@" + std::to_string(platform::ProcessId());
    request.Project = config.Project;
    request.Node = BuildNodeInfo();

    LoginResponse response;
    try
    {
        response = client_.Login(request);
    }
    catch (const HttpError& ex)
    {
        if (IsLoginRejection(ex)) throw LoginRejected(ex.what());
        throw;
    }

    if (response.ServerTime > 0) server_offset_ms_ = response.ServerTime - UnixMsNow();

    const bool code_changed = !response.Code.empty() && response.Code != config.Code;
    const bool secret_changed = !response.Secret.empty() && response.Secret != config.Secret;
    if (code_changed || secret_changed)
    {
        try
        {
            store_.Update([&](Config& current) {
                if (!response.Code.empty()) current.Code = response.Code;
                if (!response.Secret.empty()) current.Secret = response.Secret;
            });
        }
        catch (const std::exception& ex)
        {
            Log().Error(std::string("persist node identity failed: ") + ex.what());
        }
    }

    Log().Info("login succeeded name=" + response.Name + " expire=" + std::to_string(response.Expire) +
               " server_time=" + std::to_string(response.ServerTime));
}

PingResponse Agent::Ping()
{
    const auto started = SteadyMsNow();
    auto response = client_.Ping(BuildPingInfo());
    last_delay_ms_ = std::max<std::int64_t>(0, SteadyMsNow() - started);
    if (response.ServerTime > 0) server_offset_ms_ = response.ServerTime - UnixMsNow();
    return response;
}

void Agent::Migrate(const std::string& new_server)
{
    const auto old_server = client_.BaseUrl();
    const auto config = store_.Snapshot();
    Log().Info("migrating server from " + old_server + " to " + new_server);

    try
    {
        client_.Logout("server migration");
    }
    catch (...)
    {
    }

    client_.SetBaseUrl(new_server);
    try
    {
        store_.Update([&](Config& current) { current.Server = new_server; });
    }
    catch (...)
    {
        client_.SetBaseUrl(old_server);
        throw;
    }
    client_.ClearToken();

    try
    {
        Login();
    }
    catch (...)
    {
        client_.SetBaseUrl(old_server);
        client_.ClearToken();
        try
        {
            store_.Update([&](Config& current) { current.Server = config.Server; });
        }
        catch (...)
        {
        }
        throw;
    }
    Log().Info("server migration completed");
}

void Agent::DispatchCommandAsync(const CommandModel& command)
{
    if (stopping_.load()) return;
    try
    {
        std::lock_guard<std::mutex> lock(command_threads_mutex_);
        command_threads_.emplace_back([this, command]() {
            try
            {
                executor_->Handle(command,
                                  [this](const CommandReplyModel& reply) { SendReply(reply); });
            }
            catch (const std::exception& ex)
            {
                Log().Error(std::string("command handling failed: ") + ex.what());
            }
        });
    }
    catch (const std::exception& ex)
    {
        Log().Error(std::string("spawn command thread failed: ") + ex.what());
    }
}

void Agent::SendReply(const CommandReplyModel& reply)
{
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            client_.CommandReply(reply);
            Log().Debug("command reply sent id=" + std::to_string(reply.Id) +
                        " status=" + std::to_string(reply.Status));
            return;
        }
        catch (const std::exception& ex)
        {
            Log().Warn("command reply failed id=" + std::to_string(reply.Id) + " attempt=" +
                       std::to_string(attempt) + " error=" + ex.what());
        }
        if (!platform::SleepCancelable(attempt * 1000, stopping_)) return;
    }
    Log().Error("command reply dropped after retries id=" + std::to_string(reply.Id));
}

void Agent::FlushEvents()
{
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        auto batch = events_.Take(kMaxEventBatch);
        if (batch.empty()) return;

        try
        {
            const int count = client_.PostEvents(batch);
            Log().Debug("events posted count=" + std::to_string(count));
            if (count >= 0 && static_cast<std::size_t>(count) < batch.size())
            {
                events_.PutFront(std::vector<EventModel>(batch.begin() + count, batch.end()));
            }
            if (batch.size() < static_cast<std::size_t>(kMaxEventBatch)) return;
        }
        catch (const std::exception& ex)
        {
            events_.PutFront(batch);
            Log().Warn("post events failed count=" + std::to_string(batch.size()) + " attempt=" +
                       std::to_string(attempt) + " error=" + ex.what());
            if (!platform::SleepCancelable(attempt * 1000, stopping_)) return;
        }
    }
}

void Agent::CheckUpgrade()
{
    {
        std::lock_guard<std::mutex> lock(upgrade_mutex_);
        const auto now = SteadyMsNow();
        if (last_upgrade_ms_ != 0 && now - last_upgrade_ms_ < kUpgradePeriodMs) return;
        last_upgrade_ms_ = now;
    }

    try
    {
        const auto channel = store_.Snapshot().Channel;
        const auto info = client_.Upgrade(channel);
        if (!info || info->Version == version_) return;

        std::string remark = "available version " + info->Version;
        if (!info->Source.empty()) remark += " source=" + info->Source;
        Log().Info("upgrade available, manual update required version=" + info->Version);
        Enqueue("info", "Upgrade", remark);
    }
    catch (const std::exception& ex)
    {
        if (!stopping_.load()) Log().Debug(std::string("upgrade check failed: ") + ex.what());
    }
}

void Agent::Enqueue(const std::string& type, const std::string& name, const std::string& remark)
{
    EventModel event;
    event.Time = UnixMsNow();
    event.Type = type;
    event.Name = name;
    event.Remark = remark;
    events_.Add(event);
}

NodeInfo Agent::BuildNodeInfo()
{
    const auto network = NetworkSnapshot(true);
    const auto metrics = metrics_.Collect();

    NodeInfo node;
    node.Version = version_;
    node.Compile = ProtocolTime::FromTimePoint(build_time_);
    node.OSName = system_.OSName;
    node.OSVersion = system_.OSVersion;
    node.Architecture = platform::Architecture();
    node.MachineName = system_.HostName;
    node.UserName = system_.UserName;
    node.IP = network.IP;
    node.Gateway = network.Gateway;
    node.Dns = network.Dns;
    const unsigned processors = std::thread::hardware_concurrency();
    node.ProcessorCount = processors == 0 ? 1 : static_cast<int>(processors);
    node.Memory = metrics.Memory;
    node.AvailableMemory = metrics.AvailableMemory;
    node.FreeMemory = metrics.FreeMemory;
    node.TotalSize = metrics.TotalSize;
    node.AvailableFreeSpace = metrics.AvailableFreeSpace;
    node.DriveSize = metrics.DriveSize;
    node.DriveInfo = metrics.DriveInfo;
    node.UUID = hardware_.UUID;
    node.MachineGuid = hardware_.MachineGuid;
    node.SerialNumber = hardware_.SerialNumber;
    node.Board = hardware_.Board;
    node.DiskID = hardware_.DiskID;
    node.Macs = hardware_.Macs;
    node.InstallPath = platform::ExecutableDir();
    node.Runtime = "cpp17";
    node.Framework = "cpp17";
    node.TimeZone = system_.TimeZone;
    node.Time = ProtocolTime::FromTimePoint(Clock::now());
    node.Plugins = Plugins();
    return node;
}

PingInfo Agent::BuildPingInfo()
{
    const auto network = NetworkSnapshot(false);
    const auto metrics = metrics_.Collect();
    const auto delay = std::max<std::int64_t>(0, last_delay_ms_.load());

    PingInfo ping;
    ping.Time = UnixMsNow();
    ping.IP = network.IP;
    ping.Gateway = network.Gateway;
    ping.Dns = network.Dns;
    ping.Macs = hardware_.Macs;
    ping.DriveInfo = metrics.DriveInfo;
    ping.Framework = "cpp17";
    ping.Processes = metrics.Processes;
    ping.ProcessCount = metrics.ProcessCount;
    ping.Memory = metrics.Memory;
    ping.AvailableMemory = metrics.AvailableMemory;
    ping.FreeMemory = metrics.FreeMemory;
    ping.TotalSize = metrics.TotalSize;
    ping.AvailableFreeSpace = metrics.AvailableFreeSpace;
    ping.CpuRate = metrics.CpuRate;
    ping.UplinkSpeed = metrics.UplinkSpeed;
    ping.DownlinkSpeed = metrics.DownlinkSpeed;
    ping.Uptime = metrics.Uptime;
    ping.Delay = delay;
    ping.SystemLoad = metrics.SystemLoad;
    ping.ServerLatency = static_cast<std::uint64_t>(delay);
    return ping;
}

platform::NetworkInfo Agent::NetworkSnapshot(bool force)
{
    std::lock_guard<std::mutex> lock(network_mutex_);
    const auto now = SteadyMsNow();
    if (!force && network_at_ms_ != 0 && now - network_at_ms_ < 5LL * 60 * 1000) return network_;

    try
    {
        network_ = platform::Network();
        network_at_ms_ = now;
    }
    catch (...)
    {
    }
    return network_;
}

std::string Agent::LocalIP()
{
    std::lock_guard<std::mutex> lock(network_mutex_);
    return network_.IP;
}

TimePoint Agent::ServerNow() const
{
    return Clock::now() + std::chrono::milliseconds(server_offset_ms_.load());
}

std::vector<std::string> Agent::Plugins() const
{
    return {"agent:cpp",
            std::string("cap:exec:") + (store_.Snapshot().AllowExec ? "true" : "false"),
            "cap:upgrade=false",
            "cap:deploy=false",
            "os:" + OsTag()};
}

bool Agent::Wait(int milliseconds)
{
    std::unique_lock<std::mutex> lock(wait_mutex_);
    const bool stopped = wait_cv_.wait_for(lock, std::chrono::milliseconds(milliseconds),
                                           [this]() { return stopping_.load(); });
    return !stopped;
}

} // namespace staragent
