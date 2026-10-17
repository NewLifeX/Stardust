// SPDX-License-Identifier: MIT
// macOS metrics, identity and command execution.
#include "platform.hpp"

#include "config.hpp"
#include "output_buffer.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <cerrno>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fcntl.h>
#include <fstream>
#include <ifaddrs.h>
#include <mach-o/dyld.h>
#include <mach/mach.h>
#include <mach/vm_statistics.h>
#include <mach/host_info.h>
#include <mutex>
#include <net/if.h>
#include <net/if_dl.h>
#include <net/if_var.h>
#include <netinet/in.h>
#include <pwd.h>
#include <set>
#include <sstream>
#include <sys/mount.h>
#include <sys/statvfs.h>
#include <sys/sysctl.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <thread>
#include <unistd.h>

namespace staragent::platform {
namespace {

std::mutex g_mutex;
std::uint64_t g_prev_user = 0;
std::uint64_t g_prev_system = 0;
std::uint64_t g_prev_idle = 0;
std::uint64_t g_prev_nice = 0;
bool g_has_cpu_sample = false;

std::string ReadFile(const std::string& path, std::size_t max = 1024 * 1024)
{
    std::ifstream stream(path, std::ios::binary);
    if (!stream) return {};
    std::string result(max, '\0');
    stream.read(result.data(), static_cast<std::streamsize>(max));
    result.resize(static_cast<std::size_t>(stream.gcount()));
    return result;
}

std::string SysctlString(const char* name)
{
    std::size_t size = 0;
    if (::sysctlbyname(name, nullptr, &size, nullptr, 0) != 0 || size == 0) return {};
    std::string result(size, '\0');
    if (::sysctlbyname(name, result.data(), &size, nullptr, 0) != 0) return {};
    while (!result.empty() && result.back() == '\0') result.pop_back();
    return result;
}

std::uint64_t SysctlU64(const char* name)
{
    std::uint64_t value = 0;
    std::size_t size = sizeof(value);
    if (::sysctlbyname(name, &value, &size, nullptr, 0) != 0) return 0;
    return value;
}

std::string FormatMac(const unsigned char* data, std::size_t length)
{
    std::string result;
    char text[8];
    for (std::size_t i = 0; i < length; i++)
    {
        std::snprintf(text, sizeof(text), "%s%02X", i == 0 ? "" : "-", data[i]);
        result += text;
    }
    return result;
}

std::string SockaddrToIp(const sockaddr* address)
{
    if (address == nullptr) return {};
    char text[INET6_ADDRSTRLEN] = {};
    if (address->sa_family == AF_INET)
    {
        const auto* ipv4 = reinterpret_cast<const sockaddr_in*>(address);
        if (::inet_ntop(AF_INET, &ipv4->sin_addr, text, sizeof(text)) == nullptr) return {};
        return text;
    }
    if (address->sa_family == AF_INET6)
    {
        const auto* ipv6 = reinterpret_cast<const sockaddr_in6*>(address);
        if (::inet_ntop(AF_INET6, &ipv6->sin6_addr, text, sizeof(text)) == nullptr) return {};
        return text;
    }
    return {};
}

bool IsVirtualInterface(const std::string& name)
{
    const auto lower = ToLower(name);
    const char* prefixes[] = {"lo", "utun", "awdl", "llw", "bridge", "vmnet", "vboxnet", "tun", "tap", "gif", "stf"};
    for (const char* prefix : prefixes)
    {
        if (StartsWith(lower, prefix)) return true;
    }
    return false;
}

std::string CleanId(std::string value)
{
    value = Trim(value);
    const auto lower = ToLower(value);
    static const std::set<std::string> invalid = {"", "none", "unknown", "not specified", "not settable",
                                                  "default string", "0",
                                                  "00000000-0000-0000-0000-000000000000"};
    return invalid.count(lower) > 0 ? std::string() : value;
}

std::string ParseRouteGateway()
{
    FILE* pipe = ::popen("route -n get default 2>/dev/null", "r");
    if (pipe == nullptr) return {};
    char line[512];
    std::string gateway;
    while (std::fgets(line, sizeof(line), pipe) != nullptr)
    {
        std::string text = line;
        const auto colon = text.find(':');
        if (colon == std::string::npos) continue;
        const auto key = Trim(text.substr(0, colon));
        if (key == "gateway") gateway = Trim(text.substr(colon + 1));
    }
    ::pclose(pipe);
    return gateway;
}

std::string ParseDns()
{
    std::istringstream stream(ReadFile("/etc/resolv.conf"));
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

} // namespace

double CpuPercent()
{
    host_cpu_load_info_data_t load{};
    mach_msg_type_number_t count = HOST_CPU_LOAD_INFO_COUNT;
    if (::host_statistics(::mach_host_self(), HOST_CPU_LOAD_INFO, reinterpret_cast<host_info_t>(&load), &count) !=
        KERN_SUCCESS)
    {
        return 0;
    }

    const auto user = static_cast<std::uint64_t>(load.cpu_ticks[CPU_STATE_USER]);
    const auto system = static_cast<std::uint64_t>(load.cpu_ticks[CPU_STATE_SYSTEM]);
    const auto idle = static_cast<std::uint64_t>(load.cpu_ticks[CPU_STATE_IDLE]);
    const auto nice = static_cast<std::uint64_t>(load.cpu_ticks[CPU_STATE_NICE]);

    std::lock_guard<std::mutex> lock(g_mutex);
    if (!g_has_cpu_sample)
    {
        g_prev_user = user;
        g_prev_system = system;
        g_prev_idle = idle;
        g_prev_nice = nice;
        g_has_cpu_sample = true;
        return 0;
    }
    const auto user_delta = user - g_prev_user;
    const auto system_delta = system - g_prev_system;
    const auto idle_delta = idle - g_prev_idle;
    const auto nice_delta = nice - g_prev_nice;
    g_prev_user = user;
    g_prev_system = system;
    g_prev_idle = idle;
    g_prev_nice = nice;

    const auto total = user_delta + system_delta + idle_delta + nice_delta;
    if (total == 0) return 0;
    const auto busy = user_delta + system_delta + nice_delta;
    return std::max(0.0, std::min(100.0, 100.0 * static_cast<double>(busy) / static_cast<double>(total)));
}

MemoryInfo Memory()
{
    MemoryInfo result;
    result.Total = SysctlU64("hw.memsize");

    vm_statistics64_data_t statistics{};
    mach_msg_type_number_t count = HOST_VM_INFO64_COUNT;
    if (::host_statistics64(::mach_host_self(), HOST_VM_INFO64, reinterpret_cast<host_info64_t>(&statistics),
                            &count) == KERN_SUCCESS)
    {
        vm_size_t page_size = 0;
        if (::host_page_size(::mach_host_self(), &page_size) == KERN_SUCCESS)
        {
            const auto free_pages = static_cast<std::uint64_t>(statistics.free_count);
            const auto inactive = static_cast<std::uint64_t>(statistics.inactive_count);
            const auto speculative = static_cast<std::uint64_t>(statistics.speculative_count);
            result.Free = free_pages * page_size;
            result.Available = (free_pages + inactive + speculative) * page_size;
        }
    }
    return result;
}

DiskInfo DiskUsage(const std::string& path)
{
    struct statvfs stats{};
    if (::statvfs(path.c_str(), &stats) != 0) return {};
    DiskInfo result;
    result.Total = static_cast<std::uint64_t>(stats.f_blocks) * stats.f_frsize;
    result.Free = static_cast<std::uint64_t>(stats.f_bavail) * stats.f_frsize;
    return result;
}

std::vector<DriveEntry> Drives()
{
    std::vector<DriveEntry> result;
    struct statfs* mounts = nullptr;
    const int count = ::getmntinfo(&mounts, MNT_NOWAIT);
    for (int i = 0; i < count; i++)
    {
        const std::string file_system = mounts[i].f_fstypename;
        if (file_system == "devfs" || file_system == "autofs" || file_system == "none" ||
            file_system == "procfs")
        {
            continue;
        }
        struct statvfs stats{};
        if (::statvfs(mounts[i].f_mntonname, &stats) != 0) continue;
        const auto total = static_cast<std::uint64_t>(stats.f_blocks) * stats.f_frsize;
        if (total == 0) continue;
        DriveEntry entry;
        entry.Path = mounts[i].f_mntonname;
        entry.FileSystem = file_system;
        entry.Total = total;
        entry.Free = static_cast<std::uint64_t>(stats.f_bavail) * stats.f_frsize;
        result.push_back(std::move(entry));
    }
    return result;
}

NetTotals NetTotalsSinceStart()
{
    NetTotals result;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) != 0) return result;
    for (auto* item = addresses; item != nullptr; item = item->ifa_next)
    {
        if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_LINK) continue;
        if (item->ifa_name == nullptr || StartsWith(item->ifa_name, "lo")) continue;
        if (item->ifa_data == nullptr) continue;
        const auto* data = reinterpret_cast<const struct if_data*>(item->ifa_data);
        result.Sent += data->ifi_obytes;
        result.Received += data->ifi_ibytes;
        result.Valid = true;
    }
    ::freeifaddrs(addresses);
    return result;
}

std::uint64_t ProcessCount()
{
    int mib[4] = {CTL_KERN, KERN_PROC, KERN_PROC_ALL, 0};
    std::size_t size = 0;
    if (::sysctl(mib, 4, nullptr, &size, nullptr, 0) != 0 || size == 0) return 0;
    return size / sizeof(kinfo_proc);
}

std::vector<std::string> ProcessNames()
{
    std::vector<std::string> result;
    int mib[4] = {CTL_KERN, KERN_PROC, KERN_PROC_ALL, 0};
    std::size_t size = 0;
    if (::sysctl(mib, 4, nullptr, &size, nullptr, 0) != 0 || size == 0) return result;

    std::vector<kinfo_proc> processes(size / sizeof(kinfo_proc));
    if (::sysctl(mib, 4, processes.data(), &size, nullptr, 0) != 0) return result;
    for (const auto& process : processes)
    {
        if (process.kp_proc.p_comm[0] != '\0') result.emplace_back(process.kp_proc.p_comm);
    }
    return result;
}

std::uint64_t UptimeSeconds()
{
    struct timeval boot{};
    std::size_t size = sizeof(boot);
    int mib[2] = {CTL_KERN, KERN_BOOTTIME};
    if (::sysctl(mib, 2, &boot, &size, nullptr, 0) != 0) return 0;
    const auto now = UnixSecondsNow();
    return now > boot.tv_sec ? static_cast<std::uint64_t>(now - boot.tv_sec) : 0;
}

double LoadAverage()
{
    double values[3] = {};
    if (::getloadavg(values, 3) < 1) return 0;
    return values[0];
}

HardwareInfo Hardware()
{
    HardwareInfo result;
    result.MachineGuid = CleanId(SysctlString("kern.uuid"));
    result.UUID = result.MachineGuid;
    result.Board = CleanId(SysctlString("hw.model"));
    result.SerialNumber = CleanId(SysctlString("hw.serialnumber"));

    std::set<std::string> macs;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) == 0)
    {
        for (auto* item = addresses; item != nullptr; item = item->ifa_next)
        {
            if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_LINK) continue;
            if (item->ifa_name == nullptr || IsVirtualInterface(item->ifa_name)) continue;
            const auto* link = reinterpret_cast<const sockaddr_dl*>(item->ifa_addr);
            if (link->sdl_alen != 6) continue;
            const auto* data = reinterpret_cast<const unsigned char*>(LLADDR(link));
            const auto mac = FormatMac(data, 6);
            if (!mac.empty() && mac != "00-00-00-00-00-00") macs.insert(mac);
        }
        ::freeifaddrs(addresses);
    }
    result.Macs = Join(std::vector<std::string>(macs.begin(), macs.end()), ",");
    return result;
}

SystemInfo System()
{
    SystemInfo result;
    result.OSName = "macOS";
    result.OSVersion = SysctlString("kern.osproductversion");
    if (result.OSVersion.empty()) result.OSVersion = SysctlString("kern.osrelease");
    result.HostName = SysctlString("kern.hostname");
    if (const auto* user = ::getpwuid(::geteuid())) result.UserName = user->pw_name;
    if (result.UserName.empty()) result.UserName = staragent::EnvironmentValue({"USER"});
    result.TimeZone = TimeZoneName();
    return result;
}

NetworkInfo Network()
{
    NetworkInfo result;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) == 0)
    {
        for (auto* item = addresses; item != nullptr; item = item->ifa_next)
        {
            if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_INET) continue;
            if (item->ifa_name == nullptr || StartsWith(item->ifa_name, "lo")) continue;
            if ((item->ifa_flags & IFF_UP) == 0) continue;
            const auto address = SockaddrToIp(item->ifa_addr);
            if (address.empty() || StartsWith(address, "127.") || StartsWith(address, "169.254.")) continue;
            result.IP = address;
            break;
        }
        ::freeifaddrs(addresses);
    }
    result.Gateway = ParseRouteGateway();
    result.Dns = ParseDns();
    if (result.Dns.size() > 50) result.Dns.resize(50);
    return result;
}

std::string LocalIP() { return Network().IP; }
std::string Gateway() { return ParseRouteGateway(); }
std::string Dns() { return ParseDns(); }

std::string Architecture()
{
#if defined(__x86_64__)
    return "X64";
#elif defined(__aarch64__) || defined(__arm64__)
    return "Arm64";
#elif defined(__i386__)
    return "X86";
#else
    return "Unknown";
#endif
}

std::string ExecutablePath()
{
    char buffer[4096] = {};
    std::uint32_t size = sizeof(buffer);
    if (::_NSGetExecutablePath(buffer, &size) != 0) return {};
    char resolved[4096] = {};
    if (::realpath(buffer, resolved) == nullptr) return buffer;
    return resolved;
}

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

std::string DefaultDataDir()
{
    if (IsRoot()) return "/Library/Application Support/StarAgent";
    const auto home = HomeDir();
    return home.empty() ? "data" : home + "/Library/Application Support/StarAgent";
}

std::string TimeZoneName()
{
    const auto env = staragent::EnvironmentValue({"TZ"});
    if (!env.empty()) return env;
    char buffer[PATH_MAX] = {};
    const auto length = ::readlink("/etc/localtime", buffer, sizeof(buffer) - 1);
    if (length > 0)
    {
        std::string path(buffer, static_cast<std::size_t>(length));
        const auto position = path.find("zoneinfo/");
        if (position != std::string::npos) return path.substr(position + 9);
    }
    return {};
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
        int null_fd = ::open("/dev/null", O_RDONLY);
        if (null_fd >= 0)
        {
            ::dup2(null_fd, STDIN_FILENO);
            ::close(null_fd);
        }
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

bool SleepCancelable(int milliseconds, const std::atomic<bool>& cancelled)
{
    const auto deadline = SteadyMsNow() + milliseconds;
    while (!cancelled.load() && SteadyMsNow() < deadline)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(
            std::min<std::int64_t>(50, std::max<std::int64_t>(1, deadline - SteadyMsNow()))));
    }
    return !cancelled.load();
}

} // namespace staragent::platform
