// SPDX-License-Identifier: MIT
// 引导安装：把原生 Agent 升级为 .NET 版 StarAgent。
// 原生 Agent 只做采集上报，需要完整能力时由运维触发一次引导安装：
// 下载 .NET 运行时 → 安装 → 下载完整版 StarAgent → 解压 → 调用其 -install。
#pragma once

#include "config.hpp"
#include "http_client.hpp"
#include "logging.hpp"
#include "protocol.hpp"

#include <atomic>
#include <string>

namespace staragent {

class Bootstrapper
{
public:
    Bootstrapper(HttpClient& client, Logger& logger, const Config& config);

    // 执行引导安装。返回空串表示成功，否则为错误信息。
    std::string Run(const BootstrapInfo& plan, const std::atomic<bool>& cancelled);

private:
    // 下载并校验安装包，返回本地路径；失败时写入 error。
    bool Fetch(const BootstrapPackage& package, const std::string& directory, std::string& path, std::string& error);

    // 按平台安装运行时包（tar.gz / exe）。
    bool InstallRuntime(const BootstrapPackage& package, const std::string& path, std::string& error);

    // 按平台解压并安装完整版 StarAgent。
    bool InstallAgent(const BootstrapPackage& package, const std::string& path, const std::string& server, std::string& error);

    HttpClient& client_;
    Logger& logger_;
    Config config_;
};

} // namespace staragent
