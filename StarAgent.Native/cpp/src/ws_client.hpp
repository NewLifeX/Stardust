// SPDX-License-Identifier: MIT
#pragma once

#include "logging.hpp"
#include "protocol.hpp"

#include <atomic>
#include <functional>
#include <mutex>
#include <string>

namespace ix {
class WebSocket;
}

namespace staragent {

class WsClient
{
public:
    struct Options
    {
        std::string Url;
        std::string Token;
        int PingIntervalSeconds = 60;
        std::function<void(const CommandModel&)> OnCommand;
        std::function<void()> OnOpen;
        std::function<void()> OnPong;
    };

    explicit WsClient(Logger& logger);
    ~WsClient();

    WsClient(const WsClient&) = delete;
    WsClient& operator=(const WsClient&) = delete;

    // Blocking: connects, processes frames until the connection closes or
    // stopping becomes true. Returns true when a connection was established.
    bool Run(const Options& options, const std::atomic<bool>& stopping);

    // Thread-safe; may be called from another thread to abort Run.
    void Stop();

    static bool Available();

private:
    Logger& logger_;
    // 只置位、不直接操作 socket：IXWebSocket::stop() 会 join 内部线程，
    // 多线程并发调用会互等死锁，因此统一由 Run() 所在线程执行 stop()。
    std::atomic<bool> stop_requested_{false};
    ix::WebSocket* socket_ = nullptr;
};

} // namespace staragent
