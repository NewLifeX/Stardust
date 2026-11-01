// SPDX-License-Identifier: MIT
// Linux/macOS 共用实现：系统伪文件读取、网卡与标识解析、外层命令执行与自重启。
// 这些逻辑在两个 POSIX 平台完全一致，集中一份避免复制粘贴后各自漂移。
#include "platform.hpp"

#include "output_buffer.hpp"
#include "platform_posix.hpp"
#include "util.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <cerrno>
#include <chrono>
#include <csignal>
#include <cstdio>
#include <cstring>
#include <fcntl.h>
#include <fstream>
#include <netinet/in.h>
#include <pwd.h>
#include <set>
#include <sstream>
#include <sys/socket.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <thread>
#include <unistd.h>
#include <vector>

namespace staragent::platform {

namespace internal {

std::string ReadSysFile(const std::string& path, std::size_t max)
{
    std::ifstream stream(path, std::ios::binary);
    if (!stream) return {};
    std::string result;
    result.resize(max);
    stream.read(result.data(), static_cast<std::streamsize>(max));
    result.resize(static_cast<std::size_t>(stream.gcount()));
    return result;
}

std::string ReadSysTrim(const std::string& path) { return Trim(ReadSysFile(path, 64 * 1024)); }

bool IsVirtualInterface(const std::string& name)
{
    const auto lower = ToLower(name);
    const char* prefixes[] = {"lo",     "docker",    "veth", "br-",   "virbr",     "vmnet", "tun",
                              "tap",    "utun",      "awdl", "llw",   "bridge",    "vboxnet", "zt",
                              "tailscale", "wg",     "cni",  "flannel"};
    for (const char* prefix : prefixes)
    {
        if (StartsWith(lower, prefix)) return true;
    }
    return false;
}

std::string FormatMac(const unsigned char* data, std::size_t length)
{
    char text[64];
    std::string result;
    for (std::size_t i = 0; i < length; i++)
    {
        std::snprintf(text, sizeof(text), "%s%02X", i == 0 ? "" : "-", data[i]);
        result += text;
    }
    return result;
}

std::string SockaddrToIp(const void* address)
{
    if (address == nullptr) return {};
    const auto* base = static_cast<const sockaddr*>(address);
    char text[INET6_ADDRSTRLEN] = {};
    if (base->sa_family == AF_INET)
    {
        const auto* ipv4 = static_cast<const sockaddr_in*>(address);
        if (::inet_ntop(AF_INET, &ipv4->sin_addr, text, sizeof(text)) == nullptr) return {};
        return text;
    }
    if (base->sa_family == AF_INET6)
    {
        const auto* ipv6 = static_cast<const sockaddr_in6*>(address);
        if (::inet_ntop(AF_INET6, &ipv6->sin6_addr, text, sizeof(text)) == nullptr) return {};
        return text;
    }
    return {};
}

std::string CleanId(std::string value)
{
    value = Trim(value);
    if (!value.empty() && (value.front() == '"' || value.front() == '\'')) value.erase(0, 1);
    if (!value.empty() && (value.back() == '"' || value.back() == '\'')) value.pop_back();
    const auto lower = ToLower(value);
    static const std::set<std::string> invalid = {"",
                                                  "none",
                                                  "unknown",
                                                  "not specified",
                                                  "not settable",
                                                  "to be filled by o.e.m.",
                                                  "default string",
                                                  "system serial number",
                                                  "0",
                                                  "00000000-0000-0000-0000-000000000000",
                                                  "ffffffff-ffff-ffff-ffff-ffffffffffff"};
    return invalid.count(lower) > 0 ? std::string() : value;
}

std::string DnsServers()
{
    std::istringstream stream(ReadSysFile("/etc/resolv.conf", 64 * 1024));
    std::string line;
    std::vector<std::string> servers;
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string key, value;
        if (fields >> key >> value && key == "nameserver") servers.push_back(value);
    }
    return Join(servers, ",");
}

} // namespace internal

std::string ExecutableDir()
{
    const auto path = ExecutablePath();
    const auto position = path.find_last_of('/');
    return position == std::string::npos ? std::string(".") : path.substr(0, position);
}

int ProcessId() { return static_cast<int>(::getpid()); }

bool IsRoot() { return ::geteuid() == 0; }

std::string HomeDir()
{
    if (const auto* user = ::getpwuid(::geteuid()))
    {
        if (user->pw_dir != nullptr) return user->pw_dir;
    }
    return staragent::EnvironmentValue({"HOME"});
}

bool SetSystemTime(TimePoint value)
{
    const auto milliseconds =
        std::chrono::duration_cast<std::chrono::milliseconds>(value.time_since_epoch()).count();
    timeval time{};
    time.tv_sec = static_cast<time_t>(milliseconds / 1000);
    time.tv_usec = static_cast<suseconds_t>((milliseconds % 1000) * 1000);
    return ::settimeofday(&time, nullptr) == 0;
}

ShellResult RunShell(const std::string& script, int timeout_ms, const std::atomic<bool>& cancelled)
{
    ShellResult result;
    if (timeout_ms <= 0) timeout_ms = 30000;

    int pipe_fds[2];
    if (::pipe(pipe_fds) != 0)
    {
        result.Error = "pipe failed";
        return result;
    }

    const pid_t pid = ::fork();
    if (pid < 0)
    {
        ::close(pipe_fds[0]);
        ::close(pipe_fds[1]);
        result.Error = "fork failed";
        return result;
    }
    if (pid == 0)
    {
        ::setsid();
        ::close(pipe_fds[0]);
        ::dup2(pipe_fds[1], STDOUT_FILENO);
        ::dup2(pipe_fds[1], STDERR_FILENO);
        ::close(pipe_fds[1]);
        const int null_fd = ::open("/dev/null", O_RDONLY);
        if (null_fd >= 0)
        {
            ::dup2(null_fd, STDIN_FILENO);
            ::close(null_fd);
        }
        // macOS 默认没有 bash 时退到 sh；exec 成功就不会返回。
        ::execl("/bin/bash", "bash", "-c", script.c_str(), static_cast<char*>(nullptr));
        ::execl("/bin/sh", "sh", "-c", script.c_str(), static_cast<char*>(nullptr));
        ::_exit(127);
    }

    ::close(pipe_fds[1]);
    ::fcntl(pipe_fds[0], F_SETFL, O_NONBLOCK);

    HeadTailBuffer buffer(64 * 1024);
    const auto started = SteadyMsNow();
    bool killed = false;
    int status = 0;

    auto drain = [&]() {
        char chunk[4096];
        for (;;)
        {
            const auto read = ::read(pipe_fds[0], chunk, sizeof(chunk));
            if (read > 0)
            {
                buffer.Append(chunk, static_cast<std::size_t>(read));
                continue;
            }
            break;
        }
    };

    for (;;)
    {
        drain();
        const auto waited = ::waitpid(pid, &status, WNOHANG);
        if (waited == pid) break;
        if (cancelled.load())
        {
            result.Cancelled = true;
            killed = true;
            ::kill(-pid, SIGKILL);
            ::kill(pid, SIGKILL);
            break;
        }
        if (SteadyMsNow() - started > timeout_ms)
        {
            result.TimedOut = true;
            killed = true;
            ::kill(-pid, SIGKILL);
            ::kill(pid, SIGKILL);
            break;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }

    if (killed) ::waitpid(pid, &status, 0);
    drain();
    ::close(pipe_fds[0]);

    result.Output = buffer.ToString();
    result.Truncated = buffer.Truncated();
    result.ExitCode = killed ? -1 : (WIFEXITED(status) ? WEXITSTATUS(status) : -1);
    if (result.ExitCode == 127 && Trim(result.Output).empty()) result.Error = "no usable shell (bash/sh)";
    return result;
}

bool RestartSelf(const std::vector<std::string>& arguments)
{
    if (arguments.empty()) return false;
    const pid_t pid = ::fork();
    if (pid < 0) return false;
    if (pid == 0)
    {
        ::setsid();
        std::vector<char*> argv;
        argv.reserve(arguments.size() + 1);
        for (const auto& argument : arguments) argv.push_back(const_cast<char*>(argument.c_str()));
        argv.push_back(nullptr);
        ::execv(argv[0], argv.data());
        ::_exit(127);
    }
    return true;
}

} // namespace staragent::platform
