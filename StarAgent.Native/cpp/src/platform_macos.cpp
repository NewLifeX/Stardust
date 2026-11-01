// SPDX-License-Identifier: MIT
// macOS metrics, identity and command execution.
#include "platform.hpp"

#include "config.hpp"
#include "output_buffer.hpp"
#include "platform_posix.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <cctype>
#include <cerrno>
#include <chrono>
#include <cmath>
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
    return internal::ReadSysFile(path, max);
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

// 以下解析逻辑与 Linux 共用，实现见 platform_posix.cpp。
using internal::CleanId;
using internal::FormatMac;
using internal::IsVirtualInterface;
using internal::SockaddrToIp;

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

std::string ParseDns() { return internal::DnsServers(); }

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

TcpCounts TcpStates()
{
    // macOS 没有 /proc/net/tcp，借助 netstat 统计各状态连接数
    TcpCounts result;
    static const std::atomic<bool> never_cancelled{false};
    const auto shell = RunShell("netstat -an -p tcp", 5000, never_cancelled);
    std::istringstream stream(shell.Output);
    std::string line;
    while (std::getline(stream, line))
    {
        const auto text = Trim(line);
        if (text.empty()) continue;
        const auto space = text.find_last_of(" \t");
        if (space == std::string::npos) continue;
        const auto state = text.substr(space + 1);
        if (state == "ESTABLISHED")
            result.Established++;
        else if (state == "TIME_WAIT")
            result.TimeWait++;
        else if (state == "CLOSE_WAIT")
            result.CloseWait++;
    }
    return result;
}

double Temperature()
{
    // macOS 温度需要 IOKit SMC 私有接口，暂不采集
    return 0;
}

double BatteryPercent()
{
    // macOS 电量需要 IOKit AppleSmartBattery，暂不采集
    return 0;
}

int WirelessSignal()
{
    // 无线信号强度暂不采集
    return 0;
}

DiskIoSample DiskIo()
{
    // 磁盘 IO 速率暂不采集
    return {};
}

std::uint64_t MaxOpenFiles() { return 0; }

ProbeResult PingProbe(const std::string& host, int timeout_ms, int count){
    ProbeResult result;
    if (host.empty() || count <= 0) return result;

    static const std::atomic<bool> never_cancelled{false};
    const auto timeout_sec = std::max(1, (timeout_ms + 999) / 1000);
    const auto command = "ping -c " + std::to_string(count) + " -t " + std::to_string(timeout_sec) + " " + host;
    const auto shell = RunShell(command, (count + 2) * 1000 + 2000, never_cancelled);
    if (shell.Output.empty()) return result;

    int success = 0;
    double total = 0;
    std::size_t position = 0;
    while ((position = shell.Output.find("time=", position)) != std::string::npos)
    {
        position += 5;
        auto end = position;
        while (end < shell.Output.size() &&
               (std::isdigit(static_cast<unsigned char>(shell.Output[end])) || shell.Output[end] == '.'))
        {
            end++;
        }
        if (end > position)
        {
            success++;
            total += std::strtod(shell.Output.substr(position, end - position).c_str(), nullptr);
        }
    }

    result.Valid = true;
    if (success == 0)
    {
        result.LossRate = 1;
        return result;
    }
    result.Latency = static_cast<int>(std::lround(total / success));
    result.LossRate = std::round((1.0 - static_cast<double>(success) / count) * 1000.0) / 1000.0;
    result.Score = ComputePingScore(result.Latency, success, count);
    return result;
}

bool PowerAction(bool reboot)
{
    static const std::atomic<bool> never_cancelled{false};
    const auto shell = RunShell(reboot ? "shutdown -r now" : "shutdown -h now", 5000, never_cancelled);
    return shell.ExitCode == 0;
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

} // namespace staragent::platform
