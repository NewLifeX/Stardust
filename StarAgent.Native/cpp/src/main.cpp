// SPDX-License-Identifier: MIT
#include "agent.hpp"
#include "config.hpp"
#include "logging.hpp"
#include "platform.hpp"
#include "protocol.hpp"
#include "service.hpp"
#include "util.hpp"

#include <algorithm>
#include <atomic>
#include <csignal>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <map>
#include <memory>
#include <set>
#include <string>
#include <thread>
#include <vector>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

#ifndef STARAGENT_VERSION
#define STARAGENT_VERSION "1.0.2026.1017"
#endif
#ifndef STARAGENT_BUILD_TIME
#define STARAGENT_BUILD_TIME ""
#endif

namespace {

using staragent::Config;
using staragent::Logger;

volatile std::sig_atomic_t g_signal_flag = 0;

void SignalHandler(int) { g_signal_flag = 1; }

#ifdef _WIN32
BOOL WINAPI ConsoleHandler(DWORD control)
{
    if (control == CTRL_C_EVENT || control == CTRL_BREAK_EVENT || control == CTRL_CLOSE_EVENT ||
        control == CTRL_SHUTDOWN_EVENT)
    {
        g_signal_flag = 1;
        return TRUE;
    }
    return FALSE;
}
#endif

struct Arguments
{
    std::string Command;
    std::map<std::string, std::string> Values;
    std::set<std::string> Flags;
    std::vector<std::string> Raw;

    bool Has(const std::string& name) const { return Flags.count(name) > 0; }

    std::string Value(const std::string& name, const std::string& fallback = {}) const
    {
        const auto it = Values.find(name);
        return it == Values.end() ? fallback : it->second;
    }
};

Arguments ParseArguments(int argc, char* argv[])
{
    Arguments result;
    if (argc > 1) result.Command = staragent::ToLower(argv[1]);
    for (int i = 0; i < argc; i++) result.Raw.emplace_back(argv[i]);

    for (int i = 2; i < argc; i++)
    {
        std::string argument = argv[i];
        if (!staragent::StartsWith(argument, "-")) continue;

        auto name = argument;
        while (!name.empty() && name.front() == '-') name.erase(0, 1);
        std::string value;
        const auto equals = name.find('=');
        if (equals != std::string::npos)
        {
            value = name.substr(equals + 1);
            name = name.substr(0, equals);
        }
        else if (i + 1 < argc && !staragent::StartsWith(argv[i + 1], "-"))
        {
            value = argv[++i];
        }

        name = staragent::ToLower(name);
        if (value.empty())
        {
            result.Flags.insert(name);
        }
        else
        {
            result.Values[name] = value;
        }
    }
    return result;
}

void PrintUsage()
{
    std::cout << "StarAgent Native C++ " << STARAGENT_VERSION << "\n\n"
              << "Usage:\n  staragent <command> [options]\n\n"
              << "Commands:\n"
              << "  run       以长连接方式运行（Linux/systemd、Windows 服务、macOS/launchd）\n"
              << "  once      登录、心跳并处理一次命令后退出\n"
              << "  version   显示版本信息\n"
              << "  install   安装为系统服务\n"
              << "  uninstall 卸载系统服务\n"
              << "  start     启动服务\n"
              << "  stop      停止服务\n"
              << "  status    查看服务状态\n\n"
              << "Run options:\n"
              << "  --config <path>      配置文件路径（默认 staragent.json 或 STARAGENT_CONFIG）\n"
              << "  --server <url>       覆盖 Server\n"
              << "  --data <dir>         覆盖 DataDir\n"
              << "  --log-level <level>  debug/info/warn/error\n"
              << "  --allow-exec         启用 shell 命令执行（默认关闭）\n"
              << "  --console            同时输出日志到 stderr\n"
              << "  --service            Windows 服务模式（由服务管理器传入）\n\n"
              << "Install options:\n"
              << "  --config <path> --server <url> --data <dir> --user <user> --log-level <level>\n\n"
              << "Environment: STARAGENT_SERVER, STARAGENT_PRODUCTCODE/STARAGENT_PRODUCT_CODE,\n"
              << "  STARAGENT_CODE, STARAGENT_SECRET, STARAGENT_PROJECT, STARAGENT_CHANNEL,\n"
              << "  STARAGENT_DATADIR/STARAGENT_DATA_DIR, STARAGENT_LOGLEVEL/STARAGENT_LOG_LEVEL,\n"
              << "  STARAGENT_METRICSINTERVAL/STARAGENT_METRICS_INTERVAL, STARAGENT_ALLOWEXEC,\n"
              << "  STARAGENT_ALLOWEDCOMMANDS, STARAGENT_COMMANDTIMEOUT, STARAGENT_TLSVERIFY\n";
}

std::string DefaultConfig() { return staragent::DefaultConfigPath(); }

void ApplyOverrides(Config& config, const Arguments& arguments, bool allow_exec_flag)
{
    if (!arguments.Value("server").empty()) config.Server = arguments.Value("server");
    if (!arguments.Value("data").empty()) config.DataDir = arguments.Value("data");
    if (!arguments.Value("log-level").empty()) config.LogLevel = arguments.Value("log-level");
    if (allow_exec_flag) config.AllowExec = true;
    staragent::Normalize(config);
}

int RunAgent(const Arguments& arguments, bool once)
{
    const auto config_path = arguments.Value("config", DefaultConfig());
    Config config;
    try
    {
        config = staragent::LoadConfig(config_path);
        ApplyOverrides(config, arguments, arguments.Has("allow-exec"));
        staragent::Validate(config);
    }
    catch (const std::exception& ex)
    {
        std::cerr << "staragent: " << ex.what() << "\n";
        return 1;
    }

    const bool console = once ? arguments.Has("console") : !arguments.Has("no-console");
    std::unique_ptr<Logger> logger;
    try
    {
        logger = std::make_unique<Logger>((std::filesystem::path(config.DataDir) / "logs").string(),
                                          config.LogLevel, console);
    }
    catch (const std::exception& ex)
    {
        std::cerr << "staragent: log setup failed, using stderr: " << ex.what() << "\n";
        logger = std::make_unique<Logger>("", config.LogLevel, true);
    }

    auto build_time = staragent::ProtocolTime::Parse(STARAGENT_BUILD_TIME);
    staragent::AgentOptions options;
    options.ConfigPath = config_path;
    options.Configuration = config;
    options.Log = logger.get();
    options.Version = STARAGENT_VERSION;
    options.BuildTime = build_time.IsSet() ? build_time.Value() : staragent::Clock::now();
    options.Arguments = arguments.Raw;

    std::unique_ptr<staragent::Agent> agent;
    try
    {
        agent = std::make_unique<staragent::Agent>(std::move(options));
    }
    catch (const std::exception& ex)
    {
        std::cerr << "staragent: " << ex.what() << "\n";
        return 1;
    }

    std::signal(SIGINT, SignalHandler);
    std::signal(SIGTERM, SignalHandler);
#ifdef _WIN32
    ::SetConsoleCtrlHandler(ConsoleHandler, TRUE);
#endif

    if (!once && arguments.Has("service"))
    {
        const int service_result = staragent::service::RunAsService(
            [&]() { return agent->Run(); }, [&]() { agent->RequestStop(); });
        if (service_result >= 0)
        {
            logger->Close();
            return service_result;
        }
        // Not started by the service manager: continue in console mode.
    }

    std::atomic<bool> finished{false};
    std::thread watcher([&]() {
        while (!finished.load())
        {
            if (g_signal_flag != 0)
            {
                agent->RequestStop();
                return;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        }
    });

    const int result = once ? agent->Once() : agent->Run();
    finished = true;
    if (watcher.joinable()) watcher.join();
    logger->Close();
    return result;
}

int InstallAgent(const Arguments& arguments)
{
    const auto config_path = arguments.Value("config", DefaultConfig());
    Config config;
    bool exists = false;
    try
    {
        std::error_code error;
        exists = std::filesystem::exists(config_path, error);
        if (exists)
        {
            config = staragent::LoadConfig(config_path);
        }
        else
        {
            if (arguments.Value("server").empty())
            {
                std::cerr << "staragent: --server is required when the config file does not exist\n";
                return 1;
            }
            config = staragent::DefaultConfig();
        }
        ApplyOverrides(config, arguments, false);
        staragent::Validate(config);
        staragent::SaveConfigAtomic(config_path, config);
    }
    catch (const std::exception& ex)
    {
        std::cerr << "staragent: " << ex.what() << "\n";
        return 1;
    }

    staragent::service::InstallOptions options;
    options.ConfigPath = std::filesystem::absolute(config_path).string();
    options.InstallDir = arguments.Value("dir");
    options.DataDir = config.DataDir;
    options.ServiceUser = arguments.Value("user");
    options.Server = config.Server;

    const auto result = staragent::service::Install(options);
    std::cout << result.Message << "\n";
    if (result.Success) std::cout << "config=" << options.ConfigPath << "\n";
    return result.Success ? 0 : 1;
}

void PrintVersion()
{
    std::cout << "StarAgent Native C++\nVersion: " << STARAGENT_VERSION << "\nBuildTime: " << STARAGENT_BUILD_TIME
              << "\nRuntime: C++17 " << staragent::platform::Architecture() << "/"
#if defined(_WIN32)
              << "windows"
#elif defined(__APPLE__)
              << "macos"
#else
              << "linux"
#endif
              << "\n";
}

} // namespace

int main(int argc, char* argv[])
{
    const auto arguments = ParseArguments(argc, argv);
    const auto& command = arguments.Command;

    if (command.empty())
    {
        PrintUsage();
        return 2;
    }

    if (command == "help" || command == "-h" || command == "--help")
    {
        PrintUsage();
        return 0;
    }
    if (command == "version" || command == "--version")
    {
        PrintVersion();
        return 0;
    }
    if (command == "run") return RunAgent(arguments, false);
    if (command == "once") return RunAgent(arguments, true);
    if (command == "install") return InstallAgent(arguments);

    if (command == "uninstall" || command == "start" || command == "stop")
    {
        const auto result = command == "uninstall" ? staragent::service::Uninstall()
                            : command == "start"   ? staragent::service::Start()
                                                    : staragent::service::Stop();
        std::cout << result.Message << "\n";
        return result.Success ? 0 : 1;
    }
    if (command == "status")
    {
        std::cout << staragent::service::Status() << "\n";
        return 0;
    }

    std::cerr << "unknown command \"" << command << "\"\n\n";
    PrintUsage();
    return 2;
}
