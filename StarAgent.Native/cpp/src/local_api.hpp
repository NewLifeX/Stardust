// SPDX-License-Identifier: MIT
// StarAgent Native C++ - local UDP API (NewLife SRMP + JsonEncoder compatible).
//
// 与 .NET StarAgent 的本地接口保持线格式一致：UDP 5500 上收发的数据报是
// SRMP 帧（1 Flag + 1 Sequence + 2 Length + Payload），Payload 为
// [7位变长长度][action][4字节数据长度][JSON/二进制数据]。
// 局域网广播探测（LocalStarClient.Scan）即发送 action=Info 的该类帧。
#pragma once

#include <atomic>
#include <functional>
#include <string>
#include <thread>

namespace staragent {

class Logger;

class LocalApiServer
{
public:
    // 返回 action=Info 的 AgentInfo JSON；空字符串表示暂不可用。
    using InfoProvider = std::function<std::string()>;

    explicit LocalApiServer(Logger& logger);
    ~LocalApiServer();

    LocalApiServer(const LocalApiServer&) = delete;
    LocalApiServer& operator=(const LocalApiServer&) = delete;

    // 绑定 0.0.0.0:port 并启动接收线程；port<=0 或绑定失败时返回 false。
    bool Start(int port, InfoProvider info);
    void Stop();

    static bool Available();

private:
    void Loop();
    std::string Handle(const std::string& action, const std::string& json);

    Logger& logger_;
    InfoProvider info_;
    std::atomic<bool> stopping_{false};
    std::thread thread_;
    int port_ = 0;
#ifdef _WIN32
    unsigned long long socket_ = static_cast<unsigned long long>(-1);
#else
    int socket_ = -1;
#endif
};

} // namespace staragent
