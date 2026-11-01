// SPDX-License-Identifier: MIT
#include "ws_client.hpp"

#include "util.hpp"

#include <chrono>
#include <memory>
#include <thread>

#ifdef STARAGENT_WITH_WS
#include "ixwebsocket/IXWebSocket.h"
#include "ixwebsocket/IXWebSocketMessage.h"
#endif

namespace staragent {
namespace {

constexpr int kConnectTimeoutMs = 10000;

bool WaitUntil(const std::atomic<bool>& condition_true, const std::function<bool()>& stopped, int timeout_ms)
{
    const auto deadline = SteadyMsNow() + timeout_ms;
    while (!condition_true.load() && !stopped() && SteadyMsNow() < deadline)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
    return condition_true.load();
}

} // namespace

#ifdef STARAGENT_WITH_WS

WsClient::WsClient(Logger& logger) : logger_(logger) {}
WsClient::~WsClient() { Stop(); }

bool WsClient::Available() { return true; }

void WsClient::Stop()
{
    // 只做标记，真正的 socket->stop() 由 Run() 线程执行，避免并发 join 死锁
    stop_requested_ = true;
}

bool WsClient::Run(const Options& options, const std::atomic<bool>& stopping)
{
    if (options.Url.empty()) return false;

    stop_requested_ = false;
    const auto should_stop = [&]() { return stopping.load() || stop_requested_.load(); };

    std::atomic<bool> opened{false};
    std::atomic<bool> closed{false};
    std::atomic<std::int64_t> last_pong{SteadyMsNow()};

    auto socket = std::make_unique<ix::WebSocket>();
    socket->disableAutomaticReconnection();
    socket->setUrl(options.Url);
    socket->setHandshakeTimeout(kConnectTimeoutMs / 1000);
    socket->setPingInterval(0);

    ix::WebSocketHttpHeaders headers;
    headers["Authorization"] = "Bearer " + options.Token;
    socket->setExtraHeaders(headers);

    std::mutex send_mutex;
    auto send_text = [&](const std::string& text) {
        std::lock_guard<std::mutex> lock(send_mutex);
        const auto result = socket->sendText(text);
        return result.success;
    };

    socket->setOnMessageCallback([&](const ix::WebSocketMessagePtr& message) {
        if (message->type == ix::WebSocketMessageType::Open)
        {
            opened = true;
            logger_.Info("notify websocket connected");
            if (options.OnOpen) options.OnOpen();
            return;
        }
        if (message->type == ix::WebSocketMessageType::Close)
        {
            closed = true;
            return;
        }
        if (message->type == ix::WebSocketMessageType::Error)
        {
            if (!message->errorInfo.reason.empty())
            {
                logger_.Warn("notify websocket error: " + message->errorInfo.reason);
            }
            closed = true;
            return;
        }
        if (message->type != ix::WebSocketMessageType::Message) return;

        if (message->str == "Ping")
        {
            send_text("Pong");
            return;
        }
        if (message->str == "Pong")
        {
            last_pong = SteadyMsNow();
            if (options.OnPong) options.OnPong();
            return;
        }

        auto value = Json::parse(message->str, nullptr, false);
        if (value.is_discarded() || !value.is_object())
        {
            logger_.Warn("notify non-JSON text frame ignored");
            return;
        }
        try
        {
            auto command = ParseCommand(value);
            if (Trim(command.Command).empty()) return;
            if (options.OnCommand) options.OnCommand(command);
        }
        catch (const std::exception& ex)
        {
            logger_.Warn(std::string("notify command ignored: ") + ex.what());
        }
    });

    {
        socket_ = socket.get();
    }

    socket->start();

    const bool connected = WaitUntil(opened, should_stop, kConnectTimeoutMs);
    if (!connected)
    {
        socket->stop();
        socket_ = nullptr;
        return false;
    }

    const auto interval_ms = std::max(1, options.PingIntervalSeconds) * 1000;
    std::thread keepalive([&]() {
        int elapsed = 0;
        while (!should_stop() && !closed.load())
        {
            constexpr int tick = 100;
            std::this_thread::sleep_for(std::chrono::milliseconds(tick));
            elapsed += tick;
            if (elapsed < interval_ms) continue;
            elapsed = 0;

            if (SteadyMsNow() - last_pong.load() > 3LL * interval_ms)
            {
                logger_.Warn("notify pong timeout, closing websocket");
                closed = true;
                return;
            }
            if (!send_text("Ping"))
            {
                logger_.Warn("notify ping send failed");
                closed = true;
                return;
            }
        }
    });

    while (!should_stop() && !closed.load())
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }

    // 仅此处执行一次停止，避免多线程并发 join 内部线程
    socket->stop();
    keepalive.join();
    socket_ = nullptr;
    return true;
}

#else

WsClient::WsClient(Logger& logger) : logger_(logger) {}
WsClient::~WsClient() = default;

bool WsClient::Available() { return false; }

void WsClient::Stop() {}

bool WsClient::Run(const Options&, const std::atomic<bool>&)
{
    logger_.Warn("WebSocket support was not compiled in; commands fall back to HTTP heartbeat");
    return false;
}

#endif

} // namespace staragent
