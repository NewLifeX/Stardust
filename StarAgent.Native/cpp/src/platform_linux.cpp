// SPDX-License-Identifier: MIT
// Linux metrics, identity and command execution.
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
#include <dirent.h>
#include <fcntl.h>
#include <fstream>
#include <ifaddrs.h>
#include <mutex>
#include <net/if.h>
#include <netinet/in.h>
#include <netpacket/packet.h>
#include <pwd.h>
#include <set>
#include <sstream>
#include <sys/stat.h>
#include <sys/statvfs.h>
#include <sys/sysinfo.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/utsname.h>
#include <sys/wait.h>
#include <thread>
#include <unistd.h>

namespace staragent::platform {
namespace {

std::mutex g_mutex;
std::uint64_t g_prev_idle = 0;
std::uint64_t g_prev_total = 0;
bool g_has_cpu_sample = false;

std::string ReadFile(const std::string& path, std::size_t max = 1024 * 1024)
{
    std::ifstream stream(path, std::ios::binary);
    if (!stream) return {};
    std::string result;
    result.resize(max);
    stream.read(result.data(), static_cast<std::streamsize>(max));
    result.resize(static_cast<std::size_t>(stream.gcount()));
    return result;
}

std::string ReadTrim(const std::string& path)
{
    return Trim(ReadFile(path, 64 * 1024));
}

bool IsVirtualInterface(const std::string& name)
{
    const auto lower = ToLower(name);
    const char* prefixes[] = {"lo",      "docker",  "veth",    "br-",     "virbr",   "vmnet",
                              "tun",     "tap",     "utun",    "awdl",    "llw",     "bridge",
                              "vboxnet", "zt",      "tailscale", "wg",    "cni",     "flannel"};
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

std::string LinuxGateway()
{
    std::istringstream stream(ReadFile("/proc/net/route"));
    std::string line;
    std::getline(stream, line);
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string interface, destination, gateway;
        if (!(fields >> interface >> destination >> gateway)) continue;
        if (destination != "00000000") continue;
        const auto value = static_cast<std::uint32_t>(std::strtoul(gateway.c_str(), nullptr, 16));
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_addr.s_addr = value;
        return SockaddrToIp(reinterpret_cast<const sockaddr*>(&address));
    }
    return {};
}

std::string LinuxDns()
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

std::string LinuxDiskSerial()
{
    DIR* directory = ::opendir("/sys/block");
    if (directory == nullptr) return {};
    std::string result;
    while (auto* entry = ::readdir(directory))
    {
        const std::string name = entry->d_name;
        if (name == "." || name == "..") continue;
        if (StartsWith(name, "loop") || StartsWith(name, "ram") || StartsWith(name, "dm-") ||
            StartsWith(name, "md"))
        {
            continue;
        }
        auto serial = ReadTrim("/sys/block/" + name + "/device/serial");
        if (serial.empty()) serial = ReadTrim("/sys/block/" + name + "/serial");
        if (!serial.empty())
        {
            result = serial;
            break;
        }
    }
    ::closedir(directory);
    return result;
}

std::string CleanId(std::string value)
{
    value = Trim(value);
    if (!value.empty() && (value.front() == '"' || value.front() == '\'')) value.erase(0, 1);
    if (!value.empty() && (value.back() == '"' || value.back() == '\'')) value.pop_back();
    const auto lower = ToLower(value);
    static const std::set<std::string> invalid = {"", "none", "unknown", "not specified", "not settable",
                                                  "to be filled by o.e.m.", "default string",
                                                  "system serial number", "0",
                                                  "00000000-0000-0000-0000-000000000000",
                                                  "ffffffff-ffff-ffff-ffff-ffffffffffff"};
    return invalid.count(lower) > 0 ? std::string() : value;
}

std::string ReadOsRelease(const std::string& key)
{
    std::istringstream stream(ReadFile("/etc/os-release"));
    std::string line;
    while (std::getline(stream, line))
    {
        const auto position = line.find('=');
        if (position == std::string::npos) continue;
        if (line.substr(0, position) != key) continue;
        auto value = line.substr(position + 1);
        if (value.size() >= 2 && value.front() == '"' && value.back() == '"')
        {
            value = value.substr(1, value.size() - 2);
        }
        return value;
    }
    return {};
}

} // namespace

double CpuPercent()
{
    const auto content = ReadFile("/proc/stat", 4096);
    const auto newline = content.find('\n');
    if (!StartsWith(content, "cpu ")) return 0;
    std::istringstream stream(content.substr(0, newline));
    std::string label;
    std::uint64_t values[10] = {};
    stream >> label;
    for (auto& value : values) stream >> value;
    if (!stream) return 0;

    const std::uint64_t idle = values[3] + values[4];
    const std::uint64_t total = values[0] + values[1] + values[2] + values[3] + values[4] + values[5] + values[6] +
                                values[7] + values[8] + values[9];

    std::lock_guard<std::mutex> lock(g_mutex);
    if (!g_has_cpu_sample)
    {
        g_prev_idle = idle;
        g_prev_total = total;
        g_has_cpu_sample = true;
        return 0;
    }
    const auto idle_delta = idle - g_prev_idle;
    const auto total_delta = total - g_prev_total;
    g_prev_idle = idle;
    g_prev_total = total;
    if (total_delta == 0) return 0;
    const auto busy = total_delta > idle_delta ? total_delta - idle_delta : 0;
    return std::max(0.0, std::min(100.0, 100.0 * static_cast<double>(busy) / static_cast<double>(total_delta)));
}

MemoryInfo Memory()
{
    MemoryInfo result;
    std::istringstream stream(ReadFile("/proc/meminfo"));
    std::string line;
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string key;
        std::uint64_t value = 0;
        std::string unit;
        if (!(fields >> key >> value)) continue;
        fields >> unit;
        const std::uint64_t bytes = value * 1024;
        if (key == "MemTotal:") result.Total = bytes;
        else if (key == "MemAvailable:") result.Available = bytes;
        else if (key == "MemFree:") result.Free = bytes;
    }
    if (result.Available == 0) result.Available = result.Free;
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
    std::istringstream stream(ReadFile("/proc/mounts"));
    std::string line;
    static const std::set<std::string> ignored = {
        "proc",       "sysfs",      "devtmpfs",  "devpts",   "cgroup",    "cgroup2", "securityfs",
        "pstore",     "efivarfs",   "bpf",       "tracefs",  "debugfs",   "configfs", "fusectl",
        "mqueue",     "hugetlbfs",  "ramfs",     "autofs",   "binfmt_misc", "rpc_pipefs", "nsfs"};
    while (std::getline(stream, line))
    {
        std::istringstream fields(line);
        std::string device, path, file_system;
        if (!(fields >> device >> path >> file_system)) continue;
        if (ignored.count(file_system) > 0) continue;
        if (StartsWith(path, "/sys") || StartsWith(path, "/proc") || StartsWith(path, "/dev")) continue;
        struct statvfs stats{};
        if (::statvfs(path.c_str(), &stats) != 0) continue;
        const auto total = static_cast<std::uint64_t>(stats.f_blocks) * stats.f_frsize;
        if (total == 0) continue;
        DriveEntry entry;
        entry.Path = path;
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
    std::istringstream stream(ReadFile("/proc/net/dev"));
    std::string line;
    std::getline(stream, line);
    std::getline(stream, line);
    while (std::getline(stream, line))
    {
        const auto colon = line.find(':');
        if (colon == std::string::npos) continue;
        auto interface = Trim(line.substr(0, colon));
        if (interface == "lo" || StartsWith(interface, "lo:")) continue;
        std::istringstream fields(line.substr(colon + 1));
        std::uint64_t receive = 0, transmit = 0;
        std::uint64_t values[16] = {};
        for (auto& value : values)
        {
            if (!(fields >> value)) break;
        }
        receive = values[0];
        transmit = values[8];
        result.Received += receive;
        result.Sent += transmit;
        result.Valid = true;
    }
    return result;
}

std::uint64_t ProcessCount()
{
    DIR* directory = ::opendir("/proc");
    if (directory == nullptr) return 0;
    std::uint64_t count = 0;
    while (auto* entry = ::readdir(directory))
    {
        if (entry->d_name[0] >= '0' && entry->d_name[0] <= '9') count++;
    }
    ::closedir(directory);
    return count;
}

std::vector<std::string> ProcessNames()
{
    std::vector<std::string> result;
    DIR* directory = ::opendir("/proc");
    if (directory == nullptr) return result;
    while (auto* entry = ::readdir(directory))
    {
        if (entry->d_name[0] < '0' || entry->d_name[0] > '9') continue;
        auto name = ReadTrim(std::string("/proc/") + entry->d_name + "/comm");
        if (!name.empty()) result.push_back(name);
    }
    ::closedir(directory);
    return result;
}

std::uint64_t UptimeSeconds()
{
    const auto text = ReadTrim("/proc/uptime");
    return static_cast<std::uint64_t>(std::strtod(text.c_str(), nullptr));
}

double LoadAverage()
{
    const auto text = ReadTrim("/proc/loadavg");
    return std::strtod(text.c_str(), nullptr);
}

HardwareInfo Hardware()
{
    HardwareInfo result;
    result.UUID = CleanId(ReadTrim("/sys/class/dmi/id/product_uuid"));
    result.MachineGuid = CleanId(ReadTrim("/etc/machine-id"));
    if (result.MachineGuid.empty()) result.MachineGuid = CleanId(ReadTrim("/var/lib/dbus/machine-id"));
    result.SerialNumber = CleanId(ReadTrim("/sys/class/dmi/id/product_serial"));
    result.Board = CleanId(ReadTrim("/sys/class/dmi/id/board_serial"));
    if (result.Board.empty()) result.Board = CleanId(ReadTrim("/sys/class/dmi/id/board_name"));
    result.DiskID = CleanId(LinuxDiskSerial());

    std::set<std::string> macs;
    struct ifaddrs* addresses = nullptr;
    if (::getifaddrs(&addresses) == 0)
    {
        for (auto* item = addresses; item != nullptr; item = item->ifa_next)
        {
            if (item->ifa_addr == nullptr || item->ifa_addr->sa_family != AF_PACKET) continue;
            if (item->ifa_name == nullptr || IsVirtualInterface(item->ifa_name)) continue;
            const auto* link = reinterpret_cast<const sockaddr_ll*>(item->ifa_addr);
            if (link->sll_halen != 6) continue;
            const auto mac = FormatMac(link->sll_addr, link->sll_halen);
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
    result.OSName = ReadOsRelease("NAME");
    const auto pretty = ReadOsRelease("PRETTY_NAME");
    if (!pretty.empty()) result.OSName = pretty;
    result.OSVersion = ReadOsRelease("VERSION_ID");

    struct utsname name{};
    if (::uname(&name) == 0)
    {
        if (result.OSName.empty()) result.OSName = name.sysname;
        if (result.OSVersion.empty()) result.OSVersion = name.release;
        result.HostName = name.nodename;
    }

    char hostname[256] = {};
    if (::gethostname(hostname, sizeof(hostname)) == 0) result.HostName = hostname;

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
    result.Gateway = LinuxGateway();
    result.Dns = LinuxDns();
    if (result.Dns.size() > 50) result.Dns.resize(50);
    return result;
}

std::string LocalIP() { return Network().IP; }
std::string Gateway() { return LinuxGateway(); }
std::string Dns() { return LinuxDns(); }

std::string Architecture()
{
#if defined(__x86_64__) || defined(__amd64__)
    return "X64";
#elif defined(__i386__)
    return "X86";
#elif defined(__aarch64__)
    return "Arm64";
#elif defined(__arm__)
    return "Arm";
#elif defined(__riscv) && __riscv_xlen == 64
    return "RiscV64";
#elif defined(__loongarch64)
    return "LA64";
#elif defined(__mips64)
    return "Mips64";
#else
    return "Unknown";
#endif
}

std::string ExecutablePath()
{
    char buffer[4096] = {};
    const auto length = ::readlink("/proc/self/exe", buffer, sizeof(buffer) - 1);
    if (length <= 0) return {};
    return std::string(buffer, static_cast<std::size_t>(length));
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
    if (IsRoot()) return "/var/lib/staragent";
    const auto home = HomeDir();
    return home.empty() ? "data" : home + "/.staragent";
}

std::string TimeZoneName()
{
    auto timezone = ReadTrim("/etc/timezone");
    if (!timezone.empty()) return timezone;
    const auto env = staragent::EnvironmentValue({"TZ"});
    return env;
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
    if (result.ExitCode == 127 && Trim(result.Output).empty()) result.Error = "/bin/bash not available";
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
        for (const auto& argument : arguments)
        {
            argv.push_back(const_cast<char*>(argument.c_str()));
        }
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
