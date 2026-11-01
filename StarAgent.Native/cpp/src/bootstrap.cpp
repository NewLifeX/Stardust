// SPDX-License-Identifier: MIT
#include "bootstrap.hpp"

#include "platform.hpp"
#include "util.hpp"

#include <algorithm>
#include <filesystem>

namespace staragent {
namespace {

// 安装目录与运行时目录，与 .NET 版 StarAgent 及官方安装脚本保持一致
#ifdef _WIN32
const char* kAgentDir = "C:\\Program Files\\StarAgent";
const char* kRuntimeDir = "C:\\Program Files\\dotnet";
#elif defined(__APPLE__)
const char* kAgentDir = "/opt/staragent";
const char* kRuntimeDir = "/usr/local/share/dotnet";
#else
const char* kAgentDir = "/opt/staragent";
const char* kRuntimeDir = "/usr/share/dotnet";
#endif

#ifdef _WIN32
std::string Quote(const std::string& value) { return "\"" + value + "\""; }
#else
std::string Quote(const std::string& value)
{
    std::string result = "'";
    for (const auto ch : value)
    {
        if (ch == '\'')
            result += "'\\''";
        else
            result += ch;
    }
    result += "'";
    return result;
}
#endif

std::string FileNameOf(const BootstrapPackage& package, const char* fallback)
{
    if (!package.FileName.empty()) return package.FileName;
    if (!package.Source.empty())
    {
        const auto position = package.Source.find_last_of('/');
        if (position != std::string::npos && position + 1 < package.Source.size())
            return package.Source.substr(position + 1);
    }
    return fallback;
}

bool EndsWith(const std::string& value, const std::string& suffix)
{
    return value.size() >= suffix.size() &&
           value.compare(value.size() - suffix.size(), suffix.size(), suffix) == 0;
}

} // namespace

Bootstrapper::Bootstrapper(HttpClient& client, Logger& logger, const Config& config)
    : client_(client), logger_(logger), config_(config)
{
}

bool Bootstrapper::Fetch(const BootstrapPackage& package, const std::string& directory, std::string& path,
                         std::string& error)
{
    if (package.Empty())
    {
        error = "package source is empty";
        return false;
    }

    std::error_code ec;
    std::filesystem::create_directories(directory, ec);
    if (ec)
    {
        error = "create directory failed: " + ec.message();
        return false;
    }

    path = (std::filesystem::path(directory) / FileNameOf(package, "package.bin")).string();
    std::error_code remove_error;
    std::filesystem::remove(path, remove_error);

    logger_.Info("bootstrap download " + package.Source + " -> " + path);
    if (!client_.Download(package.Source, path, error)) return false;

    // 目前服务端只保证 MD5（32 位十六进制）；其他哈希留待服务端统一后扩展
    if (package.FileHash.size() == 32)
    {
        const auto actual = ToLower(Md5File(path));
        const auto expected = ToLower(Trim(package.FileHash));
        if (actual != expected)
        {
            error = "hash mismatch: expected " + expected + " actual " + actual;
            return false;
        }
    }

    return true;
}

bool Bootstrapper::InstallRuntime(const BootstrapPackage& package, const std::string& path, std::string& error)
{
    (void)package;

#ifdef _WIN32
    // 官方安装包支持 /install /quiet /norestart；3010 表示需要重启但已安装成功
    const auto result = platform::RunShell(Quote(path) + " /install /quiet /norestart", 30 * 60 * 1000,
                                           std::atomic<bool>{false});
    if (result.ExitCode != 0 && result.ExitCode != 3010)
    {
        error = "runtime installer failed: exit=" + std::to_string(result.ExitCode) + " " + Trim(result.Output);
        return false;
    }
#else
    const auto script = "mkdir -p " + Quote(kRuntimeDir) + " && tar -xzf " + Quote(path) + " -C " + Quote(kRuntimeDir) +
                        " && ln -sf " + Quote(std::string(kRuntimeDir) + "/dotnet") + " /usr/bin/dotnet";
    const auto result = platform::RunShell(script, 30 * 60 * 1000, std::atomic<bool>{false});
    if (result.ExitCode != 0)
    {
        error = "runtime extract failed: exit=" + std::to_string(result.ExitCode) + " " + Trim(result.Output);
        return false;
    }
#endif
    return true;
}

bool Bootstrapper::InstallAgent(const BootstrapPackage& package, const std::string& path, const std::string& server,
                                std::string& error)
{
    const auto server_arg = server.empty() ? config_.Server : server;

#ifdef _WIN32
    (void)package;
    const auto expand = "powershell -NoProfile -Command \"Expand-Archive -Force -LiteralPath " + Quote(path) +
                        " -DestinationPath " + Quote(kAgentDir) + "\"";
    const auto result = platform::RunShell(expand, 10 * 60 * 1000, std::atomic<bool>{false});
    if (result.ExitCode != 0)
    {
        error = "unpack failed: exit=" + std::to_string(result.ExitCode) + " " + Trim(result.Output);
        return false;
    }
    const auto install = "cd /d " + Quote(kAgentDir) + " && dotnet StarAgent.dll -install -server " + server_arg;
#else
    // .zip 与 .tar.gz 两种发布包都支持
    const auto unpack = EndsWith(ToLower(package.FileName), ".zip")
                            ? "mkdir -p " + Quote(kAgentDir) + " && cd " + Quote(kAgentDir) + " && unzip -o " + Quote(path)
                            : "mkdir -p " + Quote(kAgentDir) + " && tar -xzf " + Quote(path) + " -C " + Quote(kAgentDir);
    const auto result = platform::RunShell(unpack, 10 * 60 * 1000, std::atomic<bool>{false});
    if (result.ExitCode != 0)
    {
        error = "unpack failed: exit=" + std::to_string(result.ExitCode) + " " + Trim(result.Output);
        return false;
    }
    const auto install = "cd " + Quote(kAgentDir) + " && dotnet StarAgent.dll -install -server " + server_arg;
#endif

    const auto installed = platform::RunShell(install, 5 * 60 * 1000, std::atomic<bool>{false});
    if (installed.ExitCode != 0)
    {
        error = "agent install failed: exit=" + std::to_string(installed.ExitCode) + " " + Trim(installed.Output);
        return false;
    }

    return true;
}

std::string Bootstrapper::Run(const BootstrapInfo& plan, const std::atomic<bool>& cancelled)
{
    const auto server = plan.Server.empty() ? config_.Server : plan.Server;

    if (plan.Agent.Empty()) return "server did not return an agent package";

#ifndef _WIN32
    if (!platform::IsRoot()) return "bootstrap requires root privileges";
#endif

    const auto directory = (std::filesystem::path(config_.DataDir) / "bootstrap").string();

    std::string path;
    std::string error;

    // 第一步：.NET 运行时。服务端未提供时不装（可能已自带或使用自包含包）
    if (!plan.Runtime.Empty())
    {
        if (cancelled.load()) return "cancelled";
        if (!Fetch(plan.Runtime, directory, path, error)) return "runtime: " + error;

        logger_.Info("bootstrap installing .NET runtime " + plan.Runtime.Version);
        if (!InstallRuntime(plan.Runtime, path, error)) return "runtime: " + error;
    }
    else
    {
        logger_.Info("bootstrap skipped runtime (no package)");
    }

    // 第二步：完整版 StarAgent
    if (cancelled.load()) return "cancelled";
    if (!Fetch(plan.Agent, directory, path, error)) return "agent: " + error;

    logger_.Info("bootstrap installing StarAgent " + plan.Version + " server=" + server);
    if (!InstallAgent(plan.Agent, path, server, error)) return "agent: " + error;

    return {};
}

} // namespace staragent
