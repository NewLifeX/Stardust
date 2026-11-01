// SPDX-License-Identifier: MIT
#pragma once

#include <functional>
#include <mutex>
#include <string>
#include <vector>

namespace staragent {

struct Config
{
    std::string Server;
    std::string ProductCode;
    std::string Code;
    std::string Secret;
    std::string Project;
    std::string Channel;
    std::string DataDir;
    std::string LogLevel;
    int MetricsInterval = 60;
    bool AllowExec = false;
    std::vector<std::string> AllowedCommands;
    int CommandTimeout = 30;
    bool TlsVerify = true;
    // 本地 UDP 接口端口（局域网发现与本地 RPC），0 表示关闭
    int LocalPort = 5500;
    // 收到服务端升级通知后是否自动下载并替换自身
    bool AutoUpgrade = true;
    // 是否允许服务端下发 node/bootstrap 触发引导安装（默认关闭，与 AllowExec 同级安全策略）
    bool Bootstrap = false;
    // 最近一次成功应用的升级版本，避免同版本反复重启
    std::string UpgradeVersion;
};

Config DefaultConfig();
std::string DefaultConfigPath();
std::string EnvironmentValue(const std::vector<std::string>& names);

// Loads staragent.json. A missing file yields defaults; environment variables
// with the STARAGENT_ prefix are applied afterwards.
Config LoadConfig(const std::string& path);
void ApplyEnvironment(Config& config);
void Normalize(Config& config);
void Validate(const Config& config);

void SaveConfigAtomic(const std::string& path, const Config& config);
// Writes data through a temporary file and an atomic rename; permissions are
// restricted to the owner.
void WriteFileAtomic(const std::string& path, const std::string& data);

class ConfigStore
{
public:
    ConfigStore(std::string path, Config config);

    std::string Path() const { return path_; }
    Config Snapshot() const;
    // Applies fn to a copy, persists it atomically and only then updates
    // memory. Throws std::runtime_error on failure.
    Config Update(const std::function<void(Config&)>& fn);

private:
    mutable std::mutex mutex_;
    std::string path_;
    Config config_;
};

} // namespace staragent
