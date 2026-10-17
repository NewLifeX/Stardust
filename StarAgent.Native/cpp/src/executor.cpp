// SPDX-License-Identifier: MIT
#include "executor.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <algorithm>
#include <cmath>
#include <exception>

namespace staragent {
namespace {

constexpr std::int64_t kDedupTtlMs = 60LL * 60 * 1000;
constexpr std::size_t kMaxArgumentBytes = 8 * 1024;

CommandReplyModel Reply(CommandStatus status, std::string data)
{
    CommandReplyModel reply;
    reply.Status = static_cast<int>(status);
    reply.Data = std::move(data);
    return reply;
}

bool IsValidChannel(const std::string& channel)
{
    if (channel.empty() || channel.size() > 64) return false;
    for (const unsigned char c : channel)
    {
        if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' ||
              c == '_' || c == '-'))
        {
            return false;
        }
    }
    return true;
}

} // namespace

Executor::Executor(ExecutorOptions options) : options_(std::move(options))
{
    if (options_.TimeoutSeconds <= 0) options_.TimeoutSeconds = 30;
    if (options_.TimeoutSeconds > 300) options_.TimeoutSeconds = 300;
}

Executor::~Executor() { Stop(); }

Logger& Executor::Log()
{
    return options_.Log != nullptr ? *options_.Log : ConsoleLogger();
}

void Executor::Stop()
{
    if (stopping_.exchange(true)) return;
    {
        std::lock_guard<std::mutex> lock(slot_mutex_);
        slot_cv_.notify_all();
    }

    std::vector<std::thread> threads;
    {
        std::lock_guard<std::mutex> lock(threads_mutex_);
        threads.swap(threads_);
    }
    for (auto& thread : threads)
    {
        if (thread.joinable()) thread.join();
    }
}

void Executor::TrackThread(std::thread thread)
{
    {
        std::unique_lock<std::mutex> lock(threads_mutex_);
        if (!stopping_.load())
        {
            threads_.push_back(std::move(thread));
            return;
        }
    }
    // Stop() already swapped the thread list; join here so no joinable
    // std::thread is ever destroyed.
    if (thread.joinable()) thread.join();
}

bool Executor::AcquireSlot()
{
    std::unique_lock<std::mutex> lock(slot_mutex_);
    slot_cv_.wait(lock, [this]() { return available_slots_ > 0 || stopping_.load(); });
    if (stopping_.load()) return false;
    available_slots_--;
    return true;
}

void Executor::ReleaseSlot()
{
    {
        std::lock_guard<std::mutex> lock(slot_mutex_);
        available_slots_++;
    }
    slot_cv_.notify_one();
}

bool Executor::IsDuplicate(std::int64_t id)
{
    if (id == 0) return false;
    const auto now = SteadyMsNow();

    std::lock_guard<std::mutex> lock(seen_mutex_);
    for (auto it = seen_.begin(); it != seen_.end();)
    {
        if (now - it->second > kDedupTtlMs) it = seen_.erase(it);
        else ++it;
    }
    if (seen_.find(id) != seen_.end()) return true;
    seen_[id] = now;
    return false;
}

bool Executor::IsAllowed(const std::string& name) const
{
    if (options_.AllowedCommands.empty()) return true;
    const auto lower = ToLower(name);
    for (const auto& raw : options_.AllowedCommands)
    {
        auto item = ToLower(Trim(raw));
        if (item.empty()) continue;
        if (item == lower) return true;
        if (EndsWith(item, "*") && StartsWith(lower, item.substr(0, item.size() - 1))) return true;
    }
    return false;
}

void Executor::Handle(const CommandModel& command, std::function<void(const CommandReplyModel&)> reply)
{
    if (!reply) reply = [](const CommandReplyModel&) {};

    // Every reply path must carry the originating command Id; the server
    // validates command ownership by Id.
    auto deliver = [&reply, command](CommandReplyModel result) {
        result.Id = command.Id;
        reply(result);
    };

    if (IsDuplicate(command.Id))
    {
        Log().Debug("duplicate command ignored id=" + std::to_string(command.Id));
        return;
    }

    const auto now = Clock::now();
    if (command.Expire.IsSet() && command.Expire.Value() < now)
    {
        Log().Info("command expired id=" + std::to_string(command.Id));
        deliver(Reply(CommandStatus::Cancelled, "命令已过期"));
        return;
    }

    if (command.StartTime.IsSet() && command.StartTime.Value() > now)
    {
        Log().Info("command scheduled id=" + std::to_string(command.Id));
        deliver(Reply(CommandStatus::Processing, "已安排执行"));
        if (stopping_.load()) return;
        TrackThread(std::thread([this, command, deliver]() { RunDelayed(command, deliver); }));
        return;
    }

    deliver(Execute(command));
}

void Executor::RunDelayed(const CommandModel& command,
                          const std::function<void(const CommandReplyModel&)>& reply)
{
    while (!stopping_.load())
    {
        const auto now = Clock::now();
        if (command.StartTime.Value() <= now) break;
        const auto remaining =
            std::chrono::duration_cast<std::chrono::milliseconds>(command.StartTime.Value() - now).count();
        const auto wait = static_cast<int>(std::min<std::int64_t>(remaining, 60 * 60 * 1000));
        if (!platform::SleepCancelable(wait > 0 ? wait : 1, stopping_)) break;
    }

    if (stopping_.load())
    {
        reply(Reply(CommandStatus::Cancelled, "Agent 已停止，延迟命令取消"));
        return;
    }
    if (command.Expire.IsSet() && command.Expire.Value() < Clock::now())
    {
        reply(Reply(CommandStatus::Cancelled, "命令已过期"));
        return;
    }

    reply(Execute(command));
}

CommandReplyModel Executor::Execute(const CommandModel& command)
{
    const auto started = SteadyMsNow();
    auto result = Dispatch(command);
    result.Id = command.Id;
    result.Data = TruncateOutput(result.Data);
    Log().Info("command finished id=" + std::to_string(command.Id) + " command=" + command.Command +
               " status=" + std::to_string(result.Status) + " duration_ms=" +
               std::to_string(SteadyMsNow() - started) + " argument_bytes=" +
               std::to_string(command.Argument.size()));
    return result;
}

CommandReplyModel Executor::Dispatch(const CommandModel& command)
{
    if (!IsAllowed(command.Command))
    {
        return Reply(CommandStatus::Cancelled, "命令不在白名单: " + command.Command);
    }

    if (!AcquireSlot()) return Reply(CommandStatus::Cancelled, "Agent 已停止");
    struct SlotGuard
    {
        Executor* owner;
        ~SlotGuard() { owner->ReleaseSlot(); }
    } guard{this};

    const auto name = ToLower(command.Command);
    if (name == "bash" || name == "cmd") return RunShellCommand(command);
    if (name == "node/synctime") return SyncTimeCommand(command);
    if (name == "node/setchannel") return SetChannelCommand(command);
    if (name == "node/restart") return RestartCommand(command);
    if (name == "node/reboot" || name == "framework/install" || StartsWith(name, "deploy/"))
    {
        return Reply(CommandStatus::Error, "不支持命令: " + command.Command);
    }
    return Reply(CommandStatus::Error, "不支持命令: " + command.Command);
}

CommandReplyModel Executor::RunShellCommand(const CommandModel& command)
{
    const auto name = ToLower(command.Command);

    if (!options_.AllowExec) return Reply(CommandStatus::Cancelled, "命令执行未启用");

#ifdef _WIN32
    if (name != "cmd") return Reply(CommandStatus::Error, "不支持命令: " + command.Command);
#else
    if (name != "bash") return Reply(CommandStatus::Error, "不支持命令: " + command.Command);
#endif
    if (command.Argument.size() > kMaxArgumentBytes)
    {
        return Reply(CommandStatus::Error, "命令参数过长（" + std::to_string(command.Argument.size()) +
                                               " 字节，上限 " + std::to_string(kMaxArgumentBytes) + "）");
    }

    auto script = Trim(command.Argument);
    int timeout_ms = options_.TimeoutSeconds * 1000;

    if (!script.empty() && script.front() == '{')
    {
        auto value = Json::parse(script, nullptr, false);
        if (!value.is_discarded() && value.is_object())
        {
            if (value.contains("cmd") && value["cmd"].is_string())
            {
                const auto candidate = value["cmd"].get<std::string>();
                if (!Trim(candidate).empty()) script = candidate;
            }
            if (value.contains("timeout") && value["timeout"].is_number())
            {
                const auto requested = value["timeout"].get<std::int64_t>();
                if (requested > 0) timeout_ms = static_cast<int>(requested);
            }
        }
    }

    if (script.empty()) return Reply(CommandStatus::Error, "命令为空");
    if (script.size() > kMaxArgumentBytes)
    {
        return Reply(CommandStatus::Error, "命令过长（" + std::to_string(script.size()) + " 字节，上限 " +
                                               std::to_string(kMaxArgumentBytes) + "）");
    }
    if (timeout_ms <= 0) timeout_ms = 30000;
    if (timeout_ms > 300000) timeout_ms = 300000;

    const auto result = platform::RunShell(script, timeout_ms, stopping_);
    const auto formatted_timeout = std::to_string(timeout_ms / 1000.0);

    if (result.Cancelled) return Reply(CommandStatus::Cancelled, "命令执行已取消\n" + result.Output);
    if (result.TimedOut)
    {
        return Reply(CommandStatus::Error, "命令执行超时（" + formatted_timeout + "s）\n" + result.Output);
    }
    if (!result.Error.empty())
    {
        return Reply(CommandStatus::Error, "执行失败: " + result.Error + "\n" + result.Output);
    }
    if (result.ExitCode != 0)
    {
        return Reply(CommandStatus::Error,
                     result.Output + "\n(exit code: " + std::to_string(result.ExitCode) + ")");
    }
    if (Trim(result.Output).empty()) return Reply(CommandStatus::Completed, "命令执行成功");
    return Reply(CommandStatus::Completed, result.Output);
}

CommandReplyModel Executor::SyncTimeCommand(const CommandModel& command)
{
    ProtocolTime target;
    const auto text = Trim(command.Argument);
    if (!text.empty())
    {
        target = ProtocolTime::Parse(text);
        if (!target.IsSet()) return Reply(CommandStatus::Error, "时间格式无效: " + text);
    }

    const auto now = Clock::now();
    if (!target.IsSet())
    {
        if (options_.ServerNow)
        {
            try
            {
                target = ProtocolTime::FromTimePoint(options_.ServerNow());
            }
            catch (...)
            {
                target = ProtocolTime::FromTimePoint(now);
            }
        }
        else
        {
            target = ProtocolTime::FromTimePoint(now);
        }
    }

    const auto difference = target.Value() - now;
    const auto milliseconds =
        std::chrono::duration_cast<std::chrono::milliseconds>(difference).count();
    if (std::llabs(milliseconds) < 1000) return Reply(CommandStatus::Completed, "无需同步");

    if (!options_.SyncTime) return Reply(CommandStatus::Error, "当前平台不支持时间同步");
    bool success = false;
    try
    {
        success = options_.SyncTime(target.Value());
    }
    catch (const std::exception& ex)
    {
        return Reply(CommandStatus::Error, std::string("同步系统时间失败: ") + ex.what());
    }
    if (!success) return Reply(CommandStatus::Error, "同步系统时间失败（权限不足）");

    return Reply(CommandStatus::Completed,
                 "同步时间为 " + target.Text() + "，偏差 " + std::to_string(milliseconds) + "ms");
}

CommandReplyModel Executor::SetChannelCommand(const CommandModel& command)
{
    const auto channel = Trim(command.Argument);
    if (channel.empty()) return Reply(CommandStatus::Error, "参数为空");
    if (!IsValidChannel(channel))
    {
        return Reply(CommandStatus::Error, "通道名称无效（仅允许字母、数字、点、下划线、短横线，最长 64）");
    }
    if (!options_.SetChannel) return Reply(CommandStatus::Error, "不支持修改更新通道");
    try
    {
        if (!options_.SetChannel(channel)) return Reply(CommandStatus::Error, "保存通道失败");
    }
    catch (const std::exception& ex)
    {
        return Reply(CommandStatus::Error, std::string("保存通道失败: ") + ex.what());
    }
    return Reply(CommandStatus::Completed, "success " + channel);
}

CommandReplyModel Executor::RestartCommand(const CommandModel& command)
{
    if (!options_.Restart) return Reply(CommandStatus::Error, "不支持命令: " + command.Command);
    TrackThread(std::thread([this]() {
        if (!platform::SleepCancelable(1000, stopping_)) return;
        try
        {
            if (!options_.Restart()) Log().Error("restart process failed");
        }
        catch (const std::exception& ex)
        {
            Log().Error(std::string("restart process failed: ") + ex.what());
        }
    }));
    return Reply(CommandStatus::Completed, "正在重启");
}

} // namespace staragent
